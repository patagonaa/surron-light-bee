using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using SurronCommunication_Logging.Logging;
using System;
using System.Collections;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class BmsRequester
    {
        private readonly ISurronCommunicationHandler _bmsCommunicationHandler;
        private readonly BmsParameters.Parameters[] _parametersSlow;
        private readonly BmsParameters.Parameters[] _parametersFast;
        private readonly Hashtable _currentValuesSlow;
        private readonly Hashtable _currentValuesFast;

        public event ParameterUpdateEventHandler? ParameterUpdateEvent;

        public BmsRequester(ISurronCommunicationHandler bmsCommunicationHandler, BmsParameters.Parameters[] parametersSlow, BmsParameters.Parameters[] parametersFast)
        {
            _bmsCommunicationHandler = bmsCommunicationHandler;
            _parametersSlow = parametersSlow;
            _parametersFast = parametersFast;

            _currentValuesSlow = new Hashtable(_parametersSlow.Length);
            _currentValuesFast = new Hashtable(_parametersFast.Length);
        }

        public void Run(CancellationToken token)
        {
            var fastInterval = TimeSpan.FromSeconds(1);
            var slowDivider = 5;

            int updateCount = 0;
            var nextFastUpdate = DateTime.MinValue;
            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var sleepTime = nextFastUpdate - now;
                if (sleepTime > TimeSpan.Zero)
                {
                    Thread.Sleep(sleepTime);
                    now += sleepTime;
                }

                ReadAndPublish(now, true);

                if (updateCount % slowDivider == 0)
                {
                    ReadAndPublish(now, false);
                }

                nextFastUpdate = now + fastInterval;
                updateCount++;
            }
        }

        private void ReadAndPublish(DateTime now, bool fast)
        {
            Hashtable currentValues;
            BmsParameters.Parameters[] parametersToRead;
            LogCategory logCategory;

            if (fast)
            {
                currentValues = _currentValuesFast;
                parametersToRead = _parametersFast;
                logCategory = LogCategory.BmsFast;
            }
            else
            {
                currentValues = _currentValuesSlow;
                parametersToRead = _parametersSlow;
                logCategory = LogCategory.BmsSlow;
            }

            var anyReceived = false;
            foreach (var parameterToRead in parametersToRead)
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
            }
            if (anyReceived)
                ParameterUpdateEvent?.Invoke(now, logCategory, currentValues);
        }
    }
}
