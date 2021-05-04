using System;

namespace StompDotNet
{

    /// <summary>
    /// Raised when the underlying transport has failed.
    /// </summary>
    public class StompTransportException : StompException
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public StompTransportException()
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        public StompTransportException(string message) : base(message)
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public StompTransportException(string message, Exception innerException) : base(message, innerException)
        {

        }

    }

}
