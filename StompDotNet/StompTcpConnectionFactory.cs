using System.Net.Sockets;

using Microsoft.Extensions.Logging;

namespace StompDotNet
{

    /// <summary>
    /// Provides <see cref="StompConnection"/> instances for TCP connections.
    /// </summary>
    public class StompTcpConnectionFactory : StompSocketConnectionFactory
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="logger"></param>
        public StompTcpConnectionFactory(StompConnectionOptions options, ILogger logger) : base(ProtocolType.Tcp, options, logger)
        {

        }

    }

}
