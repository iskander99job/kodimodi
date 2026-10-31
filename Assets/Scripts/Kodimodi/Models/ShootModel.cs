using MemoryPack;

namespace Kodimodi.Models
{
    [MemoryPackable]
    public partial class ShootModel
    {
        public int Id { get; set; }
        public int HitPlayerId { get; set; }
        public int Damage { get; set; }
    }
}