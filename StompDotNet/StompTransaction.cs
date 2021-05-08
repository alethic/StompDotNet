using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StompDotNet
{

    public class StompTransaction :
#if NETSTANDARD2_1
        IAsyncDisposable,
#endif
    IDisposable
    {

        readonly StompConnection connection;
        readonly string id;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="id"></param>
        public StompTransaction(StompConnection connection, string id)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>
        /// Gets the connection associated with the subscription.
        /// </summary>
        public StompConnection Connection => connection;

        /// <summary>
        /// Gets the ID of the subscription.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask CommitAsync(IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            await connection.CommitAsync(id, headers, cancellationToken);
        }

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask AbortAsync(IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            await connection.AbortAsync(id, headers, cancellationToken);
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public ValueTask DisposeAsync()
        {
            return AbortAsync(null, CancellationToken.None);
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public void Dispose()
        {
            Task.Run(() => DisposeAsync()).ConfigureAwait(false).GetAwaiter().GetResult();
        }

    }
}