﻿namespace System.Buffers.Binary
{
    public static class SpanExtensions
    {
        public static void CopyTo(this byte[] bytes, SpanByte span)
        {
            new SpanByte(bytes).CopyTo(span);
        }
        public static SpanByte AsSpan(this byte[] bytes, int index, int count = -1)
        {
            return new SpanByte(bytes, index, count == -1 ? bytes.Length - index : count);
        }
    }
}
