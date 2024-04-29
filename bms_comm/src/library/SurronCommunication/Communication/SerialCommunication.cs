using System;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;

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
        private readonly AutoResetEvent _dataReceivedEvent = new AutoResetEvent(false);
        private readonly string _serialPort;

        private SerialPort? _sp;

        public SerialCommunication(string serialPort)
        {
            _serialPort = serialPort;
            GetOpenSerialPort(); // open serial port to immediately throw exceptions and init UART
        }

        private SerialPort GetOpenSerialPort()
        {
            if (_sp == null || !_sp.IsOpen)
            {
                _sp = new SerialPort(_serialPort)
                {
                    BaudRate = 9600,
#if NANOFRAMEWORK_1_0
                    Mode = SerialMode.RS485 // only required when using RTS as RX/TX switch
#endif
                };

                _sp.Open();
                _sp.DataReceived += OnDataReceived;
            }
            return _sp;
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _dataReceivedEvent.Set();
        }

        public void Dispose()
        {
            _sp?.Dispose();
        }

        public void DiscardInBuffer(CancellationToken token)
        {
            var sp = GetOpenSerialPort();

#if NANOFRAMEWORK_1_0
            var available = sp.BytesToRead;
            var tmpBuf = new byte[available];
            sp.Read(tmpBuf, 0, available);
            if (available > 0)
                Debug.WriteLine($"Discarded data: {HexUtils.BytesToHex(tmpBuf)}");
#else
            sp.DiscardInBuffer();
#endif
        }

        public void Write(ReadOnlySpanByte bytes, CancellationToken token)
        {
            var sp = GetOpenSerialPort();

            var array = bytes.ToArray();
            sp.Write(array, 0, array.Length);
        }

        public bool ReadExactly(SpanByte buffer, int timeoutMillis, CancellationToken token)
        {
            var cancelWaitHandle = token.WaitHandle;
            var waitHandles = new[] { cancelWaitHandle, _dataReceivedEvent };

            var sp = GetOpenSerialPort();
            var position = 0;
            var length = buffer.Length;

            var bufferArray = new byte[length];
            while (position < length)
            {
                token.ThrowIfCancellationRequested();
                var remainingBytes = length - position;

                var rxBufferBytes = sp.BytesToRead;
                if (rxBufferBytes > 0)
                {
                    position += sp.Read(bufferArray, position, remainingBytes < rxBufferBytes ? remainingBytes : rxBufferBytes);
                }
                else
                {
                    var handleIndex = WaitHandle.WaitAny(waitHandles, timeoutMillis, false);
                    // return value tells us if we got data or were cancelled, but we check cancellation on the top of this loop anyway.
                    if (handleIndex == WaitHandle.WaitTimeout)
                    {
                        Debug.WriteLine($"Timeout buffer data: {HexUtils.BytesToHex(bufferArray.AsSpan(0, position).ToArray())}");
                        return false;
                    }
                }
            }
            bufferArray.AsSpan(0, position).CopyTo(buffer);
            return true;
        }

        public void Reset()
        {
#if NANOFRAMEWORK_1_0
            // this is a hack because on ESP32 (I couldn't reproduce this on ESP32-S3 or desktop),
            // somehow the UART desyncs (always missing the first byte) and doesn't resync until
            // several minutes. Reconnecting the RS485 lines or standby cycling the BMS doesn't help,
            // only restarting the ESP32 (or reinitializing the UART) seems to help.
            Console.WriteLine("Resetting UART");
            _sp?.Dispose();
            _sp = null;
#endif
        }
    }
}
