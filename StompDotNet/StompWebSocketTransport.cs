using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace StompDotNet
{

    /// <summary>
    /// Implements a STOMP transport across a web socket.
    /// </summary>
    public class StompWebSocketTransport : StompPipeTransport
    {

        readonly EndPoint endpoint;
        readonly WebSocket socket;
        readonly StompBinaryProtocol protocol;
        readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="socket"></param>
        /// <param name="protocol"></param>
        /// <param name="logger"></param>
        public StompWebSocketTransport(EndPoint endpoint, WebSocket socket, StompBinaryProtocol protocol, ILogger logger) :
            base(protocol, logger)
        {
            this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
            this.protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the endpoint represented by the transport.
        /// </summary>
        public override EndPoint EndPoint => endpoint;

        /// <summary>
        /// Processes incoming data from the socket.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task FillPipeAsync(PipeWriter writer, CancellationToken cancellationToken)
        {
            try
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    var memory = writer.GetMemory(512);

                    // socket is no longer connected, we're done
                    if (socket.State != WebSocketState.Open)
                        break;

                    // receive some data from the web socket
                    var recv = await socket.ReceiveAsync(memory, cancellationToken);
                    if (recv.MessageType == WebSocketMessageType.Close)
                        break;

                    writer.Advance(recv.Count);

                    // push successfully read data
                    var result = await writer.FlushAsync(cancellationToken);
                    if (result.IsCompleted)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception e)
            {
                await writer.CompleteAsync(e);
            }
            finally
            {
                // socket is complete for other reasons
                await writer.CompleteAsync();
            }
        }

        /// <summary>
        /// Sends a frame to the server.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override ValueTask SendAsync(StompFrame frame, CancellationToken cancellationToken)
        {
            if (socket.State != WebSocketState.Open)
                throw new InvalidOperationException("Web socket is closed.");

            // write frame to array buffer
            var b = new ArrayBufferWriter<byte>(512);
            protocol.Write(b, frame);
            return socket.SendAsync(b.WrittenMemory, WebSocketMessageType.Binary, true, cancellationToken);
        }

        /// <summary>
        /// Closes the transport.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            return CloseAsync(cancellationToken, false);
        }

        /// <summary>
        /// Closes the transport.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="disposing"></param>
        /// <returns></returns>
        async ValueTask CloseAsync(CancellationToken cancellationToken, bool disposing = false)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "STOMP shutdown", cancellationToken);
            }
            catch (WebSocketException e)
            {
                logger.LogError(e, "Unexpected exception closing web socket");
            }
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public override async ValueTask DisposeAsync()
        {
            try
            {
                await CloseAsync(CancellationToken.None, true);
            }
            catch (WebSocketException)
            {
                // ignore, we tried to close
            }
        }

    }

}
