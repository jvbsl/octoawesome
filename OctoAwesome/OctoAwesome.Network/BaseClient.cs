using NLog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace OctoAwesome.Network
{
    public abstract class BaseClient
    {
        public event EventHandler<OctoPackageAvailableEventArgs> PackageAvailable;
        //public delegate int ReceiveDelegate(object sender, (byte[] Data, int Offset, int Count) eventArgs);
        //public event ReceiveDelegate OnMessageRecived;

        protected readonly Socket Socket;
        protected readonly SocketAsyncEventArgs ReceiveArgs;

        protected readonly OctoNetworkStream internalSendStream;
        protected readonly OctoNetworkStream internalRecivedStream;
        protected readonly Logger logger;

        private byte readSendQueueIndex;
        private byte nextSendQueueWriteIndex;
        private bool sending;
        private readonly SocketAsyncEventArgs sendArgs;
        private readonly (byte[] data, int len)[] sendQueue;
        private readonly object sendLock;

        protected BaseClient(Socket socket)
        {
            logger = LogManager.GetCurrentClassLogger();

            sendQueue = new (byte[] data, int len)[512];
            sendLock = new object();

            Socket = socket;
            Socket.NoDelay = true;

            ReceiveArgs = new SocketAsyncEventArgs();
            ReceiveArgs.Completed += OnReceived;
            ReceiveArgs.SetBuffer(ArrayPool<byte>.Shared.Rent(1024*1024*8), 0, 1024*1024*8);

            sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += OnSent;

            internalSendStream = new OctoNetworkStream();
            internalRecivedStream = new OctoNetworkStream();

        }

        public void Start()
        {
            while (true)
            {
                if (Socket.ReceiveAsync(ReceiveArgs))
                    return;
                Receive(ReceiveArgs);
            }
        }

        public void SendAsync(byte[] data, int len)
        {
            logger.Trace("Send async length " + len);
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
        private void SendInternal(byte[] data, int len)
        {
            while (true)
            {
                sendArgs.SetBuffer(data, 0, len);

                if (Socket.SendAsync(sendArgs))
                    return;
                
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

        protected void Receive(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred < 1)
                return;

            logger.Trace($"Data Received: Transferred Data = {e.BytesTransferred}");

            int offset = 0;
            int count = 0;
            do
            {
                count = internalRecivedStream.Write(e.Buffer, offset, e.BytesTransferred - offset);
                logger.Trace($"Write: {offset} - {e.BytesTransferred - offset} count: {count}");
                logger.Trace($"EventArgs: Count: {e.Count}, Offset: {e.Offset}");
                
                DataReceived(internalRecivedStream);

                if (count < 0)
                    continue;

                offset += count;

            } while (offset < e.BytesTransferred);
        }

        private void OnReceived(object sender, SocketAsyncEventArgs e)
        {
            Receive(e);

            while (Socket.Connected)
            {
                if (Socket.ReceiveAsync(ReceiveArgs))
                    return;

                Receive(ReceiveArgs);
            }
        }

    

        private Package _package;
        private readonly byte[] _headerBuffer = new byte[Package.HEAD_LENGTH]; // TODO: shared?
        private int _offset = 0;

        private void FinalizePackage()
        {
            _offset = 0;
                
            logger.Trace($"ID = {_package.UId} package is complete");
            PackageAvailable?.Invoke(this, new OctoPackageAvailableEventArgs { BaseClient = this, Package = _package });
        }

        private void DataReceived(OctoNetworkStream stream)
        {
            if (_offset < Package.HEAD_LENGTH)
            {
                int res;
                do
                {
                    try{
                        res = stream.Read(_headerBuffer, _offset, Package.HEAD_LENGTH - _offset);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Header read exc: {_offset}");
                        throw;
                    }
                    if (res < 0)
                    {
                        throw new NotSupportedException();
                    }

                    if (res == 0)
                        return; // not complete and no data available
                    if (res < 0)
                        throw new NotSupportedException();

                    _offset += res;
                } while (_offset < Package.HEAD_LENGTH);


                _package = new Package(false);
                if (!_package.TryDeserializeHeader(_headerBuffer))
                    throw new InvalidDataException("Can not deserialize header with these bytes :(");

                if (_package.Payload.Length == 0)
                    FinalizePackage();
            }

            var payload = _package.Payload;
            int payloadOffset = _offset - Package.HEAD_LENGTH;
            {
                int res = 0;
                do
                {
                    try
                    {
                        res = stream.Read(payload, payloadOffset, payload.Length - payloadOffset);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("$Payload read exc: {payloadOffset}/{payload.Length}");
                        throw;
                    }
                    if (res < 0)
                    {
                        throw new NotSupportedException();
                    }

                    if (res == 0)
                        return; // not complete and no data available
                    if (res < 0)
                        throw new NotSupportedException();

                    _offset += res;
                    payloadOffset += res;
                } while (payloadOffset < payload.Length);

                FinalizePackage();
            }

        }
    }
}
