using System;
using System.Threading;

namespace StompDotNet
{

    /// <summary>
    /// Arguments passed when the connection state changes.
    /// </summary>
    public class StompConnectionStateChangeArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="exception"></param>
        public StompConnectionStateChangeArgs(StompConnectionState state, Exception exception, CancellationToken cancellationToken)
        {
            State = state;
            Exception = exception;
        }

        /// <summary>
        /// Gets the new state of the connection.
        /// </summary>
        public StompConnectionState State { get; set; }

        /// <summary>
        /// Gets the exception that caused the state change.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets the token that indicates the event has been canceled.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

    }

}