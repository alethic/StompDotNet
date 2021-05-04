using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace StompDotNet
{

    /// <summary>
    /// Implements a STOMP transport across a socket.
    /// </summary>
    public class StompSocketTransport : StompPipeTransport
    {

        readonly IPEndPoint endpoint;
        readonly Socket socket;
        readonly StompBinaryProtocol protocol;
        readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="socket"></param>
        /// <param name="protocol"></param>
        /// <param name="logger"></param>
        public StompSocketTransport(IPEndPoint endpoint, Socket socket, StompBinaryProtocol protocol, ILogger logger) :
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
                    if (socket.Connected == false)
                    {
                        await writer.CompleteAsync();
                        break;
                    }

                    var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, cancellationToken);
                    if (bytesRead == 0)
                        break;

                    writer.Advance(bytesRead);

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
                return;
            }

            // socket is complete for other reasons
            await writer.CompleteAsync();
        }

        /// <summary>
        /// Sends a frame to the server.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask SendAsync(StompFrame frame, CancellationToken cancellationToken)
        {
            if (socket.Connected == false)
                throw new InvalidOperationException("Socket is closed.");

            // write frame to array buffer
            var b = new ArrayBufferWriter<byte>(512);
            protocol.Write(b, frame);
            await socket.SendAsync(b.WrittenMemory, SocketFlags.None, cancellationToken);
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
        ValueTask CloseAsync(CancellationToken cancellationToken, bool disposing = false)
        {
            try
            {
                if (socket.Connected)
                    socket.Close();
            }
            catch (SocketException e)
            {
                logger.LogError(e, "Unexpected exception closing socket");
            }

            return ValueTask.CompletedTask;
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
            catch (SocketException)
            {
                // ignore, we tried to close
            }
        }

    }

}
