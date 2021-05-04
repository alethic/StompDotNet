namespace StompDotNet
{

    /// <summary>
    /// Describes an exception caused by a STOMP frame.
    /// </summary>
    public class StompFrameException : StompException
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public StompFrameException(StompFrame frame) : base()
        {
            Frame = frame;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public StompFrameException(StompFrame frame, string message) : base(message)
        {
            Frame = frame;
        }

        /// <summary>
        /// Gets the frame that caused the exception.
        /// </summary>
        public StompFrame Frame { get; }

    }

}
