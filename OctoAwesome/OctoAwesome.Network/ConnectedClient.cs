using System;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OctoAwesome.Network
{
    public class ConnectedClient : BaseClient
    {
        public ConnectedClient(Socket socket) : base(socket)
        {

        }
    }
}