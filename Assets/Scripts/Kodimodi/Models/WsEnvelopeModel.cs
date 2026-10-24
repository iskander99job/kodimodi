using MemoryPack;

namespace Kodimodi.Models
{
    [MemoryPackable]
    public partial class WsEnvelopeModel
    {
        public WsMessageType Type { get; set; }
        public byte[] Payload { get; set; }
    }
    
    public enum WsMessageType : byte
    {
        BattlePingRequest = 1,
        BattlePingResponse = 2,
        PlayerBodyPosition = 3,
    }
}