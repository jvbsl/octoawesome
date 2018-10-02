using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoAwesome
{
    public class SerializableCollection<T>: List<T>, ISerializable where T : ISerializable
    {
        public void Deserialize(BinaryReader reader, IDefinitionManager definitionManager)
        {
            foreach (var item in this)
                item.Deserialize(reader, definitionManager);
            
        }
        public void Serialize(BinaryWriter writer, IDefinitionManager definitionManager)
        {
            foreach (var item in this)
                item.Serialize(writer, definitionManager);

        }
    }
}
