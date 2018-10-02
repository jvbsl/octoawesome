﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using OctoAwesome.Basics;

namespace OctoAwesome.Network
{
    public class NetworkPersistenceManager : IPersistenceManager
    {
        private Client client;
        private readonly IDefinitionManager definitionManager;

        private Dictionary<uint, Awaiter> packages;

        public NetworkPersistenceManager(IDefinitionManager definitionManager)
        {
            client = new Client();
            client.PackageAvailable += ClientPackageAvailable;
            packages = new Dictionary<uint, Awaiter>();
            this.definitionManager = definitionManager;
        }


        public NetworkPersistenceManager(string host, ushort port, IDefinitionManager definitionManager) : this(definitionManager) => client.Connect(host, port);

        public void DeleteUniverse(Guid universeGuid)
        {
            //throw new NotImplementedException();
        }

        public Awaiter Load(out SerializableCollection<IUniverse> universes) => throw new NotImplementedException();

        public Awaiter Load(out IChunkColumn chunkColumn, Guid universeGuid, IPlanet planet, Index2 columnIndex)
        {
            var package = new Package((ushort)OfficialCommands.LoadColumn, 0);

            using (var memoryStream = new MemoryStream())
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write(universeGuid.ToByteArray());
                binaryWriter.Write(planet.Id);
                binaryWriter.Write(columnIndex.X);
                binaryWriter.Write(columnIndex.Y);

                package.Payload = memoryStream.ToArray();
            }
            client.SendPackage(package);

            var awaiter = new Awaiter();
            chunkColumn = new ChunkColumn();
            awaiter.Serializable = chunkColumn;
            packages.Add(package.UId, awaiter);

            return awaiter;
        }

        public Awaiter Load(out IPlanet planet, Guid universeGuid, int planetId)
        {
            var package = new Package((ushort)OfficialCommands.GetPlanet, 0);
            client.SendPackage(package);

            planet = new ComplexPlanet();
            
            var awaiter = new Awaiter();
            awaiter.Serializable = planet;
            packages.Add(package.UId, awaiter);
            return awaiter;
        }

        public Awaiter Load(out Player player, Guid universeGuid, string playername)
        {
            var playernameBytes = Encoding.UTF8.GetBytes(playername);

            var package = new Package((ushort)OfficialCommands.Whoami, playernameBytes.Length)
            {
                Payload = playernameBytes
            };
            //package.Write(playernameBytes);


            client.SendPackage(package);

            player = new Player();
            var awaiter = new Awaiter();
            awaiter.Serializable = player;
            packages.Add(package.UId, awaiter);
            return awaiter;
        }

        public Awaiter Load(out IUniverse universe, Guid universeGuid)
        {
            var package = new Package((ushort)OfficialCommands.GetUniverse, 0);

            universe = new Universe();
            var awaiter = new Awaiter();
            awaiter.Serializable = universe;
            packages.Add(package.UId, awaiter);
            client.SendPackage(package);

            return awaiter;
        }

        public void SaveColumn(Guid universeGuid, int planetId, IChunkColumn column)
        {
            //throw new NotImplementedException();
        }

        public void SavePlanet(Guid universeGuid, IPlanet planet)
        {
            //throw new NotImplementedException();
        }

        public void SavePlayer(Guid universeGuid, Player player)
        {
            //throw new NotImplementedException();
        }

        public void SaveUniverse(IUniverse universe)
        {
            //throw new NotImplementedException();
        }

        private void ClientPackageAvailable(object sender, Package e)
        {
            packages[e.UId].SetResult(e.Payload, definitionManager);
            packages.Remove(e.UId);
        }

    }
}
