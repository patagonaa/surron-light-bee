using nanoFramework.Hardware.Esp32;
using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using System;
using System.Diagnostics;
using System.Threading;
using System.Device.Gpio;
using nanoFramework.Runtime.Native;
using nanoFramework.System.IO.FileSystem;
using System.Collections;
using SurronCommunication;
using System.Buffers.Binary;

namespace SurronCommunication_Logger
{
    public class Program
    {
        private static readonly TimeSpan _utcBmsTimeOffset = new TimeSpan(8, 20, 44).Negate();

        public static void Main()
        {
            Configuration.SetPinFunction(Gpio.IO16, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(Gpio.IO17, DeviceFunction.COM2_TX);

            Configuration.SetPinFunction(Gpio.IO18, DeviceFunction.COM3_RX);
            Configuration.SetPinFunction(Gpio.IO19, DeviceFunction.COM3_TX);

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
                BmsParameters.RemainingCapacity,
                BmsParameters.TotalCapacity,
                BmsParameters.ChargeCycles,
                BmsParameters.Statistics,
                BmsParameters.History,
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

            // TODO: mount SD card

            //Configuration.SetPinFunction(Gpio.IO13, DeviceFunction.SPI1_MISO);
            //Configuration.SetPinFunction(Gpio.IO11, DeviceFunction.SPI1_MOSI);
            //Configuration.SetPinFunction(Gpio.IO12, DeviceFunction.SPI1_CLOCK);

            //var sdCard = new SDCard(new SDCard.SDCardSpiParameters { spiBus = 1, chipSelectPin = 46 });
            //while (!sdCard.IsMounted)
            //{
            //    try
            //    {
            //        Console.WriteLine("Trying to mount SD card");
            //        sdCard.Mount();
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine($"Mount failed: {ex.Message}");
            //        Thread.Sleep(1000);
            //    }
            //}
            
            var dataLogger = new DataLogger($"I:\\log_{DateTime.UtcNow:yyyy'-'MM'-'dd'_'HH'-'mm'-'ss}.ndjson");
            bmsRequester.ParameterUpdateEvent += dataLogger.SetData;
            escResponder.ParameterUpdateEvent += dataLogger.SetData;
            var dataLoggerThread = new Thread(() => dataLogger.Run());
            dataLoggerThread.Start();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
