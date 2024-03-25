using nanoFramework.Hardware.Esp32;
using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using System;
using System.Diagnostics;
using System.Threading;
using System.Device.Gpio;
using nanoFramework.Runtime.Native;

namespace SurronCommunication_Logger
{
    public class Program
    {
        private static readonly TimeSpan _utcBmsTimeOffset = new TimeSpan(8, 20, 44).Negate();

        public static void Main()
        {
            Configuration.SetPinFunction(16, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(17, DeviceFunction.COM2_TX);

            Configuration.SetPinFunction(18, DeviceFunction.COM3_RX);
            Configuration.SetPinFunction(19, DeviceFunction.COM3_TX);

            var bmsCommunicationHandler = SurronCommunicationHandler.FromSerialPort("COM3", "BMS");
            var escCommunicationHandler = SurronCommunicationHandler.FromSerialPort("COM2", "ESC");

            while (true)
            {
                Debug.WriteLine("Trying to read BMS RTC...");
                var response = bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, BmsParameters.RtcTime.Id, BmsParameters.RtcTime.Length, CancellationToken.None);
                if (response != null)
                {
                    var rtcTime = new DateTime(2000 + response[0], response[1], response[2], response[3], response[4], response[5]);
                    Debug.WriteLine($"Got RTC Time: {rtcTime:s}");
                    var correctedTime = rtcTime + _utcBmsTimeOffset;
                    Debug.WriteLine($"Correcting to: {correctedTime:s}");
                    Rtc.SetSystemTime(correctedTime);
                    break;
                }
                Thread.Sleep(1000);
            }
            // BMS requester
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

            var bmsRequester = new BmsRequester(bmsCommunicationHandler, parametersToRead);

            var bmsReadThread = new Thread(() => bmsRequester.Run());
            bmsReadThread.Start();

            // ESC request responder
            var escReadParameters = new byte[]
            {
                BmsParameters.Unknown_7.Id,
                BmsParameters.Temperatures.Id,
                BmsParameters.BatteryVoltage.Id,
                BmsParameters.BatteryPercent.Id,
                BmsParameters.BmsStatus.Id,
            };
            var escResponder = new EscResponder(escCommunicationHandler, escReadParameters);
            bmsRequester.ParameterUpdateEvent += escResponder.SetData;
            var escRespondThread = new Thread(() => escResponder.Run());
            escRespondThread.Start();

            // Logger
            var dataLogger = new DataLogger();
            bmsRequester.ParameterUpdateEvent += dataLogger.SetData;
            var dataLoggerThread = new Thread(() => dataLogger.Run());
            dataLoggerThread.Start();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
