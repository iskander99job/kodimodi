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
        // /battle
        BattlePingRequest = 1,
        BattlePingResponse = 2,
        PlayerBodyPosition = 3,
        Shoot = 4,
        // /lobby
        LobbyJoin = 5,    // клиент → сервер
        LobbyState = 6,   // сервер → все
        StartMatch = 7,   // хост → сервер
        CancelMatch = 8,  // хост → сервер
        RoomClosed = 9,   // сервер → все, также причина отказа при входе
        MatchStarted = 10 // сервер → все
    }
}