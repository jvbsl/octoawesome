using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;

namespace OctoAwesome.Network
{
    public class PackageManager : ObserverBase<OctoNetworkEventArgs>
    {
        public List<BaseClient> ConnectedClients { get; set; }

        private readonly Logger logger;
        private Dictionary<BaseClient, Package> packages;
        public event EventHandler<OctoPackageAvailableEventArgs> PackageAvailable;
        private readonly MemoryStream backupStream;
        private ConcurrentQueue<OctoNetworkEventArgs> receivedQue;
        private Thread internalThread;

        public PackageManager()
        {
            receivedQue = new ConcurrentQueue<OctoNetworkEventArgs>();
            packages = new Dictionary<BaseClient, Package>();
            ConnectedClients = new List<BaseClient>();
            logger = LogManager.GetCurrentClassLogger();
            backupStream = new MemoryStream();
        }

        public void AddConnectedClient(BaseClient client) => client.Subscribe(this);

        public void SendPackage(Package package, BaseClient client)
        {
            logger.Trace($"Send Package to client id= {package.UId} Command= {package.Command} PayloadSize={package.Payload.Length}");
            byte[] bytes = new byte[package.Payload.Length + Package.HEAD_LENGTH];
            package.SerializePackage(bytes);
            client.SendAsync(bytes, bytes.Length);
        }

        public void StartProcessing()
        {
            internalThread = new Thread(() =>
            {
                while (true)
                {
                    if (receivedQue.TryDequeue(out OctoNetworkEventArgs args))
                    {
                        logger.Trace($"Dequeue recived data: RestQueue: {receivedQue.Count}");
                        ClientDataAvailable(args);
                    }
                }
            })
            {
                IsBackground = true
            };
            internalThread.Start();
        }

        private void ClientDataAvailable(OctoNetworkEventArgs e)
        {
            var baseClient = e.Client;

            var data = e.NetworkStream.DataAvailable(Package.HEAD_LENGTH - 0);

            byte[] bytes;
            bytes = new byte[e.DataCount];

            if (!packages.TryGetValue(baseClient, out Package package))
            {
                int backUpOffset = 0;

                if (backupStream.Length > 0)
                {
                    e.DataCount += (int)backupStream.Length;
                    backupStream.Read(bytes, 0, (int)backupStream.Length);
                    backUpOffset = (int)backupStream.Length;
                    backupStream.Position = 0;
                    backupStream.SetLength(0);
                    data += (int)backupStream.Length;
                }


                if (data < Package.HEAD_LENGTH)
                {
                    logger.Error($"data available not enough for new package: Data = {data}");
                    logger.Trace("Write rest to backupstream");
                    e.NetworkStream.Read(bytes, backUpOffset, data);
                    backupStream.Write(bytes, 0, data);
                    backupStream.Position = 0;
                    return;
                }


                package = new Package(false);
                logger.Trace("Can't get package, create new package " + package.UId);


                int current = backUpOffset;

                current += e.NetworkStream.Read(bytes, current, Package.HEAD_LENGTH - current);

                if (current != Package.HEAD_LENGTH)
                {
                    logger.Error($"ID = {package.UId} Package was not complete, only got: {current} bytes");
                    return;
                }


                if (package.TryDeserializeHeader(bytes))
                {
                    packages.Add(baseClient, package);
                    e.DataCount -= Package.HEAD_LENGTH;
                    logger.Trace($"Deserialize Header (Id={package.UId} Command={package.Command} PayloadSize={package.Payload.Length})");
                }
                else
                {
                    logger.Error("Can not deserialize header");
                    return;
                }
            }

            int count = package.PayloadRest();

            if ((e.DataCount - count) < 1)
                count = e.DataCount;

            if (count > 0)
                count = e.NetworkStream.Read(bytes, 0, count);

            count = package.DeserializePayload(bytes, 0, count);

            if (package.IsComplete)
            {
                logger.Trace($"ID = {package.UId} package is complete");
                packages.Remove(baseClient);
                PackageAvailable?.Invoke(this, new OctoPackageAvailableEventArgs { BaseClient = baseClient, Package = package });

                if (e.DataCount - count > 0)
                {
                    logger.Trace($"Rest data after package {package.UId}: " + (e.DataCount - count));
                    ClientDataAvailable(new OctoNetworkEventArgs() { Client = baseClient, DataCount = (e.DataCount - count), NetworkStream = e.NetworkStream });
                }
            }
            else
            {
                if (e.DataCount - count > 0)
                    logger.Error($"ID = {package.UId} Restdata: " + (e.DataCount - count));
            }

            logger.Trace($"ID = {package.UId} Data Read: " + count);
        }

        protected override void OnNextCore(OctoNetworkEventArgs args)
        {
            logger.Trace("On Next Core called with data " + args.DataCount);
            receivedQue.Enqueue(args);
        }

        protected override void OnErrorCore(Exception error) => throw new NotImplementedException();
        protected override void OnCompletedCore() => throw new NotImplementedException();
    }
}
