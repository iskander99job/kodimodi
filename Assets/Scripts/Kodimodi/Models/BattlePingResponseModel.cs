using System;
using MemoryPack;

namespace Kodimodi.Models
{
    public partial class BattlePingResponseModel
    {
        public SpawnInfo[] Spawns { get; set; } = Array.Empty<SpawnInfo>();
        public string LivekitToken { get; set; }
        public string Error { get; set; }
    }
    
    [MemoryPackable]
    public partial class SpawnInfo
    {
        public int Id { get; set; }
        public Vec3 Position { get; set; }
    }
}