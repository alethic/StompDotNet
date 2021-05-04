using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace StompDotNet.Sample.Console
{

    public class SampleService : IHostedService
    {

        StompConnection connection;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var f = new StompWebSocketConnectionFactory(c => { c.Credentials = new NetworkCredential("admin", "admin"); }, NullLogger.Instance);
            var e = new UriEndPoint(new Uri("ws://localhost:44112/websockets/messaging/websocket"));
            connection = await f.OpenAsync(e, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await connection.CloseAsync(cancellationToken);
            await connection.DisposeAsync();
        }

    }

}