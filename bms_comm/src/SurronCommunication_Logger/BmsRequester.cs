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
        private readonly ParameterDefinition[] _parametersToRead;

        public event ParameterUpdateEventHandler? ParameterUpdateEvent;

        public BmsRequester(ISurronCommunicationHandler bmsCommunicationHandler, ParameterDefinition[] parametersToRead)
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
                    var anyUpdated = false;
                    foreach (var parameterToRead in _parametersToRead)
                    {
                        var receivedData = _bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, parameterToRead.Id, parameterToRead.Length, CancellationToken.None);
                        if (receivedData == null)
                        {
                            Console.WriteLine($"BMS read error");
                        }
                        else
                        {
                            anyUpdated = true;
                            var currentValue = (byte[])currentValues[parameterToRead.Id];

                            if (currentValue == null)
                            {
                                currentValues[parameterToRead.Id] = receivedData;
                            }
                            else
                            {
                                Array.Copy(receivedData, currentValue, parameterToRead.Length);
                            }
                        }
                        Thread.Sleep(10);
                    }
                    if (anyUpdated)
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
