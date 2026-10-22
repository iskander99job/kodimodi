using MemoryPack;

namespace Kodimodi.Models
{
    [MemoryPackable]
    public partial class MatchmakingResponseModel
    {
        public string RoomId { get; set; }
    }
}