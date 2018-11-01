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
    public static class GeneralCommands
    {
        private static Logger logger;

        static GeneralCommands()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        [Command((ushort)OfficialCommands.GetUniverse)]
        public static byte[] GetUniverse(byte[] data)
        {
            logger.Trace("Get universe");

            var universe = Program.ServerHandler.SimulationManager.GetUniverse();
            
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                universe.Serialize(writer, null);
                return memoryStream.ToArray();
            }
        }

        [Command((ushort)OfficialCommands.GetPlanet)]
        public static byte[] GetPlanet(byte[] data)
        {
            var planet = Program.ServerHandler.SimulationManager.GetPlanet(0);
            logger.Trace("Get planet");

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                planet.Serialize(writer, null);
                return memoryStream.ToArray();
            }
        }
    }
}
