﻿using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OctoAwesome.Network
{
    public class Client : BaseClient
    {
        public bool IsClient { get; set; }
        public event EventHandler<Package> PackageAvailable;

        private Package currentPackage;
        private static readonly int clientReceived;

        public Client() :
            base(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        public void SendPing()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(4);
            Encoding.UTF8.GetBytes("PING", 0, 4, buffer, 0);
            SendAsync(buffer, 4);
        }

        public void Connect(string host, ushort port)
        {
            var address = Dns.GetHostAddresses(host).FirstOrDefault(
                a => a.AddressFamily == Socket.AddressFamily);

            Socket.BeginConnect(new IPEndPoint(address, port), OnConnected, null);
        }

        protected override void Notify(OctoNetworkEventArgs args)
        {
            ClientDataAvailable(args);
        }

        private void OnConnected(IAsyncResult ar)
        {
            Socket.EndConnect(ar);

            while (true)
            {
                if (Socket.ReceiveAsync(ReceiveArgs))
                    return;

                Receive(ReceiveArgs);
            }
        }

        internal void SendPackage(Package package)
        {
            logger.Trace($"Send package Id = {package.UId} Command={package.Command} PayloadSize = {package.Payload.Length}");
            byte[] bytes = new byte[package.Payload.Length + Package.HEAD_LENGTH];
            package.SerializePackage(bytes);

            SendAsync(bytes, bytes.Length);
        }

        private void ClientDataAvailable(OctoNetworkEventArgs e)
        {
            byte[] bytes = new byte[e.DataCount];
            if (currentPackage == null)
            {
                currentPackage = new Package();
                logger.Trace("CurrentPackage is null, create new package: " + currentPackage.UId);
                if (e.DataCount >= Package.HEAD_LENGTH)
                {
                    e.NetworkStream.Read(bytes, 0, Package.HEAD_LENGTH);
                    currentPackage.TryDeserializeHeader(bytes);
                    e.DataCount -= Package.HEAD_LENGTH;
                }
            }

            e.NetworkStream.Read(bytes, 0, e.DataCount);
            currentPackage.DeserializePayload(bytes, 0, e.DataCount);

            if (currentPackage.IsComplete)
            {
                PackageAvailable?.Invoke(this, currentPackage);
                currentPackage = null;
            }
        }
    }
}