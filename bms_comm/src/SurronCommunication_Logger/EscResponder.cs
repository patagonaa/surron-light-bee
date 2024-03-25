using SurronCommunication.Communication;
using SurronCommunication.Packet;
using SurronCommunication.Parameter;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class EscResponder
    {
        private static readonly TimeSpan _escResponseTimeout = TimeSpan.FromSeconds(10);
        private readonly SurronCommunicationHandler _escCommunicationHandler;
        private readonly byte[] _escReadParameters;
        private readonly Hashtable _currentValues = new Hashtable();
        private DateTime _lastUpdate = DateTime.MinValue;

        public EscResponder(SurronCommunicationHandler escCommunicationHandler, byte[] escReadParameters)
        {
            _escCommunicationHandler = escCommunicationHandler;
            _escReadParameters = escReadParameters;
        }

        public void Run()
        {
            while (true)
            {
                _escCommunicationHandler.ReceivePacket(1000, CancellationToken.None, out var packet);

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
            }
        }

        public void SetData(DateTime updateTime, Hashtable newData)
        {
            foreach (var requestedKey in _escReadParameters)
            {
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
