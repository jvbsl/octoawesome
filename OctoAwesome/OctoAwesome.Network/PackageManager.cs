using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OctoAwesome.Network
{
    public class PackageManager : IObserver<OctoNetworkEventArgs>
    {
        public List<BaseClient> ConnectedClients { get; set; }
        private Dictionary<BaseClient, Package> packages;
        public event EventHandler<OctoPackageAvailableEventArgs> PackageAvailable;


        private readonly List<Subscription<OctoNetworkEventArgs>> subsciptions;
        private readonly Dictionary<BaseClient, Package> packages;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ConcurrentQueue<OctoNetworkEventArgs> receivingQueue;
        private readonly MemoryStream backupStream;
        private readonly Logger logger;

        public PackageManager()
        {
            packages = new Dictionary<BaseClient, Package>();
            subsciptions = new List<Subscription<OctoNetworkEventArgs>>();
            receivingQueue = new ConcurrentQueue<OctoNetworkEventArgs>();
            cancellationTokenSource = new CancellationTokenSource();
            backupStream = new MemoryStream();
            logger = LogManager.GetCurrentClassLogger();
        }

        public void AddConnectedClient(BaseClient client)
        {
           subsciptions.Add((Subscription<OctoNetworkEventArgs>)client.Subscribe(this));
        }

        public void SendPackage(Package package, BaseClient client)
        {
            logger.Trace($"Send Package to client id= {package.UId} Command= {package.Command} PayloadSize={package.Payload.Length}");
            byte[] bytes = new byte[package.Payload.Length + Package.HEAD_LENGTH];
            package.SerializePackage(bytes);
            client.SendAsync(bytes, bytes.Length);
        }

        public void OnNext(OctoNetworkEventArgs value)
            => receivingQueue.Enqueue(value);

        public void OnError(Exception error) => throw new NotImplementedException();

        public void OnCompleted() => throw new NotImplementedException();

        public Task Start()
        {
            var task = new Task(InternalProcess, cancellationTokenSource.Token, TaskCreationOptions.LongRunning);
            task.Start(TaskScheduler.Default);
            return task;
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
        }

        private void InternalProcess()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (receivingQueue.IsEmpty)
                    continue;

                if (receivingQueue.TryDequeue(out OctoNetworkEventArgs eventArgs))
                    ClientDataAvailable(eventArgs);
                logger.Trace($"Dequeue recived data: RestQueue: {receivedQue.Count}");
            }
        }

        private void ClientDataAvailable(OctoNetworkEventArgs e)
        {
            var baseClient = e.Client;

            var data = e.NetworkStream.DataAvailable(Package.HEAD_LENGTH);

            byte[] bytes;
            bytes = new byte[e.DataCount];

            if (!packages.TryGetValue(baseClient, out Package package))
            {
                int offset = 0;

                if (backupStream.Length > 0)
                {
                    e.DataCount += (int)backupStream.Length;
                    backupStream.Read(bytes, 0, (int)backupStream.Length);
                    offset = (int)backupStream.Length;
                    backupStream.Position = 0;
                    backupStream.SetLength(0);
                }

                data += offset;

                if (data < Package.HEAD_LENGTH)
                {
                    logger.Error($"data available not enough for new package: Data = {data}");
                    logger.Trace("Write rest to backupstream");

                    e.NetworkStream.Read(bytes, offset, data);
                    backupStream.Write(bytes, 0, data);
                    backupStream.Position = 0;
                    logger.Error($"ID = {package.UId} Package was not complete, only got: {current} bytes");
                    return;
                }

                package = new Package(false);


                offset += e.NetworkStream.Read(bytes, offset, Package.HEAD_LENGTH - offset);

                if (package.TryDeserializeHeader(bytes))
                {
                    packages.Add(baseClient, package);
                    e.DataCount -= Package.HEAD_LENGTH;
                }
                else
                {
                    var exception = new InvalidDataException("Can not deserialize header with these bytes :(");
                    logger.Error("Can not deserialize header", exception);
                    throw exception;
                }

            }

            int count = package.PayloadRest;

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
                logger.Trace($"Rest data after package {package.UId}: " + (e.DataCount - count));
                ClientDataAvailable(new OctoNetworkEventArgs() { Client = baseClient, DataCount = e.DataCount - count, NetworkStream = e.NetworkStream });
            }

            logger.Trace($"ID = {package.UId} Data Read: " + count);
        }

    }
}
