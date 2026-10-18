using MemoryPack;

namespace Kodimodi.Models
{
    [MemoryPackable]
    public partial class MoveMessage
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RY { get; set; }
    }
}