﻿using System;
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
            EnsureOpen();
            var position = 0;
            var length = buffer.Length;

            var waitMs = 10;
            var totalRetries = timeoutMillis == Timeout.Infinite ? int.MaxValue : timeoutMillis / waitMs;
            var retries = 0;
            var bufferArray = new byte[length];
            while (position < length)
            {
                token.ThrowIfCancellationRequested();
                var remainingBytes = length - position;

                if (_sp.BytesToRead >= remainingBytes)
                {
                    position += _sp.Read(bufferArray, position, remainingBytes);
                }
                else
                {
                    retries++;
                    if (retries > totalRetries)
                    {
                        return false;
                    }

                    Thread.Sleep(waitMs);
                }
            }
            bufferArray.AsSpan(0, buffer.Length).CopyTo(buffer);
            return true;
        }
    }
}
