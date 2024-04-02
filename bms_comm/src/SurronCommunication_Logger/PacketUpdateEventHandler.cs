using SurronCommunication_Logging.Logging;
using System;
using System.Collections;

namespace SurronCommunication_Logger
{
    public delegate void ParameterUpdateEventHandler(DateTime updateTime, LogCategory logCategory, Hashtable newData);
}
