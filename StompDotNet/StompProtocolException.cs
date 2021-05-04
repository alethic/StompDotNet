using System;

namespace StompDotNet
{

    /// <summary>
    /// Represents errors that occurred in the Stomp protocol implementation.
    /// </summary>
    public class StompProtocolException : StompException
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public StompProtocolException()
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        public StompProtocolException(string message) : base(message)
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public StompProtocolException(string message, Exception innerException) : base(message, innerException)
        {

        }

    }

}