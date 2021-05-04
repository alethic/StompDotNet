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
        class FrameRouter
        {

            readonly Func<StompFrame, bool> match;
            readonly Func<StompFrame, CancellationToken, ValueTask<bool>> routeAsync;
            readonly Func<Exception, CancellationToken, ValueTask> abortAsync;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="match"></param>
            /// <param name="writeAsync"></param>
            /// <param name="abortAsync"></param>
            public FrameRouter(Func<StompFrame, bool> match, Func<StompFrame, CancellationToken, ValueTask<bool>> writeAsync, Func<Exception, CancellationToken, ValueTask> abortAsync)
            {
                this.match = match ?? throw new ArgumentNullException(nameof(match));
                this.routeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
                this.abortAsync = abortAsync ?? throw new ArgumentNullException(nameof(abortAsync));
            }

            /// <summary>
            /// Filter to execute against received frames in order to decide whether to resume the handle.
            /// </summary>
            public Func<StompFrame, bool> Match => match;

            /// <summary>
            /// Action to be invoked when a matching frame is received. Returns <c>true</c> or <c>false</c> to signal whether to maintain the listener.
            /// </summary>
            public Func<StompFrame, CancellationToken, ValueTask<bool>> RouteAsync => routeAsync;

            /// <summary>
            /// Action to be invoked when the router is aborted.
            /// </summary>
            public Func<Exception, CancellationToken, ValueTask> AbortAsync => abortAsync;

        }

        readonly StompTransport transport;
        readonly StompConnectionOptions options;
        readonly ILogger logger;

        readonly AsyncLock stateLock = new AsyncLock();

        // channels that store frames as they move in and out of the system
        readonly Channel<StompFrame> recv = Channel.CreateUnbounded<StompFrame>();
        readonly Channel<StompFrame> send = Channel.CreateUnbounded<StompFrame>();

        // internal frame routing
        readonly LinkedList<FrameRouter> routers = new LinkedList<FrameRouter>();
        readonly AsyncLock routersLock = new AsyncLock();

        Task runner;
        CancellationTokenSource runnerCts;
        int prevReceiptId = 0;
        int prevSubscriptionId = 0;

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
            using (await stateLock.LockAsync(cancellationToken))
            {
                if (runner != null)
                    throw new StompException("Connection has already been started.");

                // ensure there are no listeners hanging around
                using (await routersLock.LockAsync(cancellationToken))
                    routers.Clear();

                // begin new run loop
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
            using (await stateLock.LockAsync(cancellationToken))
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
                    // disconnect, but only allow 2 seconds
                    var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;
                    var combine = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout).Token;
                    await DisconnectAsync(cancellationToken: CancellationToken.None);
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

                // abort any outstanding routers, they'll never complete
                await AbortAsync(cancellationToken);

                // signal cancellation of the runner and wait for completion
                runnerCts.Cancel();
                await runner;

                // clean up the runner state
                runner = null;
                runnerCts = null;

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
                    var readTask = transport.ReceiveAsync(recv.Writer, cancellationToken).AsTask();
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
                    logger.LogError(e, "Unhandled exception during STOMP connecton processing.");
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
            try
            {
                while (await send.Reader.WaitToReadAsync(cancellationToken))
                    await OnSendAsync(await send.Reader.ReadAsync(cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        /// <summary>
        /// Handles outgoing messages to the transport.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async ValueTask OnSendAsync(StompFrame frame, CancellationToken cancellationToken)
        {
            logger.LogDebug("Sending STOMP frame: {Command}", frame.Command);

            await transport.SendAsync(frame, cancellationToken);
        }

        /// <summary>
        /// Processes incoming items from the transport.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task RunReceiveAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await recv.Reader.WaitToReadAsync(cancellationToken))
                    await OnReceiveAsync(await recv.Reader.ReadAsync(cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        /// <summary>
        /// Handles incoming received messages from the transport.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async ValueTask OnReceiveAsync(StompFrame frame, CancellationToken cancellationToken)
        {
            logger.LogDebug("Received STOMP frame: {Command}", frame.Command);

            // execute and act upon any registered listeners
            using (await routersLock.LockAsync(cancellationToken))
                if (FindListenerNode(frame) is LinkedListNode<FrameRouter> node)
                    if (await TryRouteAsync(node.Value, frame, cancellationToken) == false)
                        if (node.List != null)
                            routers.Remove(node);
        }

        /// <summary>
        /// Attempts to execute the listener and return whether or not it should be removed. If the listener throws an exception, it is logged and marked for removal.
        /// </summary>
        /// <param name="router"></param>
        /// <param name="frame"></param>
        /// <returns></returns>
        async ValueTask<bool> TryRouteAsync(FrameRouter router, StompFrame frame, CancellationToken cancellationToken)
        {
            try
            {
                return await router.RouteAsync(frame, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unhandled exception occurred dispatching frame to trigger.");
                return false;
            }
        }

        /// <summary>
        /// Invoked when the send or receive loops are finished.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async ValueTask AbortAsync(CancellationToken cancellationToken)
        {
            var exception = new StompConnectionAbortedException();

            using (await routersLock.LockAsync(cancellationToken))
            {
                // send abort exception to all of the routers
                for (var node = routers.First; node != null; node = node.Next)
                    await TryAbortRouter(node.Value, exception, cancellationToken); // result does not matter, always error

                // clear the list of listeners
                routers.Clear();
            }
        }

        /// <summary>
        /// Attempts to pass an error to the router and return whether or not it should be removed.
        /// </summary>
        /// <param name="router"></param>
        /// <param name="exception"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async ValueTask TryAbortRouter(FrameRouter router, Exception exception, CancellationToken cancellationToken)
        {
            try
            {
                await router.AbortAsync(exception, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unhandled exception occurred dispatching exception to listener.");
            }
        }

        /// <summary>
        /// Finds the node of the handle list that matches the given frame.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        LinkedListNode<FrameRouter> FindListenerNode(StompFrame frame)
        {
            for (var n = routers.First; n != null; n = n.Next)
                if (n.Value.Match(frame))
                    return n;

            return null;
        }

        /// <summary>
        /// Sends the specified frame.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        async ValueTask SendAsync(StompFrame frame, CancellationToken cancellationToken)
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
        async ValueTask<StompFrame> SendAndWaitAsync(StompCommand command, IEnumerable<KeyValuePair<string, string>> headers, ReadOnlyMemory<byte> body, Func<StompFrame, bool> response, CancellationToken cancellationToken)
        {
            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();

            // schedule a router for the frame filter
            var tcs = new TaskCompletionSource<StompFrame>();
            ValueTask<bool> WriteAsync(StompFrame frame, CancellationToken cancellationToken) { tcs.SetResult(frame); return new ValueTask<bool>(false); }
            ValueTask AbortAsync(Exception exception, CancellationToken cancellationToken) { tcs.SetException(exception); return ValueTask.CompletedTask; }
            var hnd = new FrameRouter(response, WriteAsync, AbortAsync);

            // handle cancellation through new task
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            // subscribe to matching frames
            var node = await RegisterRouterAsync(hnd, cancellationToken);

            try
            {
                // send initial frame and wait for resumption
                await SendAsync(new StompFrame(command, headers, body), cancellationToken);
                return await tcs.Task;
            }
            finally
            {
                // ensure listener is removed upon completion
                if (node.List != null)
                    using (await routersLock.LockAsync())
                        if (node.List != null)
                            routers.Remove(node);
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
        ValueTask<StompFrame> SendAndWaitWithReceiptAsync(StompCommand command, IEnumerable<KeyValuePair<string, string>> headers, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();

            var receiptIdText = Interlocked.Increment(ref prevReceiptId).ToString();
            headers = headers.Prepend(new KeyValuePair<string, string>("receipt", receiptIdText));
            return SendAndWaitAsync(command, headers, body, f => f.GetHeaderValue("receipt-id") == receiptIdText, cancellationToken);
        }

        /// <summary>
        /// Registers a trigger for a frame condition.
        /// </summary>
        /// <param name="trigger"></param>
        /// <returns></returns>
        async ValueTask<LinkedListNode<FrameRouter>> RegisterRouterAsync(FrameRouter trigger, CancellationToken cancellationToken)
        {
            using (await routersLock.LockAsync(cancellationToken))
                return routers.AddLast(trigger);
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
        /// Creates a subscription where each individual message requires an acknowledgement. This implements the 'client-individual' STOMP ack method.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask<StompMessageSubscription> SubscribeAutoAsync(string destination, IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException($"'{nameof(destination)}' cannot be null or whitespace.", nameof(destination));

            var id = Interlocked.Increment(ref prevSubscriptionId).ToString();
            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();
            headers = headers.Prepend(new KeyValuePair<string, string>("destination", destination));
            headers = headers.Prepend(new KeyValuePair<string, string>("id", id));
            headers = headers.Prepend(new KeyValuePair<string, string>("ack", "auto"));

            // establish a router for inbound messages, and direct them to a new channel for the subscription
            var channel = Channel.CreateBounded<StompFrame>(1);
            async ValueTask<bool> RouteAsync(StompFrame frame, CancellationToken cancellationToken) { await channel.Writer.WriteAsync(frame, cancellationToken); return true; }
            ValueTask AbortAsync(Exception exception, CancellationToken cancellationToken) { channel.Writer.Complete(exception); return ValueTask.CompletedTask; }
            var router = await RegisterRouterAsync(new FrameRouter(frame => frame.GetHeaderValue("subscription") == id, RouteAsync, AbortAsync), cancellationToken);

            // completes the channel and removes the listener
            async ValueTask CompleteAsync(Exception exception)
            {
                // remove the listener to stop sending
                if (router.List != null)
                    using (await routersLock.LockAsync(CancellationToken.None))
                        if (router.List != null)
                            routers.Remove(router);

                // signal to channel that we're done with messages
                channel.Writer.Complete(exception);
            }

            try
            {
                // send SUBSCRIBE command
                await SendAndWaitWithReceiptAsync(StompCommand.Subscribe, headers, null, cancellationToken);
            }
            catch (Exception e)
            {
                // complete the channel with an error if we can't even establish it
                await CompleteAsync(e);
                throw;
            }

            // caller obtains a subscription reference that closes the channel upon completion
            return new StompMessageSubscription(this, id, channel.Reader, CompleteAsync);
        }

        /// <summary>
        /// Creates a subscription where the stream of messages thus far received can be acknowledged in a batch. This implements the 'client' STOMP ack method.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask<StompMessageSubscription> SubscribeClientAsync(string destination, IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a subscription where each individual message requires an acknowledgement. This implements the 'client-individual' STOMP ack method.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask<StompMessageSubscription> SubscribeClientIndividualAsync(string destination, IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes the specified subscription.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async ValueTask UnsubscribeAsync(string id, IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException($"'{nameof(id)}' cannot be null or empty.", nameof(id));

            headers ??= Enumerable.Empty<KeyValuePair<string, string>>();
            headers = headers.Prepend(new KeyValuePair<string, string>("id", id));
            await SendAndWaitWithReceiptAsync(StompCommand.Unsubscribe, headers, null, cancellationToken);
        }

        /// <summary>
        /// Unsubscribes the specified subscription.
        /// </summary>
        /// <param name="subscription"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async ValueTask UnsubscribeAsync(StompMessageSubscription subscription, IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken)
        {
            if (subscription is null)
                throw new ArgumentNullException(nameof(subscription));

            await UnsubscribeAsync(subscription.Id, headers, cancellationToken);
            await subscription.CompleteAsync(null);
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