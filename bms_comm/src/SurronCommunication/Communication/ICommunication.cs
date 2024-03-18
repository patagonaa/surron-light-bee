using System;
using System.Threading;
using System.Threading.Tasks;

namespace SurronCommunication.Communication
{
    internal interface ICommunication : IDisposable
    {
        /// <summary>
        /// Write the supplied bytes to the device.
        /// </summary>
        /// <exception cref="OperationCanceledException">Operation was canceled</exception>
        Task Write(ReadOnlyMemory<byte> bytes, CancellationToken token);
        /// <summary>
        /// Read enough data to fill the supplied buffer completely.
        /// </summary>
        /// <exception cref="TimeoutException">Timeout has expired</exception>
        /// <exception cref="OperationCanceledException">Operation was canceled</exception>
        Task ReadExactly(Memory<byte> buffer, int timeoutMillis, CancellationToken token);

        /// <summary>
        /// Discard the input / read buffer.
        /// </summary>
        /// <exception cref="OperationCanceledException">Operation was canceled</exception>
        Task DiscardInBuffer(CancellationToken token);
    }
}
