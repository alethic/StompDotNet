﻿using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace StompDotNet
{

    /// <summary>
    /// Manages the receiving and sending of STOMP frames.
    /// </summary>
    public abstract class StompTransport : IAsyncDisposable
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
        /// <param name="onReceiveAsync"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract ValueTask ReceiveAsync(Func<StompFrame, CancellationToken, ValueTask> onReceiveAsync, CancellationToken cancellationToken);

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
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public virtual ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

    }

}