using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StompDotNet.Tests
{

    [TestClass]
    public class Class1
    {

        [TestMethod]
        public async Task Foo()
        {
            var cancellationToken = CancellationToken.None;
            var f = new StompWebSocketConnectionFactory(c => { c.Credentials = new NetworkCredential("admin", "admin"); }, NullLogger.Instance);
            var e = new UriEndPoint(new Uri("ws://localhost:44112/websockets/messaging/websocket"));
            var c = await f.OpenAsync(e, cancellationToken);
            await Task.Delay(TimeSpan.FromMinutes(1));
        }

    }

}
