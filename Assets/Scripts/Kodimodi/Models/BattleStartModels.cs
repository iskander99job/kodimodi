using MemoryPack;

namespace Kodimodi.Models
{
    [MemoryPackable]
    public partial class BattleReadyModel
    {
        public bool Ready { get; set; }
    }

    [MemoryPackable]
    public partial class BattleCountdownModel
    {
        public int Version { get; set; }
        public long ServerNowUnixMs { get; set; }
        public long StartsAtUnixMs { get; set; }
    }

    [MemoryPackable]
    public partial class BattleStartedModel
    {
        public int Version { get; set; }
        public long ServerNowUnixMs { get; set; }
    }
}