namespace CampGearBackend.Models
{
    public class LogAktivitas
    {
        public uint Id { get; set; }
        public uint IdUser { get; set; }
        public string? NamaUser { get; set; }
        public string Aktivitas { get; set; } = string.Empty;
        public DateTime Waktu { get; set; }
    }
}
