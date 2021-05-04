using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace StompDotNet
{

    /// <summary>
    /// Represents a subscription established on the STOMP server.
    /// </summary>
    public class StompMessageSubscription : IAsyncDisposable
    {

        readonly StompConnection connection;
        readonly string id;
        readonly ChannelReader<StompFrame> reader;
        readonly Func<Exception, ValueTask> completeAsync;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="id"></param>
        /// <param name="reader"></param>
        /// <param name="completeAsync"></param>
        public StompMessageSubscription(StompConnection connection, string id, ChannelReader<StompFrame> reader, Func<Exception, ValueTask> completeAsync)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.id = id ?? throw new ArgumentNullException(nameof(id));
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.completeAsync = completeAsync ?? throw new ArgumentNullException(nameof(completeAsync));
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
        /// Returns a <see cref="ValueTask"/> that will complete when a message is available to read.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            return reader.WaitToReadAsync(cancellationToken);
        }

        /// <summary>
        /// Asychronously reads an item from the subscription.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask<StompFrame> ReadAsync(CancellationToken cancellationToken = default)
        {
            return reader.ReadAsync(cancellationToken);
        }

        /// <summary>
        /// Unsubscribes from the subscription.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask UnsubscribeAsync(IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            await connection.UnsubscribeAsync(this, headers, cancellationToken);
        }

        /// <summary>
        /// Marks the subscription as complete.
        /// </summary>
        /// <returns></returns>
        internal ValueTask CompleteAsync(Exception exception) => completeAsync(exception);

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        /// <returns></returns>
        public ValueTask DisposeAsync()
        {
            return UnsubscribeAsync(null, CancellationToken.None);
        }

    }

}
