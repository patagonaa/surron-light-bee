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

            var dataToReturn = new Dictionary<BmsParameters.Parameters, byte[]>
            {
                {BmsParameters.Parameters.Unknown_7, [0x05] },
                {BmsParameters.Parameters.Temperatures, HexUtils.HexToBytes("1515150016161600")},
                {BmsParameters.Parameters.BatteryVoltage, new byte[4]},
                {BmsParameters.Parameters.BatteryCurrent, new byte[4]},
                {BmsParameters.Parameters.BatteryPercent, HexUtils.HexToBytes("4B")},
                {BmsParameters.Parameters.BmsStatus, HexUtils.HexToBytes("20000000000000000000")},
                {BmsParameters.Parameters.RtcTime, new byte[6]}
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

                    BinaryPrimitives.WriteInt32LittleEndian(dataToReturn[BmsParameters.Parameters.BatteryVoltage], r.Next(-50000, 67000));
                    BinaryPrimitives.WriteInt32LittleEndian(dataToReturn[BmsParameters.Parameters.BatteryCurrent], r.Next(-90000, 10000));
                    dataToReturn[BmsParameters.Parameters.BatteryPercent][0] = (byte)r.Next(0, 101);

                    var now = DateTime.UtcNow + new TimeSpan(8, 20, 54);
                    dataToReturn[BmsParameters.Parameters.RtcTime] = [(byte)(now.Year - 2000), (byte)now.Month, (byte)now.Day, (byte)now.Hour, (byte)now.Minute, (byte)now.Second];

                    try
                    {
                        var readResult = communicationHandler.ReceivePacket(Timeout.Infinite, token, out var packet)!;
                        if (readResult == SurronReadResult.Success &&
                            packet != null &&
                            packet.Command == SurronCmd.ReadRequest &&
                            packet.Address == BmsParameters.BmsAddress &&
                            dataToReturn.TryGetValue((BmsParameters.Parameters)packet.Parameter, out var responseData))
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
