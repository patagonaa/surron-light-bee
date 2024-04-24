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

            var dataToReturn = new Dictionary<BmsParameterId, byte[]>
            {
                {BmsParameterId.Unknown_7, [0x05] },
                {BmsParameterId.Temperatures, HexUtils.HexToBytes("1515150016161600")},
                {BmsParameterId.BatteryVoltage, new byte[4]},
                {BmsParameterId.BatteryCurrent, new byte[4]},
                {BmsParameterId.BatteryPercent, HexUtils.HexToBytes("4B")},
                {BmsParameterId.BmsStatus, HexUtils.HexToBytes("20000000000000000000")},
                {BmsParameterId.RtcTime, new byte[6]}
            };
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (o, e) => { cts.Cancel(); e.Cancel = true; };

            var token = cts.Token;

            using var communicationHandler = SurronCommunicationHandler.FromSerialPort("COM5");
            try
            {
                var i = 0;
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    BinaryPrimitives.WriteInt32LittleEndian(dataToReturn[BmsParameterId.BatteryVoltage], r.Next(-50000, 67000));
                    BinaryPrimitives.WriteInt32LittleEndian(dataToReturn[BmsParameterId.BatteryCurrent], r.Next(-90000, 10000));
                    dataToReturn[BmsParameterId.BatteryPercent][0] = (byte)r.Next(0, 101);

                    var now = DateTime.UtcNow + new TimeSpan(8, 20, 54);
                    dataToReturn[BmsParameterId.RtcTime] = [(byte)(now.Year - 2000), (byte)now.Month, (byte)now.Day, (byte)now.Hour, (byte)now.Minute, (byte)now.Second];

                    try
                    {
                        var readResult = communicationHandler.ReceivePacket(Timeout.Infinite, token, out var packet)!;
                        if (readResult == SurronReadResult.Success &&
                            packet != null &&
                            packet.Command == SurronCmd.ReadRequest &&
                            packet.Address == BmsParameters.BmsAddress &&
                            dataToReturn.TryGetValue((BmsParameterId)packet.Parameter, out var responseData))
                        {
                            var responseBuffer = new byte[packet.DataLength];
                            Array.Copy(responseData, responseBuffer, Math.Min(packet.DataLength, responseData.Length));
                            var responsePacket = SurronDataPacket.Create(SurronCmd.ReadResponse, packet.Address, packet.Parameter, packet.DataLength, responseBuffer);
                            Console.WriteLine($"> {responsePacket}");
                            //if (i++ % 10 == 5)
                            //    communicationHandler.Communication.Write(new byte[] { 0x10 }, CancellationToken.None);
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
