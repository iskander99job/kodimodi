using MemoryPack;

namespace Kodimodi.Models
{
    [MemoryPackable]
    public partial class MatchmakingRequestModel
    {
        public string Action { get; set; }
        public int Count { get; set; }
    }
}