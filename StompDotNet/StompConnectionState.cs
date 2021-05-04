namespace StompDotNet
{

    /// <summary>
    /// Describes the state of a <see cref="StompConnection"/>.
    /// </summary>
    public enum StompConnectionState
    {

        Closed,
        Opening,
        Opened,
        Closing,
        Aborted,

    }

}
