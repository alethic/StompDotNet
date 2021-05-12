using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StompDotNet
{

    /// <summary>
    /// Represents a STOMP MESSAGE received.
    /// </summary>
    public class StompMessage
    {

        readonly StompSubscription subscription;
        readonly IReadOnlyList<KeyValuePair<string, string>> headers;
        readonly ReadOnlyMemory<byte> body;
        readonly CancellationToken cancellationToken;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="subscription"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        public StompMessage(StompSubscription subscription, IReadOnlyList<KeyValuePair<string, string>> headers, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            this.subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));
            this.headers = headers ?? new List<KeyValuePair<string, string>>();
            this.body = body;
            this.cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the subscription that originated the message.
        /// </summary>
        public StompSubscription Subscription => subscription;

        /// <summary>
        /// Gets the first header value with the specified key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetHeaderValue(string key) => headers.FirstOrDefault(i => i.Key == key).Value;

        /// <summary>
        /// Gets the ID of the message.
        /// </summary>
        public string Id => GetHeaderValue("message-id");

        /// <summary>
        /// Gets the content-type of the message.
        /// </summary>
        public string ContentType => GetHeaderValue("content-type");

        /// <summary>
        /// Gets the headers of the message.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Headers => headers;

        /// <summary>
        /// Gets the body of the message.
        /// </summary>
        public ReadOnlyMemory<byte> Body => body;

        /// <summary>
        /// Cancellation token that is signaled when the message is no longer valid.
        /// </summary>
        public CancellationToken CancellationToken => cancellationToken;

        /// <summary>
        /// Sends an 'ACK' command for the current message.
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask AckAsync(StompTransaction transaction = null, IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            return subscription.Connection.AckAsync(Id, transaction, headers, CancellationTokenSource.CreateLinkedTokenSource(this.cancellationToken, cancellationToken).Token);
        }

        /// <summary>
        /// Sends an 'ACK' command for the current message.
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public ValueTask NackAsync(StompTransaction transaction = null, IEnumerable<KeyValuePair<string, string>> headers = null, CancellationToken cancellationToken = default)
        {
            return subscription.Connection.NackAsync(Id, transaction, headers, CancellationTokenSource.CreateLinkedTokenSource(this.cancellationToken, cancellationToken).Token);
        }

    }

}
