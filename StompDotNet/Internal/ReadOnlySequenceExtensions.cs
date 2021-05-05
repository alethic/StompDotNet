using System;
using System.Buffers;

namespace StompDotNet.Internal
{

    public static class ReadOnlySequenceExtensions
    {

#if NETSTANDARD2_0

        public static void GetFirstSpan<T>(this ReadOnlySequence<T> sequence, out ReadOnlySpan<T> first, out SequencePosition position)
        {
            throw new NotImplementedException();
        }

#endif

    }

}
