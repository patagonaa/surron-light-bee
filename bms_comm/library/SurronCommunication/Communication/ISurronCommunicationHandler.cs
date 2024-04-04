using SurronCommunication.Packet;
using System;
using System.Threading;

namespace SurronCommunication.Communication
{
    public interface ISurronCommunicationHandler : IDisposable
    {
        /// <returns>the requested register's data. null on timeout</returns>
        byte[]? ReadRegister(ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken);

        /// <returns>The received data packet. null on timeout.</returns>
        SurronReadResult ReceivePacket(int timeoutMillis, CancellationToken token, out SurronDataPacket? packet);
        void SendPacket(SurronDataPacket packet, CancellationToken token);
    }
}
