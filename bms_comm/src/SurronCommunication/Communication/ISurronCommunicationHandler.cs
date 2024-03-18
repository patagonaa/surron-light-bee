using SurronCommunication.Packet;
using System;
using System.Threading;

namespace SurronCommunication.Communication
{
    public interface ISurronCommunicationHandler : IDisposable
    {
        byte[] ReadRegister(ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken);
        SurronDataPacket ReceivePacket(int timeoutMillis, CancellationToken token);
        void SendPacket(SurronDataPacket packet, CancellationToken token);
    }
}
