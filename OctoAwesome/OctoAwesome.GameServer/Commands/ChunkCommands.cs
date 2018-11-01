using CommandManagementSystem.Attributes;
using NLog;
using OctoAwesome.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoAwesome.GameServer.Commands
{
    public static class ChunkCommands
    {
        private static Logger logger;

        static ChunkCommands()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        [Command((ushort)OfficialCommands.LoadColumn)]
        public static byte[] LoadColumn(byte[] data)
        {
            
            Guid guid;
            int planetId;
            Index2 index2;

            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BinaryReader(memoryStream))
            {
                guid = new Guid(reader.ReadBytes(16));
                planetId = reader.ReadInt32();
                index2 = new Index2(reader.ReadInt32(), reader.ReadInt32());
            }
            logger.Trace($"Load column [{guid.ToString()}] on planet {planetId} with index {index2.ToString()}");
            var column = Program.ServerHandler.SimulationManager.LoadColumn(guid, planetId, index2);

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                column.Serialize(writer, Program.ServerHandler.SimulationManager.DefinitionManager);
                return memoryStream.ToArray();
            }
        }

        [Command((ushort)OfficialCommands.SaveColumn)]
        public static byte[] SaveColumn(byte[] data)
        {
            var chunkColumn = new ChunkColumn();

            using (var memoryStream = new MemoryStream(data))
            using (var reader = new BinaryReader(memoryStream))
            {
                chunkColumn.Deserialize(reader, Program.ServerHandler.SimulationManager.DefinitionManager);
            }
            logger.Trace($"Save column [{chunkColumn.Index.ToString()}]");
            Program.ServerHandler.SimulationManager.Simulation.ResourceManager.SaveChunkColumn(chunkColumn);

            return null;
        }
    }
}
