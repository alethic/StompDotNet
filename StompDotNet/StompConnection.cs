using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Nito.AsyncEx;

namespace StompDotNet
{

    /// <summary>
    /// Maintains a connection to a STOMP server.
    /// </summary>
    public class StompConnection : IAsyncDisposable
    {

        /// <summary>
        /// Describes a condition and a handle to resume when the condition is met.
        /// </summary>
        class FrameTrigger
        {

            readonly Func<StompFrame, bool> filter;
            readonly Action<StompFrame> setResult;
            private readonly Action<Exception> setException;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="filter"></param>
            /// <param name="setResult"></param>
            /// <param name="setException"></param>
            public FrameTrigger(Func<StompFrame, bool> filter, Action<StompFrame> setResult, Action<Exception> setException)
            {
                this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
                this.setResult = setResult ?? throw new ArgumentNullException(nameof(setResult));
                this.setException = setException ?? throw new ArgumentNullException(nameof(setException));
            }

            /// <summary>
            /// Filter to execute against received frames in order to decide whether to resume the handle.
            /// </summary>
            public Func<StompFrame, bool> Filter => filter;

            /// <summary>
            /// Action to be invoked when a matching frame is received.
            /// </summary>
            public Action<StompFrame> SetResult => setResult;

            /// <summary>
            /// Action to be invoked when the trigger terminates.
            /// </summary>
            public Action<Exception> SetException => setException;

        }

        readonly StompTransport transport;
        readonly StompConnectionOptions options;
        readonly ILogger logger;

        readonly AsyncLock sync = new AsyncLock();
        readonly Channel<StompFrame> recv = Channel.CreateUnbounded<StompFrame>();
        readonly Channel<StompFrame> send = Channel.CreateUnbounded<StompFrame>();
        readonly LinkedList<FrameTrigger> triggers = new LinkedList<FrameTrigger>();

        Task runner;
        CancellationTokenSource runnerCts;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="transport"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public StompConnection(StompTransport transport, StompConnectionOptions options, ILogger logger)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the connection.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task OpenAsync(CancellationToken cancellationToken)
        {
            using (await sync.LockAsync(cancellationToken))
            {
                if (runner != null)
                    throw new StompException("Connection has already been started.");

                // begin new run loop
                triggers.Clear();
                runnerCts = new CancellationTokenSource();
                runner = Task.Run(() => RunAsync(runnerCts.Token), CancellationToken.None);
            }
        }

        /// <summary>
        /// Stops the connection.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            return CloseAsync(cancellationToken, false);
        }

