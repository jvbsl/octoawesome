using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OctoAwesome
{
    public class Awaiter
    {
        public ISerializable Serializable { get; set; }
        public bool Timeout { get; private set; }
        public uint Uid { get; set; }

        private readonly ManualResetEventSlim manualReset;
        private readonly Logger logger;
        private bool deserialized;


        public Awaiter()
        {
            manualReset = new ManualResetEventSlim(false);
            logger = LogManager.GetCurrentClassLogger();
        }

        public ISerializable WaitOn()
        {
            while (!deserialized)
            {
                Timeout = !manualReset.Wait(10000);

                if (Timeout)
                    logger.Error("Timeout in Awaiter for Id = " + Uid, new TimeoutException());
            }


            return Serializable;
        }


        public void SetResult(ISerializable serializable)
        {
            Serializable = serializable;
            deserialized = true;
            manualReset.Set();
        }

        public void SetResult(byte[] bytes, IDefinitionManager definitionManager)
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                Serializable.Deserialize(reader, definitionManager);
            }
            deserialized = true;
            manualReset.Set();
        }       
    }
}
