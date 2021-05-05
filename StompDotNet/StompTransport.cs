using System;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using StompDotNet.Internal;

namespace StompDotNet
{

    /// <summary>
    /// Manages the receiving and sending of STOMP frames.
    /// </summary>
    public abstract class StompTransport :
#if NETSTANDARD2_1
        IAsyncDisposable,
#endif
        IDisposable
    {

        readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="logger"></param>
        protected StompTransport(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the endpoint represented by the transport.
        /// </summary>
        public abstract EndPoint EndPoint { get; }

        /// <summary>
        /// Initiates the process of receiving froms from the STOMP transport.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract ValueTask ReceiveAsync(ChannelWriter<StompFrame> writer, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a single STOMP frame to the transport.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract ValueTask SendAsync(StompFrame frame, CancellationToken cancellationToken);

        /// <summary>
        /// Closes the underlying transport.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            return ValueTaskHelper.CompletedTask;
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public virtual ValueTask DisposeAsync()
        {
            return ValueTaskHelper.CompletedTask;
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public void Dispose()
        {
            Task.Run(() => DisposeAsync()).ConfigureAwait(false).GetAwaiter().GetResult();
        }

    }

}