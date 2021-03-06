using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace StompDotNet.Internal
{

    public static class SocketExtensions
    {

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP3_1

        public static Task ConnectAsync(this Socket socket, EndPoint remoteEP, CancellationToken cancellationToken)
        {
            using var r = cancellationToken.Register(socket.Close);

            try
            {
                return socket.ConnectAsync(remoteEP);
            }
            catch (NullReferenceException)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (SocketException)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw new InvalidOperationException();
        }

#endif

#if NETSTANDARD2_0

        public static ValueTask<int> ReceiveAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        
        public static ValueTask<int> SendAsync(this Socket socket, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

#endif

    }

}
