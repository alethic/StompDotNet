
using System;

using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace StompDotNet
{

    /// <summary>
    /// Represents a two-way exchange of STOMP messages across some underlying transport.
    /// </summary>
    public class StompClient
    {

        readonly StompTcpConnectionFactory connectionFactory;
        readonly StompBinaryProtocol protocol;
        readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="connectionFactory"></param>
        /// <param name="protocol"></param>
        /// <param name="loggerFactory"></param>
        public StompClient(StompTcpConnectionFactory connectionFactory, StompBinaryProtocol protocol, ILoggerFactory loggerFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

    }

}
