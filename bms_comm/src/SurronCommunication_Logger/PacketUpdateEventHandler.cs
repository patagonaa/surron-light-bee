using System;
using System.Collections;

namespace SurronCommunication_Logger
{
    public delegate void ParameterUpdateEventHandler(DateTime updateTime, ushort addr, Hashtable newData);
}
