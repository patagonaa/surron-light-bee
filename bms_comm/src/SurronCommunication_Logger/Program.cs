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
        private static readonly TimeSpan _utcBmsTimeOffset = new TimeSpan(8, 20, 54).Negate();

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
                var response = bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, (byte)BmsParameters.Parameters.RtcTime, BmsParameters.GetLength(BmsParameters.Parameters.RtcTime), CancellationToken.None);
                if (response != null)
                {
                    Thread.Sleep(1000); // read RTC twice because the value is outdated right after wakeup
                    response = bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, (byte)BmsParameters.Parameters.RtcTime, BmsParameters.GetLength(BmsParameters.Parameters.RtcTime), CancellationToken.None);
                    if (response != null)
                    {
                        var rtcTime = new DateTime(2000 + response[0], response[1], response[2], response[3], response[4], response[5]);
                        Debug.WriteLine($"Got RTC Time: {rtcTime:s}");
                        var correctedTime = rtcTime + _utcBmsTimeOffset;
                        Debug.WriteLine($"Correcting to: {correctedTime:s}");
                        Rtc.SetSystemTime(correctedTime);
                        break;
                    }
                }
                Thread.Sleep(1000);
            }

            // BMS requester
            var parametersSlow = new BmsParameters.Parameters[]
            {
                // read by esc
                BmsParameters.Parameters.Unknown_7,
                BmsParameters.Parameters.Temperatures,
                BmsParameters.Parameters.BatteryPercent,
                BmsParameters.Parameters.BmsStatus,

                // other stuff
                BmsParameters.Parameters.TotalCapacity,
                BmsParameters.Parameters.ChargeCycles,
                BmsParameters.Parameters.Statistics,
                BmsParameters.Parameters.History,
            };

            var parametersFast = new BmsParameters.Parameters[]
            {
                // read by esc
                BmsParameters.Parameters.BatteryVoltage,

                // other stuff
                BmsParameters.Parameters.BatteryCurrent,
                BmsParameters.Parameters.RemainingCapacity,
                BmsParameters.Parameters.CellVoltages1,
            };

            var bmsRequester = new BmsRequester(bmsCommunicationHandler, parametersSlow, parametersFast);

            var bmsReadThread = new Thread(() => bmsRequester.Run());
            bmsReadThread.Start();

            // ESC request responder
            var escReadParameters = new BmsParameters.Parameters[]
            {
                BmsParameters.Parameters.Unknown_7,
                BmsParameters.Parameters.Temperatures,
                BmsParameters.Parameters.BatteryVoltage,
                BmsParameters.Parameters.BatteryPercent,
                BmsParameters.Parameters.BmsStatus,
            };
            var escResponder = new EscResponder(escCommunicationHandler, escReadParameters);
            bmsRequester.ParameterUpdateEvent += escResponder.SetBmsData;
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
