#if NANOFRAMEWORK_1_0
using System.Buffers.Binary;
#endif

namespace SurronCommunication.Communication
{
    public enum SurronReadResult
    {
        Success = 1,
        Timeout = 2,
        InvalidData = 3
    }
}
