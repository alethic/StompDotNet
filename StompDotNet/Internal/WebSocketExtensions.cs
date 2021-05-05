using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace StompDotNet.Internal
{

    public static class WebSocketExtensions
    {

#if NETSTANDARD2_0

        public static ValueTask SendAsync(this WebSocket socket, ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return new ValueTask(socket.SendAsync(new ArraySegment<byte>(buffer.ToArray()), messageType, endOfMessage, cancellationToken));
        }
        
        public static async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(this WebSocket socket, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer.ToArray()), cancellationToken);
            return new ValueWebSocketReceiveResult(result.Count, result.MessageType, result.EndOfMessage);
        }

#endif

    }

}
