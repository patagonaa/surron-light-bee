using SurronCommunication.Packet;
using System;
using System.Threading;

namespace SurronCommunication.Communication
{
    public class DummySurronCommunicationHandler : ISurronCommunicationHandler
    {
        private readonly Random _random;

        public DummySurronCommunicationHandler()
        {
            _random = new Random();
        }

        public byte[] ReadRegister(ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken)
        {
            var bytes = new byte[paramLength];
            _random.NextBytes(bytes);
            return bytes;
        }

        public SurronReadResult ReceivePacket(int timeoutMillis, CancellationToken token, out SurronDataPacket? packet)
        {
            throw new NotSupportedException();
        }

        public void SendPacket(SurronDataPacket packet, CancellationToken token)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }
    }
}
