using System;
using System.IO;
using System.Reflection;

namespace OctoAwesome.Network
{
    public class OctoNetworkStream
    {
        private int _readPosition;
        private int _writePosition;

        private readonly int _capacity;
        private readonly int CapacityMask;
        
        public byte[] Buffer { get; }

        public OctoNetworkStream(int capacity = 1024*1024*8)
        {
            Buffer = new byte[capacity];
            _capacity = capacity;

            if ((capacity & (capacity - 1)) != 0 )
                throw new NotSupportedException("capacity not power of 2");
            
            CapacityMask = capacity - 1;
        }

        public int Write(byte[] buffer, int offset, int count)
        {
            int maxWrite = count;
            int curWritePosition = _writePosition;
            int curReadPosition = _readPosition;
            maxWrite = Math.Min(maxWrite, _capacity - (curWritePosition - curReadPosition));
            for (int i = 0; i < maxWrite; i++, curWritePosition++)
            {
                Buffer[curWritePosition & CapacityMask] = buffer[offset + i];
            }

            _writePosition = curWritePosition;
            return maxWrite;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int maxRead = count;
            int curReadPosition = _readPosition;
            int curWritePosition = _writePosition;
            maxRead = Math.Min(maxRead, curWritePosition - curReadPosition);

            for (int i = 0; i < maxRead; i++, curReadPosition++)
            {
                buffer[i + offset] = Buffer[curReadPosition & CapacityMask];
            }

            _readPosition = curReadPosition;

            return maxRead;
        }
    }
}