        /// <summary>
        /// Stops the connection.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task CloseAsync(CancellationToken cancellationToken, bool disposing = false)
        {
            using (await sync.LockAsync(cancellationToken))
            {
                // during disposal, we don't want to do any work if we're already closed
                if (runner == null && disposing)
                    return;

                // but otherwise, we should throw an exception about the wrong state
                if (runner == null)
                    throw new StompException("Connection is not started.");

                // disconnect from the transport
                try
                {
                    await DisconnectAsync(cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // ignore, cancellation might cause connection drop and timeout
                }
                catch (StompConnectionAbortedException)
                {
                    // ignore, connection ends up closed without receipt for disconnect
                }
                catch (StompException e)
                {
                    logger.LogError(e, "Unexpected exception disconnecting from the STOMP server.");
                }

                // signal cancellation of the runner and wait for completion
                runnerCts.Cancel();
                await runner;

                // clean up the runner state
                runner = null;
                runnerCts = null;
                triggers.Clear();

                // close and dispose of the transport
                await transport.CloseAsync(cancellationToken);
                await transport.DisposeAsync();
            }
        }

        /// <summary>
        /// Background processing for the connection.
        /// </summary>
        /// <returns></returns>
        async Task RunAsync(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    var readTask = transport.ReceiveAsync(recv.Writer.WriteAsync, cancellationToken).AsTask();
                    var recvTask = RunReceiveAsync(cancellationToken);
                    var sendTask = RunSendAsync(cancellationToken);
                    await Task.WhenAll(readTask, recvTask, sendTask);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Unhandled exception during STOMP processing.");
                }
            }
        }

        /// <summary>
        /// Processes outgoing items to the transport.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task RunSendAsync(CancellationToken cancellationToken)
        {
            while (await send.Reader.WaitToReadAsync(cancellationToken))
                await transport.SendAsync(await send.Reader.ReadAsync(cancellationToken), cancellationToken);

            // reached the end of the available messages, abort outstanding triggers
            await AbortAsync(cancellationToken);
        }

        /// <summary>
        /// Processes incoming items from the transport.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task RunReceiveAsync(CancellationToken cancellationToken)
        {
            while (await recv.Reader.WaitToReadAsync(cancellationToken))
                await OnReceiveAsync(await recv.Reader.ReadAsync(cancellationToken), cancellationToken);

            // reached the end of the available events, abort outstanding triggers
            await AbortAsync(cancellationToken);
        }

        /// <summary>
        /// Handles incoming received messages from the transport.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask OnReceiveAsync(StompFrame frame, CancellationToken cancellationToken)
        {
            // resume any handles that match
            lock (triggers)
            {
                var node = FindEventHandleNode(frame);
                if (node != null)
                {
                    // remove trigger immediately to prevent double calls
                    if (node.List != null)
                        triggers.Remove(node);

                    try
                    {
                        // execute trigger action
                        node.Value.SetResult(frame);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Unhandled exception occurred dispatching frame to trigger.");
                    }
                }
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Invoked when the send or receive loops are finished.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async ValueTask AbortAsync(CancellationToken cancellationToken)
        {
            var e = new StompConnectionAbortedException();

            lock (triggers)
            {
                for (var node = triggers.First; node != null; node = node.Next)
                {
                    triggers.Remove(node);
                    node.Value.SetException(e);
                }
            }

            await DisposeAsync();
        }

        /// <summary>
        /// Finds the node of the handle list that matches the given frame.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        LinkedListNode<FrameTrigger> FindEventHandleNode(StompFrame frame)
        {
            for (var n = triggers.First; n != null; n = n.Next)
                if (n.Value.Filter(frame))
                    return n;

            return null;
        }

        /// <summary>
        /// Sends the specified frame.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        async Task SendAsync(StompFrame frame, CancellationToken cancellationToken)
        {
            await send.Writer.WriteAsync(frame, cancellationToken);
        }

        /// <summary>
        /// Sends the specified frame, and registers a completion for the next frame that matches the specified conditions.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        /// <param name="response"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<StompFrame> SendAndWaitAsync(StompCommand command, IEnumerable<KeyValuePair<string, string>> headers, ReadOnlyMemory<byte> body, Func<StompFrame, bool> response, CancellationToken cancellationToken)
        {
            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();

            // schedule a trigger for the frame filter
            var tcs = new TaskCompletionSource<StompFrame>();
            var hnd = new FrameTrigger(response, tcs.SetResult, tcs.SetException);

            // handle cancellation through new task
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            // subscribe to matching frames
            var node = RegisterTrigger(hnd);

            try
            {
                // send initial frame and wait for resumption
                await SendAsync(new StompFrame(command, headers, body), cancellationToken);
                return await tcs.Task;
            }
            finally
            {
                // ensure trigger is removed upon completion
                if (node.List != null)
                    lock (triggers)
                        if (node.List != null)
                            triggers.Remove(node);
            }
        }

        /// <summary>
        /// Sends the specified frame, and registers a completion for the response frame by receipt ID.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<StompFrame> SendAndWaitWithReceiptAsync(StompCommand command, IEnumerable<KeyValuePair<string, string>> headers, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();

            var receiptId = Guid.NewGuid().ToString("n");
            headers = headers.Prepend(new KeyValuePair<string, string>("receipt", receiptId));
            return SendAndWaitAsync(command, headers, body, f => f.GetHeaderValue("receipt-id") == receiptId, cancellationToken);
        }

        /// <summary>
        /// Registers a trigger for a frame condition.
        /// </summary>
        /// <param name="trigger"></param>
        /// <returns></returns>
        LinkedListNode<FrameTrigger> RegisterTrigger(FrameTrigger trigger)
        {
            lock (triggers)
                return triggers.AddLast(trigger);
        }

        /// <summary>
        /// Initiates the connection to the STOMP server.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="login"></param>
        /// <param name="passcode"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task ConnectAsync(string host, string login, string passcode, IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken)
        {
            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();

            if (GetStompVersionHeader() is string acceptVersion)
                headers = headers.Prepend(new KeyValuePair<string, string>("accept-version", acceptVersion));
            if (host != null)
                headers = headers.Prepend(new KeyValuePair<string, string>("host", host));

            var result = await SendAndWaitAsync(StompCommand.Connect, headers, null, frame => frame.Command == StompCommand.Connected || frame.Command == StompCommand.Error, cancellationToken);
            if (result.Command == StompCommand.Error)
                throw new StompException($"Error returned during STOMP connection: {Encoding.UTF8.GetString(result.Body.Span)}");
            if (result.Command != StompCommand.Connected)
                throw new StompException("Did not receive CONNECTED response.");
        }

        /// <summary>
        /// Gets the version header value.
        /// </summary>
        /// <returns></returns>
        string GetStompVersionHeader()
        {
            var l = new List<string>(4);
            if (options.MaximumVersion >= StompVersion.Stomp_1_0)
                l.Add("1.0");
            if (options.MaximumVersion >= StompVersion.Stomp_1_1)
                l.Add("1.1");
            if (options.MaximumVersion >= StompVersion.Stomp_1_2)
                l.Add("1.1");

            return string.Join(',', l);
        }

        /// <summary>
        /// Initiates a disconnection from the STOMP server.
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task DisconnectAsync(IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();

            var result = await SendAndWaitWithReceiptAsync(StompCommand.Disconnect, headers, null, cancellationToken);
            if (result.Command == StompCommand.Error)
                throw new StompException($"Error returned during STOMP connection: {Encoding.UTF8.GetString(result.Body.Span)}");
            if (result.Command != StompCommand.Receipt)
                throw new StompException("Did not receive RECEIPT response.");
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            await CloseAsync(CancellationToken.None, true);
        }

    }

}