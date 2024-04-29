using System;
using System.IO.Ports;
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
    internal class SerialCommunication : ICommunication
    {
        private readonly SerialPort _sp;
        private readonly AutoResetEvent _dataReceivedEvent = new AutoResetEvent(false);

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
                _sp.DataReceived += OnDataReceived;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _dataReceivedEvent.Set();
        }

        public void Dispose()
        {
            _sp.Dispose();
        }

        public void DiscardInBuffer(CancellationToken token)
        {
            EnsureOpen();

#if NANOFRAMEWORK_1_0
            var available = _sp.BytesToRead;
            var tmpBuf = new byte[available];
            _sp.Read(tmpBuf, 0, available);
#else
            _sp.DiscardInBuffer();
#endif
        }

        public void Write(ReadOnlySpanByte bytes, CancellationToken token)
        {
            EnsureOpen();

            var array = bytes.ToArray();
            _sp.Write(array, 0, array.Length);
        }

        public bool ReadExactly(SpanByte buffer, int timeoutMillis, CancellationToken token)
        {
            var cancelWaitHandle = token.WaitHandle;
            var waitHandles = new[] { cancelWaitHandle, _dataReceivedEvent };

            EnsureOpen();
            var position = 0;
            var length = buffer.Length;

            var bufferArray = new byte[length];
            while (position < length)
            {
                token.ThrowIfCancellationRequested();
                var remainingBytes = length - position;

                var rxBufferBytes = _sp.BytesToRead;
                if (rxBufferBytes > 0)
                {
                    position += _sp.Read(bufferArray, position, remainingBytes < rxBufferBytes ? remainingBytes : rxBufferBytes);
                }
                else
                {
                    var handleIndex = WaitHandle.WaitAny(waitHandles, timeoutMillis, false);
                    // return value tells us if we got data or were cancelled, but we check cancellation on the top of this loop anyway.
                    if (handleIndex == WaitHandle.WaitTimeout)
                    {
                        return false;
                    }
                }
            }
            bufferArray.AsSpan(0, position).CopyTo(buffer);
            return true;
        }
    }
}
