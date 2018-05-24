﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OctoAwesome.Network
{
    public abstract class BaseClient
    {
        public delegate int ReceiveDelegate(object sender, (byte[] Data, int Offset, int Count) eventArgs);
        public event ReceiveDelegate OnMessageRecived;

        protected readonly Socket Socket;
        protected readonly SocketAsyncEventArgs ReceiveArgs;

        private byte readSendQueueIndex;
        private byte nextSendQueueWriteIndex;
        private bool sending;

        private readonly SocketAsyncEventArgs sendArgs;

        private readonly (byte[] data, int len)[] sendQueue;
        private readonly object sendLock;

        protected BaseClient(Socket socket)
        {
            sendQueue = new(byte[] data, int len)[256];
            sendLock = new object();

            Socket = socket;
            Socket.NoDelay = true;

            ReceiveArgs = new SocketAsyncEventArgs();
            ReceiveArgs.Completed += OnReceived;
            ReceiveArgs.SetBuffer(ArrayPool<byte>.Shared.Rent(2048), 0, 2048);

            sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += OnSent;

        }

        public void Start()
        {
            while (true)
            {
                if (Socket.ReceiveAsync(ReceiveArgs))
                    return;
                int offset = 0;
                offset += ProcessInternal(ReceiveArgs.Buffer,offset, ReceiveArgs.BytesTransferred);
            }
        }

        public void SendAsync(byte[] data, int len)
        {
            lock (sendLock)
            {
                if (sending)
                {
                    sendQueue[nextSendQueueWriteIndex++] = (data, len);
                    return;
                }

                sending = true;
            }

            SendInternal(data, len);

        }
        public void SendAsync(Package package)
        {
            var buffer = new byte[2048];
            int /*offset = 0,*/read = 0;
            do
            {
                read = package.Read(buffer, 0, buffer.Length);
                //offset += read;
                SendAsync(buffer, read);

            } while (read >= buffer.Length);
        }

        public Package SendAndReceive(Package package)
        {
            var manualResetEvent = new ManualResetEvent(false);
            Package returnPackage = new Package();
            var onDataReceive = new ReceiveDelegate((sender, eventArgs) =>
            {
                int read = returnPackage.Write(eventArgs.Data, 0, eventArgs.Count);

                if (read < eventArgs.Count)
                    manualResetEvent.Set();

                return read;
            });
            OnMessageRecived += onDataReceive;

            SendAsync(package);
            manualResetEvent.WaitOne();

            OnMessageRecived -= onDataReceive;

            return returnPackage;
        }

        protected abstract int ProcessInternal(byte[] receiveArgsBuffer,int receiveOffset, int receiveArgsCount);

        protected int OnMessageReceivedInvoke(byte[] receiveArgsBuffer,int receiveOffset, int receiveArgsCount)
            => OnMessageRecived?.Invoke(this, (receiveArgsBuffer, receiveOffset, receiveArgsCount)) ?? 0;

        private void SendInternal(byte[] data, int len)
        {
            while (true)
            {
                sendArgs.SetBuffer(data, 0, len);

                if (Socket.SendAsync(sendArgs))
                    return;

                ArrayPool<byte>.Shared.Return(data);

                lock (sendLock)
                {
                    if (readSendQueueIndex < nextSendQueueWriteIndex)
                    {
                        (data, len) = sendQueue[readSendQueueIndex++];
                    }
                    else
                    {
                        nextSendQueueWriteIndex = 0;
                        readSendQueueIndex = 0;
                        sending = false;
                        return;
                    }
                }
            }
        }

        private void OnSent(object sender, SocketAsyncEventArgs e)
        {
            byte[] data;
            int len;

            ArrayPool<byte>.Shared.Return(e.Buffer);

            lock (sendLock)
            {
                if (readSendQueueIndex < nextSendQueueWriteIndex)
                {
                    (data, len) = sendQueue[readSendQueueIndex++];
                }
                else
                {
                    nextSendQueueWriteIndex = 0;
                    readSendQueueIndex = 0;
                    sending = false;
                    return;
                }
            }

            SendInternal(data, len);
        }

        private void OnReceived(object sender, SocketAsyncEventArgs e)
        {
            int offset = 0;
            do
            {
                offset += ProcessInternal(e.Buffer, offset, e.BytesTransferred);
            } while (offset < e.BytesTransferred);
            while (Socket.Connected)
            {
                if (Socket.ReceiveAsync(ReceiveArgs))
                    return;
                offset = 0;
                do
                {
                    offset += ProcessInternal(ReceiveArgs.Buffer,offset, ReceiveArgs.BytesTransferred);
                } while (offset < ReceiveArgs.BytesTransferred);
            }
        }
    }
}