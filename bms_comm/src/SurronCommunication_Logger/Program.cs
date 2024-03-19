using nanoFramework.Hardware.Esp32;
using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Device.Gpio;
using System.Collections;
using SurronCommunication;
using SurronCommunication.Packet;
using nanoFramework.Runtime.Native;

namespace SurronCommunication_Logger
{
    public class Program
    {
        private static readonly TimeSpan _utcBmsTimeOffset = new TimeSpan(8, 20, 44).Negate();
        private static readonly TimeSpan _escResponseTimeout = TimeSpan.FromSeconds(10);

        private static event ParameterUpdate? ParameterUpdateEvent;
        private delegate void ParameterUpdate(DateTime dateTime, Hashtable currentParameters);

        public static void Main()
        {
            Configuration.SetPinFunction(16, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(17, DeviceFunction.COM2_TX);

            Configuration.SetPinFunction(18, DeviceFunction.COM3_RX);
            Configuration.SetPinFunction(19, DeviceFunction.COM3_TX);

            var bmsCommunicationHandler = SurronCommunicationHandler.FromSerialPort("COM3");

            while (true)
            {
                Debug.WriteLine("Trying to read BMS RTC...");
                var response = bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, BmsParameters.RtcTime.Id, BmsParameters.RtcTime.Length, CancellationToken.None);
                if (response != null)
                {
                    var rtcTime = new DateTime(2000 + response[0], response[1], response[2], response[3], response[4], response[5]) + _utcBmsTimeOffset;
                    Debug.WriteLine($"Got RTC Time: {rtcTime}");
                    Rtc.SetSystemTime(rtcTime);
                    Debug.WriteLine($"Current Time: {DateTime.UtcNow}");
                    break;
                }
            }

            var bmsReadThread = new Thread(() => ReadBmsValuesThread(bmsCommunicationHandler));
            bmsReadThread.Start();

            var escRespondThread = new Thread(RespondToEscThread);
            escRespondThread.Start();

            Thread.Sleep(Timeout.Infinite);
        }

        private static void ReadBmsValuesThread(ISurronCommunicationHandler handler)
        {
            var gpioController = new GpioController();
            var ledPin = gpioController.OpenPin(2, PinMode.Output);

            var parametersToRead = new ParameterDefinition[]
            {
                // read by esc
                BmsParameters.Unknown_7,
                BmsParameters.Temperatures,
                BmsParameters.BatteryVoltage,
                BmsParameters.BatteryPercent,
                BmsParameters.BmsStatus,

                // other stuff
                BmsParameters.BatteryCurrent,
                //BmsParameters.RemainingCapacity,
                //BmsParameters.TotalCapacity,
                //BmsParameters.ChargeCycles,
                //BmsParameters.Statistics,
                //BmsParameters.History,
            };

            var currentValues = new Hashtable();

            while (true)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var anyUpdated = false;
                    foreach (var parameterToRead in parametersToRead)
                    {
                        var receivedData = handler.ReadRegister(BmsParameters.BmsAddress, parameterToRead.Id, parameterToRead.Length, CancellationToken.None);
                        if (receivedData == null)
                        {
                            Debug.WriteLine($"Timeout");
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
                    }
                    if (anyUpdated)
                        ParameterUpdateEvent?.Invoke(now, currentValues);
                }
                catch (InvalidDataException ex)
                {
                    Debug.WriteLine($"Invalid: {ex.Message}");
                }

                ledPin.Write(PinValue.Low);
                Thread.Sleep(1000);
                ledPin.Write(PinValue.High);
            }
        }

        private static bool SequenceEqual(byte[] arr1, byte[] arr2)
        {
            if (arr1.Length != arr2.Length)
                return false;

            for (int i = 0; i < arr1.Length; i++)
            {
                if (arr1[i] != arr2[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void RespondToEscThread()
        {
            var dataToReturn = new Hashtable
            {
                { BmsParameters.Unknown_7.Id, new byte[]{ 0x05 } },
                { BmsParameters.Temperatures.Id, HexUtils.HexToBytes("1515150016161600") },
                { BmsParameters.BatteryVoltage.Id, BitConverter.GetBytes(60000) },
                { BmsParameters.BatteryPercent.Id, HexUtils.HexToBytes("4B") },
                { BmsParameters.BmsStatus.Id, HexUtils.HexToBytes("20000000000000000000") }
            };

            DateTime lastUpdate = DateTime.MinValue;

            ParameterUpdateEvent += (dateTime, changedParameters) =>
            {
                foreach (var requestedKey in dataToReturn.Keys)
                {
                    var sourceArray = (byte[])changedParameters[requestedKey];

                    if (sourceArray != null)
                    {
                        var targetArray = (byte[])dataToReturn[requestedKey];

                        Array.Copy(sourceArray, targetArray, Math.Min(sourceArray.Length, targetArray.Length));

                        lastUpdate = DateTime.UtcNow;
                    }
                }
            };

            using var communicationHandler = SurronCommunicationHandler.FromSerialPort("COM2");

            while (true)
            {
                try
                {
                    var packet = communicationHandler.ReceivePacket(Timeout.Infinite, CancellationToken.None)!;

                    if (packet.Command == SurronCmd.ReadRequest &&
                        packet.Address == BmsParameters.BmsAddress &&
                        dataToReturn.Contains(packet.Parameter))
                    {
                        if (DateTime.UtcNow - _escResponseTimeout > lastUpdate)
                        {
                            Debug.WriteLine("Parameter is outdated");
                            continue;
                        }

                        var responseData = (byte[])dataToReturn[packet.Parameter];
                        var responseBuffer = new byte[packet.DataLength];
                        // this copy here has two reasons:
                        // - to get the requested length regarless of the actual field length
                        // - to prevent a task switch during transmission to overwrite our data while we haven't completely read it.
                        Array.Copy(responseData, responseBuffer, Math.Min(packet.DataLength, responseData.Length));

                        var responsePacket = SurronDataPacket.Create(SurronCmd.ReadResponse, packet.Address, packet.Parameter, packet.DataLength, responseBuffer);
                        communicationHandler.SendPacket(responsePacket, CancellationToken.None);
                    }
                }
                catch (InvalidDataException)
                {
                }
            }
        }
    }
}
