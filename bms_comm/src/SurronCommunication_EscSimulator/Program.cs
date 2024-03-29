using SurronCommunication;
using SurronCommunication.Communication;
using SurronCommunication.Parameter;

namespace SurronCommunication_EscSimulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (o, e) => { cts.Cancel(); e.Cancel = true; };

            var token = cts.Token;

            using var communicationHandler = SurronCommunicationHandler.FromSerialPort("COM11");

            var parametersToRead = new List<BmsParameters.Parameters>
            {
                BmsParameters.Parameters.Unknown_7,
                BmsParameters.Parameters.Temperatures,
                BmsParameters.Parameters.BatteryVoltage,
                BmsParameters.Parameters.BatteryPercent,
                BmsParameters.Parameters.BmsStatus,
            };

            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    foreach (var paramToRead in parametersToRead)
                    {
                        var response = communicationHandler.ReadRegister(BmsParameters.BmsAddress, (byte)paramToRead, BmsParameters.GetLength(paramToRead), token);
                        if (response != null)
                            Console.WriteLine($"{paramToRead,3}: {HexUtils.BytesToHex(response)}");
                        else
                            Console.WriteLine($"{paramToRead,3}: <Timeout>");
                        Thread.Sleep(100);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
