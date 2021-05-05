using System.Text;

using StompDotNet.Internal;

namespace StompDotNet
{

    /// <summary>
    /// Describes an exception caused by a STOMP ERROR frame.
    /// </summary>
    public class StompErrorFrameException : StompFrameException
    {

        /// <summary>
        /// Extracts the message from the error frame.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        static string GetErrorMessage(StompFrame frame)
        {
            return frame.GetHeaderValue("message") ?? GetErrorBody(frame);
        }

        /// <summary>
        /// Gets the error message in the body of the frame.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        static string GetErrorBody(StompFrame frame)
        {
            if (frame.Body.IsEmpty)
                return null;

            return Encoding.UTF8.GetString(frame.Body.Span);
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public StompErrorFrameException(StompFrame frame) : base(frame, GetErrorMessage(frame))
        {

        }

        /// <summary>
        /// Gets the error body text, if available.
        /// </summary>
        public string ErrorBody => GetErrorBody(Frame);

    }

}
