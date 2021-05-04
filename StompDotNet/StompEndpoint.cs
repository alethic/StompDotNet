using System.Threading;
using System.Threading.Tasks;

namespace StompDotNet
{

    /// <summary>
    /// Represents a way to connect to a STOMP server.
    /// </summary>
    public abstract class StompEndpoint
    {

        /// <summary>
        /// Opens a transport to the endpoint.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract ValueTask<StompTransport> ConnectAsync(CancellationToken cancellationToken);

    }

}