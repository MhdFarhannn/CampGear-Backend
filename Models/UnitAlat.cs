namespace CampGearBackend.Models
{
    public class UnitAlat
    {
        public uint Id { get; set; }
        public uint IdAlat { get; set; }
        public string? NamaAlat { get; set; }
        public uint IdStatusKetersediaan { get; set; }
        public string? StatusKetersediaan { get; set; }
        public string KodeInventaris { get; set; } = string.Empty;
        public uint IdStatusBarang { get; set; }
        public string? StatusBarang { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
