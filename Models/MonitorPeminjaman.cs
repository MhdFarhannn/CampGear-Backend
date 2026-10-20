namespace CampGearBackend.Models
{
    public class MonitorPeminjaman
    {
        public uint Id { get; set; }
        public string? Peminjam { get; set; }
        public string? NoTelepon { get; set; }
        public DateOnly TanggalPeminjaman { get; set; }
        public DateOnly TanggalPengembalian { get; set; }
        public int HariTerlambat { get; set; }
        public decimal TotalBiaya { get; set; }
    }
}
