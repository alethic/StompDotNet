using System;
using System.Buffers;
using System.Text;

namespace StompDotNet.Internal
{

    public static class EncodingExtensions
    {

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP3_1

        public static string GetString(this Encoding encoding, ReadOnlySpan<byte> chars)
        {
            return encoding.GetString(chars.ToArray());
        }

        public static string GetString(this Encoding encoding, in ReadOnlySequence<byte> chars)
        {
            return encoding.GetString(chars.ToArray());
        }

        public static void GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, IBufferWriter<byte> writer)
        {
            writer.Write(encoding.GetBytes(chars.ToArray()));
        }

#endif

    }

}
