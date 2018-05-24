﻿using System;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OctoAwesome.Network
{
    public class ConnectedClient : BaseClient
    {
        private static int received;
        

        public ConnectedClient(Socket socket) : base(socket)
        {

        }

        protected override int ProcessInternal(byte[] receiveArgsBuffer,int receiveOffset, int receiveArgsCount)
        {
            int read = OnMessageReceivedInvoke(receiveArgsBuffer, receiveOffset, receiveArgsCount);

            var tmpString = Encoding.UTF8.GetString(receiveArgsBuffer, 0, receiveArgsCount);
            var increment = Interlocked.Increment(ref received);
            if (increment > 0 && increment % 10000 == 0)
                Console.WriteLine("SERVER RECEIVED 10000 msgs");

            if (tmpString.StartsWith("+PING"))
            {
                var buffer = ArrayPool<byte>.Shared.Rent(4);
                Encoding.UTF8.GetBytes("-PONG", 0, 4, buffer, 0);
                SendAsync(buffer, 4);
            }
            return read;
        }
    }
}