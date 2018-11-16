using NLog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OctoAwesome.Network
{
    public class PackageManager
    {
        private readonly Logger logger;

        public PackageManager()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        public void SendPackage(Package package, BaseClient client)
        {
            logger.Trace($"Send Package to client id= {package.UId} Command= {package.Command} PayloadSize={package.Payload.Length}");
            byte[] bytes = new byte[package.Payload.Length + Package.HEAD_LENGTH];
            package.SerializePackage(bytes);
            client.SendAsync(bytes, bytes.Length);
        }
    }
}
