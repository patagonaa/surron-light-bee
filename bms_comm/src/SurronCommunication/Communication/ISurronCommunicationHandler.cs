using SurronCommunication.Packet;

namespace SurronCommunication.Communication
{
    public interface ISurronCommunicationHandler : IDisposable
    {
        Task<byte[]> ReadRegister(ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken);
        Task<SurronDataPacket> ReceivePacket(int timeoutMillis, CancellationToken token);
        Task SendPacket(SurronDataPacket packet, CancellationToken token);
    }
}
