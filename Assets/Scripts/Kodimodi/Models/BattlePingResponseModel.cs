using System;
using MemoryPack;

namespace Kodimodi.Models
{
    [MemoryPackable]
    public partial class BattlePingResponseModel
    {
        public SpawnInfo[] Spawns { get; set; } = Array.Empty<SpawnInfo>();
    }

    [MemoryPackable]
    public partial class SpawnInfo
    {
        public int Id { get; set; }
        public Vec3 Position { get; set; }
    }
}