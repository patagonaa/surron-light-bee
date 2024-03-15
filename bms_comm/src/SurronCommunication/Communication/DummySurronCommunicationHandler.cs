﻿using SurronCommunication.Packet;

namespace SurronCommunication.Communication
{
    public class DummySurronCommunicationHandler : ISurronCommunicationHandler
    {
        private readonly Random _random;

        public DummySurronCommunicationHandler()
        {
            _random = new Random();
        }

        public Task<byte[]> ReadRegister(ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken)
        {
            var bytes = new byte[paramLength];
            _random.NextBytes(bytes);
            return Task.FromResult(bytes);
        }

        public Task<SurronDataPacket> ReceivePacket(int timeoutMillis, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task SendPacket(SurronDataPacket packet, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
