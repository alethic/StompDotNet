using System.Net.WebSockets;

namespace StompDotNet.Internal
{

    public readonly struct ValueWebSocketReceiveResult
    {

        public ValueWebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage)
        {
            Count = count;
            MessageType = messageType;
            EndOfMessage = endOfMessage;
        }

        public int Count { get; }

        public bool EndOfMessage { get; }

        public WebSocketMessageType MessageType { get; }

    }

}
