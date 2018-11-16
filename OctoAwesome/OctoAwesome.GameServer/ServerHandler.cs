using CommandManagementSystem;
using NLog;
using OctoAwesome.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OctoAwesome.GameServer
{
    public class ServerHandler
    {
        public SimulationManager SimulationManager { get; set; }

        private readonly Logger logger;
        private readonly Server server;
        private readonly PackageManager packageManager;
        private readonly DefaultCommandManager<ushort, byte[], byte[]> defaultManager;

        public ServerHandler()
        {
            logger = LogManager.GetCurrentClassLogger();

            server = new Server();
            SimulationManager = new SimulationManager(new Settings());
            packageManager = new PackageManager();
            defaultManager = new DefaultCommandManager<ushort, byte[], byte[]>(typeof(ServerHandler).Namespace + ".Commands");
        }

        public void Start()
        {

            logger.Debug("Start server handler");
            server.Start(IPAddress.Any, 8888);
            server.OnClientConnected += ServerOnClientConnected;
        }

        private void PackageManagerPackageAvailable(object sender, OctoPackageAvailableEventArgs e)
        {
            if (e.Package.Command == 0 && e.Package.Payload.Length == 0)
            {
                logger.Error("Received a null package");
                return;
            }
            logger.Trace($"Received a new Package ID = {e.Package.UId} Command = {e.Package.Command} PayloadSize = {e.Package.Payload.Length}");
            try
            {
                e.Package.Payload = defaultManager.Dispatch(e.Package.Command, e.Package.Payload) ?? new byte[0];
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Dispatch failed in Command " + e.Package.Command);
                return;
            }

            packageManager.SendPackage(e.Package, e.BaseClient);
        }

        private void ServerOnClientConnected(object sender, ConnectedClient e)
        {
            logger.Debug("Hurra ein neuer Spieler");
            e.PackageAvailable += PackageManagerPackageAvailable;
        }

    }
}
