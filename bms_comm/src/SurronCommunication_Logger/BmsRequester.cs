using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class BmsRequester
    {
        private readonly ISurronCommunicationHandler _bmsCommunicationHandler;
        private readonly BmsParameters.Parameters[] _parametersToRead;

        public event ParameterUpdateEventHandler? ParameterUpdateEvent;

        public BmsRequester(ISurronCommunicationHandler bmsCommunicationHandler, BmsParameters.Parameters[] parametersToRead)
        {
            _bmsCommunicationHandler = bmsCommunicationHandler;
            _parametersToRead = parametersToRead;
        }

        public void Run()
        {
            var currentValues = new Hashtable(_parametersToRead.Length);

            while (true)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var anyReceived = false;
                    foreach (var parameterToRead in _parametersToRead)
                    {
                        var paramLength = BmsParameters.GetLength(parameterToRead);
                        var receivedData = _bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, (byte)parameterToRead, paramLength, CancellationToken.None);
                        if (receivedData == null)
                        {
                            Console.WriteLine($"BMS read error");
                        }
                        else
                        {
                            anyReceived = true;
                            var currentValue = (byte[])currentValues[(byte)parameterToRead];

                            if (currentValue == null)
                            {
                                currentValues[(byte)parameterToRead] = receivedData;
                            }
                            else
                            {
                                Array.Copy(receivedData, currentValue, paramLength);
                            }
                        }
                        Thread.Sleep(10);
                    }
                    if (anyReceived)
                        ParameterUpdateEvent?.Invoke(now, BmsParameters.BmsAddress, currentValues);
                }
                catch (InvalidDataException ex)
                {
                    Debug.WriteLine($"Invalid: {ex.Message}");
                }

                Thread.Sleep(100);
            }
        }
    }
}
