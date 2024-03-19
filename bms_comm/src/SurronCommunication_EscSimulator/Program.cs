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

            var parametersToRead = new List<ParameterDefinition>
            {
                BmsParameters.Unknown_7,
                BmsParameters.Temperatures,
                BmsParameters.BatteryVoltage,
                BmsParameters.BatteryPercent,
                BmsParameters.BmsStatus,
            };

            try
            {
                while (true)
                {   
                    token.ThrowIfCancellationRequested();

                    foreach (var paramToRead in parametersToRead)
                    {
                        var response = communicationHandler.ReadRegister(BmsParameters.BmsAddress, paramToRead.Id, paramToRead.Length, token);
                        if (response != null)
                            Console.WriteLine($"{paramToRead.Id,3}: {HexUtils.BytesToHex(response)}");
                        else
                            Console.WriteLine($"{paramToRead.Id,3}: <Timeout>");
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
