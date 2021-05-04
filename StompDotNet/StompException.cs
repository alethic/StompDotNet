using System;

namespace StompDotNet
{

    /// <summary>
    /// Represents errors that occurred in the Stomp implementation.
    /// </summary>
    public class StompException : Exception
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public StompException()
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        public StompException(string message) : base(message)
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public StompException(string message, Exception innerException) : base(message, innerException)
        {

        }

    }

}