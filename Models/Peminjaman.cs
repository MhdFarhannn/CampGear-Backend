namespace CampGearBackend.Models
{
    public class Peminjaman
    {
        public uint Id { get; set; }
        public uint IdPeminjam { get; set; }
        public uint? IdPetugas { get; set; }
        public uint IdStatusPeminjaman { get; set; }
        public string? Status { get; set; }
        public DateOnly TanggalPeminjaman { get; set; }
        public DateOnly TanggalPengembalian { get; set; }
        public decimal TotalBiaya { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
