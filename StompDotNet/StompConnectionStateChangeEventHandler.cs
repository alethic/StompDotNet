using System.Threading.Tasks;

namespace StompDotNet
{

    /// <summary>
    /// Describes a handler for a connection state changed event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public delegate ValueTask StompConnectionStateChangeEventHandler(object sender, StompConnectionStateChangeArgs args);

}