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

        public SurronDataPacket ReceivePacket(int timeoutMillis, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public void SendPacket(SurronDataPacket packet, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}
