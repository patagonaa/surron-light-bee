using SurronCommunication.Communication;
using SurronCommunication.Packet;
using SurronCommunication.Parameter;
using System.Buffers.Binary;
using System.Globalization;

namespace SurronCommunication.BatterySimulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var r = new Random();

            var dataToReturn = new Dictionary<byte, byte[]>
            {
                {BmsParameters.Unknown_7.Id, [0x05] },
                {BmsParameters.Temperatures.Id, HexUtils.HexToBytes("1515150016161600")},
                {BmsParameters.BatteryVoltage.Id, new byte[4]},
                {BmsParameters.BatteryCurrent.Id, new byte[4]},
                {BmsParameters.BatteryPercent.Id, HexUtils.HexToBytes("4B")},
                {BmsParameters.BmsStatus.Id, HexUtils.HexToBytes("20000000000000000000")},

                {BmsParameters.RtcTime.Id, new byte[6]}
            };
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (o, e) => { cts.Cancel(); e.Cancel = true; };

            var token = cts.Token;

            using var communicationHandler = SurronCommunicationHandler.FromSerialPort("COM8");
            try
            {
                var i = 0;
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    BinaryPrimitives.WriteInt32LittleEndian(dataToReturn[BmsParameters.BatteryVoltage.Id], r.Next(-50000, 67000));
                    BinaryPrimitives.WriteInt32LittleEndian(dataToReturn[BmsParameters.BatteryCurrent.Id], r.Next(-90000, 10000));
                    dataToReturn[BmsParameters.BatteryPercent.Id][0] = (byte)r.Next(0, 101);

                    var now = DateTime.UtcNow + new TimeSpan(8, 20, 44);
                    dataToReturn[BmsParameters.RtcTime.Id] = [(byte)(now.Year - 2000), (byte)now.Month, (byte)now.Day, (byte)now.Hour, (byte)now.Minute, (byte)now.Second];

                    try
                    {
                        var readResult = communicationHandler.ReceivePacket(Timeout.Infinite, token, out var packet)!;
                        if (readResult == SurronReadResult.Success &&
                            packet != null &&
                            packet.Command == SurronCmd.ReadRequest &&
                            packet.Address == BmsParameters.BmsAddress &&
                            dataToReturn.TryGetValue(packet.Parameter, out var responseData))
                        {
                            var responseBuffer = new byte[packet.DataLength];
                            Array.Copy(responseData, responseBuffer, Math.Min(packet.DataLength, responseData.Length));
                            var responsePacket = SurronDataPacket.Create(SurronCmd.ReadResponse, packet.Address, packet.Parameter, packet.DataLength, responseBuffer);
                            Console.WriteLine($"> {responsePacket}");
                            //if (i++ % 10 == 5)
                            //    communicationHandler._communication.Write(new byte[] { 0x10 }, CancellationToken.None);
                            communicationHandler.SendPacket(responsePacket, token);
                        }
                    }
                    catch (InvalidDataException)
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
