using System;
using System.Buffers;

namespace StompDotNet.Internal
{

    public static class SequenceReaderExtensions
    {

#if NETSTANDARD2_0

        public static bool TryReadTo<T>(this SequenceReader<T> reader, out ReadOnlySequence<T> sequence, T delimiter, bool advancePastDelimiter = true)
            where T : unmanaged, IEquatable<T>
        {
            throw new NotImplementedException();
        }

        public static bool TryReadTo<T>(this SequenceReader<T> reader, out ReadOnlySpan<T> span, T delimiter, bool advancePastDelimiter = true)
            where T : unmanaged, IEquatable<T>
        {
            throw new NotImplementedException();
        }

#endif

    }

}
