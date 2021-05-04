namespace StompDotNet
{

    /// <summary>
    /// Describes the various options of the STOMP connection.
    /// </summary>
    public class StompConnectionOptions
    {

        /// <summary>
        /// Gets the maximum acceptable version of the STOMP protocol to support.
        /// </summary>
        public StompVersion MaximumVersion { get; set; } = StompVersion.Stomp_1_2;

    }

}