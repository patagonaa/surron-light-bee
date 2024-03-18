using System.Buffers;
using System.IO.Ports;
using System.Threading;

namespace SurronCommunication.Communication
{
    internal class SerialCommunication : ICommunication
    {
        private readonly SerialPort _sp;

        public SerialCommunication(string serialPort)
        {
            _sp = new SerialPort(serialPort)
            {
                BaudRate = 9600
            };
        }

        private void EnsureOpen()
        {
            if (!_sp.IsOpen)
            {
                _sp.Open();
            }
        }

        public void Dispose()
        {
            _sp.Dispose();
        }

        public Task DiscardInBuffer(CancellationToken token)
        {
            EnsureOpen();
            _sp.DiscardInBuffer();
            return Task.CompletedTask;
        }

        public Task Write(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            EnsureOpen();

            var buffer = ArrayPool<byte>.Shared.Rent(bytes.Length);
            try
            {
                bytes.CopyTo(buffer);
                _sp.Write(buffer, 0, bytes.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            return Task.CompletedTask;
        }

        public Task ReadExactly(Memory<byte> buffer, int timeoutMillis, CancellationToken token)
        {
            EnsureOpen();
            _sp.ReadTimeout = timeoutMillis;
            var position = 0;
            var length = buffer.Length;

            var bufferArray = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                while (position < length)
                {
                    token.ThrowIfCancellationRequested();
                    var remainingBytes = length - position;
                    position += _sp.Read(bufferArray, position, remainingBytes);
                }
                bufferArray.AsMemory(0, buffer.Length).CopyTo(buffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bufferArray);
            }
            return Task.CompletedTask;
        }
    }
}
