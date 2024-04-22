using nanoFramework.Hardware.Esp32;
using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using System;
using System.Diagnostics;
using System.Threading;
using System.Device.Gpio;
using nanoFramework.Runtime.Native;
using Iot.Device.Ws28xx.Esp32;

namespace SurronCommunication_Logger
{
    public class Program
    {
        public static void Main()
        {
            // Config
            var utcBmsTimeOffset = new TimeSpan(8, 20, 54).Negate();
            var buttonPin = 0;
            var debugWsLedPin = 38;
            var logPath = "I:";

            Configuration.SetPinFunction(Gpio.IO16, DeviceFunction.COM2_RX); // ESC
            Configuration.SetPinFunction(Gpio.IO17, DeviceFunction.COM2_TX); // ESC

            Configuration.SetPinFunction(Gpio.IO06, DeviceFunction.COM3_RX); // BMS
            Configuration.SetPinFunction(Gpio.IO07, DeviceFunction.COM3_TX); // BMS

            var uploadUrl = "http://surronlogger.example.com";
            var uploadUsername = "admin";
            var uploadPassword = "admin";
            // ---

            var gpioController = new GpioController();

            var cts = new CancellationTokenSource();
            var button = gpioController.OpenPin(buttonPin, PinMode.InputPullUp);
            button.DebounceTimeout = TimeSpan.FromMilliseconds(10);
            button.ValueChanged += (o, e) =>
            {
                if (e.ChangeType == PinEventTypes.Falling)
                    cts.Cancel();
            };
            var token = cts.Token;

            Ws28xx debugLed = new Ws2808(debugWsLedPin, 1);

            debugLed.Image.SetPixel(0, 0, 10, 0, 0);
            debugLed.Update();

            var bmsCommunicationHandler = SurronCommunicationHandler.FromSerialPort("COM3", "BMS");
            var escCommunicationHandler = SurronCommunicationHandler.FromSerialPort("COM2", "ESC");

            GetCurrentTime(bmsCommunicationHandler, utcBmsTimeOffset, token);

            if (!token.IsCancellationRequested)
            {
                debugLed.Image.SetPixel(0, 0, 0, 10, 0);
                debugLed.Update();

                RunLogger(bmsCommunicationHandler, escCommunicationHandler, logPath, token);
            }

            debugLed.Image.SetPixel(0, 0, 0, 0, 10);
            debugLed.Update();

            var uploader = new InfluxUploader(influxUrl, influxDatabase, influxUsername, influxPassword);
            uploader.Run(logPath);

            debugLed.Image.SetPixel(0, 0, 10, 0, 0);
            debugLed.Update();

            Thread.Sleep(Timeout.Infinite);
        }

        private static void GetCurrentTime(SurronCommunicationHandler bmsCommunicationHandler, TimeSpan utcBmsTimeOffset, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("Trying to read BMS RTC...");
                var response = bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, (byte)BmsParameterId.RtcTime, BmsParameters.GetLength(BmsParameterId.RtcTime), CancellationToken.None);
                if (response != null)
                {
                    Thread.Sleep(1000); // read RTC twice because the value is outdated right after wakeup
                    response = bmsCommunicationHandler.ReadRegister(BmsParameters.BmsAddress, (byte)BmsParameterId.RtcTime, BmsParameters.GetLength(BmsParameterId.RtcTime), CancellationToken.None);
                    if (response != null)
                    {
                        var rtcTime = new DateTime(2000 + response[0], response[1], response[2], response[3], response[4], response[5]);
                        Debug.WriteLine($"Got RTC Time: {rtcTime:s}");
                        var correctedTime = rtcTime + utcBmsTimeOffset;
                        Debug.WriteLine($"Correcting to: {correctedTime:s}");
                        Rtc.SetSystemTime(correctedTime);
                        break;
                    }
                }
                Thread.Sleep(1000);
            }
            Console.WriteLine($"aborted RTC read, time is now {DateTime.UtcNow:s}");
        }

        private static void RunLogger(SurronCommunicationHandler bmsCommunicationHandler, SurronCommunicationHandler escCommunicationHandler, string logPath, CancellationToken token)
        {
            // BMS requester
            var parametersSlow = new BmsParameterId[]
            {
                // read by esc
                BmsParameterId.Unknown_7,
                BmsParameterId.Temperatures,
                BmsParameterId.BatteryPercent,
                BmsParameterId.BmsStatus,

                // other stuff
                BmsParameterId.TotalCapacity,
                BmsParameterId.ChargeCycles,
                BmsParameterId.History,
                BmsParameterId.CellVoltages1,
            };

            var parametersFast = new BmsParameterId[]
            {
                // read by esc
                BmsParameterId.BatteryVoltage,

                // other stuff
                BmsParameterId.BatteryCurrent,
                BmsParameterId.RemainingCapacity,
                BmsParameterId.Statistics,
            };

            var bmsRequester = new BmsRequester(bmsCommunicationHandler, parametersSlow, parametersFast);

            var bmsReadThread = new Thread(() => bmsRequester.Run(token));
            bmsReadThread.Start();

            // ESC request responder
            var escReadParameters = new BmsParameterId[]
            {
                BmsParameterId.Unknown_7,
                BmsParameterId.Temperatures,
                BmsParameterId.BatteryVoltage,
                BmsParameterId.BatteryPercent,
                BmsParameterId.BmsStatus,
            };
            var escResponder = new EscResponder(escCommunicationHandler, escReadParameters);
            bmsRequester.ParameterUpdateEvent += escResponder.SetBmsData;

            var escRespondThread = new Thread(() => escResponder.Run(token));
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

            var dataLogger = new DataLogger($"{logPath}\\log_{DateTime.UtcNow:yyyy'-'MM'-'dd'_'HH'-'mm'-'ss}.bin");
            bmsRequester.ParameterUpdateEvent += dataLogger.SetData;
            escResponder.ParameterUpdateEvent += dataLogger.SetData;

            var dataLoggerThread = new Thread(() => dataLogger.Run(token));
            dataLoggerThread.Start();

            bmsReadThread.Join();
            escRespondThread.Join();
            dataLoggerThread.Join();
        }
    }
}
