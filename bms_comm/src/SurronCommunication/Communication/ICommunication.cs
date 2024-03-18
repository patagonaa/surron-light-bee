using System;
using System.Threading;
#if NANOFRAMEWORK_1_0
using System.Buffers.Binary;
using ReadOnlySpanByte = System.SpanByte;
#else
using ReadOnlySpanByte = System.ReadOnlySpan<byte>;
using SpanByte = System.Span<byte>;
#endif

namespace SurronCommunication.Communication
{
    internal interface ICommunication : IDisposable
    {
        /// <summary>
        /// Write the supplied bytes to the device.
        /// </summary>
        /// <exception cref="OperationCanceledException">Operation was canceled</exception>
        void Write(ReadOnlySpanByte bytes, CancellationToken token);
        /// <summary>
        /// Read enough data to fill the supplied buffer completely.
        /// </summary>
        /// <returns>true, if the number of bytes could be read. false, if the timeout has expired.</returns>
        /// <exception cref="OperationCanceledException">Operation was canceled</exception>
        bool ReadExactly(SpanByte buffer, int timeoutMillis, CancellationToken token);

        /// <summary>
        /// Discard the input / read buffer.
        /// </summary>
        /// <exception cref="OperationCanceledException">Operation was canceled</exception>
        void DiscardInBuffer(CancellationToken token);
    }
}
