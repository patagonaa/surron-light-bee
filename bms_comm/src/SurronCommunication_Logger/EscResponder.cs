using SurronCommunication.Communication;
using SurronCommunication.Packet;
using SurronCommunication.Parameter;
using SurronCommunication.Parameter.Logging;
using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class EscResponder
    {
        public event ParameterUpdateEventHandler? ParameterUpdateEvent;

        private static readonly TimeSpan _escResponseTimeout = TimeSpan.FromSeconds(10);
        private readonly SurronCommunicationHandler _escCommunicationHandler;
        private readonly BmsParameters.Parameters[] _escReadParameters;
        private readonly Hashtable _currentValues;
        private DateTime _lastUpdate = DateTime.MinValue;

        public EscResponder(SurronCommunicationHandler escCommunicationHandler, BmsParameters.Parameters[] escReadParameters)
        {
            _escCommunicationHandler = escCommunicationHandler;
            _escReadParameters = escReadParameters;
            _currentValues = new Hashtable(escReadParameters.Length);
        }

        public void Run()
        {
            var escStatus = new Hashtable();

            while (true)
            {
                var result = _escCommunicationHandler.ReceivePacket(Timeout.Infinite, CancellationToken.None, out var packet);

                switch (result)
                {
                    case SurronReadResult.Success:
                        {
                            if (packet != null &&
                                packet.Command == SurronCmd.ReadRequest &&
                                packet.Address == BmsParameters.BmsAddress)
                            {
                                var responseData = (byte[])_currentValues[packet.Parameter];

                                if (responseData == null || DateTime.UtcNow - _escResponseTimeout > _lastUpdate)
                                {
                                    Console.WriteLine($"Parameter {packet.Parameter} is missing/outdated");
                                    continue;
                                }

                                var responseBuffer = new byte[packet.DataLength];
                                // this copy here has two reasons:
                                // - to get the requested length regardless of the actual field length
                                // - to prevent a task switch during transmission to overwrite our data while we haven't completely read it.
                                Array.Copy(responseData, responseBuffer, Math.Min(packet.DataLength, responseData.Length));

                                var responsePacket = SurronDataPacket.Create(SurronCmd.ReadResponse, packet.Address, packet.Parameter, packet.DataLength, responseBuffer);
                                _escCommunicationHandler.SendPacket(responsePacket, CancellationToken.None);
                            }

                            if (packet != null &&
                                packet.Command == SurronCmd.Status &&
                                packet.Address == EscParameters.EscAddress)
                            {
                                escStatus[packet.Parameter] = packet.CommandData;
                                ParameterUpdateEvent?.Invoke(DateTime.UtcNow, LogCategory.Esc, escStatus);
                            }
                            continue;
                        }
                    case SurronReadResult.Timeout:
                        continue;
                    case SurronReadResult.InvalidData:
                        Console.WriteLine("ESC: Invalid Data");
                        continue;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public void SetBmsData(DateTime updateTime, LogCategory logCategory, Hashtable newData)
        {
            foreach (var readParameter in _escReadParameters)
            {
                var requestedKey = (byte)readParameter;
                var sourceArray = (byte[])newData[requestedKey];

                if (sourceArray != null)
                {
                    var targetArray = (byte[])_currentValues[requestedKey];
                    if (targetArray == null)
                    {
                        targetArray = new byte[sourceArray.Length];
                        _currentValues[requestedKey] = targetArray;
                    }

                    Debug.Assert(sourceArray.Length == targetArray.Length);
                    Array.Copy(sourceArray, targetArray, targetArray.Length);

                    _lastUpdate = updateTime;
                }
            }
        }
    }
}
