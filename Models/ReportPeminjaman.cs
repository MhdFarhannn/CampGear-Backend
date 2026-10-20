namespace CampGearBackend.Models
{
    public class ReportPeminjaman
    {
        public uint Id { get; set; }
        public string? Peminjam { get; set; }
        public string? Petugas { get; set; }
        public string? Status { get; set; }
        public DateOnly TanggalPeminjaman { get; set; }
        public DateOnly TanggalPengembalian { get; set; }
        public decimal TotalBiaya { get; set; }
        public int JumlahAlat { get; set; }
    }

    public class ReportPengembalian
    {
        public uint Id { get; set; }
        public uint IdPeminjaman { get; set; }
        public string? Peminjam { get; set; }
        public DateOnly TanggalKembali { get; set; }
        public int KeterlambatanPengembalian { get; set; }
        public decimal Denda { get; set; }
        public decimal DendaKondisi { get; set; }
    }
}
