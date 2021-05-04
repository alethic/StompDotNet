using System;
using System.Collections.Generic;
using System.Linq;

namespace StompDotNet
{

    /// <summary>
    /// Describes a STOMP frame.
    /// </summary>
    public readonly struct StompFrame
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        public StompFrame(StompCommand command, IEnumerable<KeyValuePair<string, string>> headers, ReadOnlyMemory<byte> body)
        {
            Command = command;
            Headers = headers ?? Enumerable.Empty<KeyValuePair<string, string>>();
            Body = body;
        }

        /// <summary>
        /// Gets the command of the STOMP frame.
        /// </summary>
        public StompCommand Command { get; }

        /// <summary>
        /// Gets the headers which are part of the STOMP frame.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Headers { get; }

        /// <summary>
        /// Gets the first header with the specified value.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetHeaderValue(string key) => Headers.FirstOrDefault(i => i.Key == key).Value;

        /// <summary>
        /// Gets the body of the frame.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }

    }

}
