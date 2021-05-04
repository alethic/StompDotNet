using System;

namespace StompDotNet
{

    /// <summary>
    /// Represents errors that occurred in the Stomp implementation.
    /// </summary>
    public class StompConnectionAbortedException : StompException
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public StompConnectionAbortedException()
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        public StompConnectionAbortedException(string message) : base(message)
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public StompConnectionAbortedException(string message, Exception innerException) : base(message, innerException)
        {

        }

    }

}