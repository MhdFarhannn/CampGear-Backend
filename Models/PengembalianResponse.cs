namespace CampGearBackend.Models
{
    public class PengembalianResponse
    {
        public uint Id { get; set; }
        public uint IdPeminjaman { get; set; }
        public string? Peminjam { get; set; }
        public DateOnly TanggalKembali { get; set; }
        public int KeterlambatanPengembalian { get; set; }
        public decimal Denda { get; set; }
        public string? Catatan { get; set; }
        public List<PengembalianUnitResponse> Details { get; set; } = new();
    }

    public class PengembalianUnitResponse
    {
        public uint IdUnitAlat { get; set; }
        public string? KodeInventaris { get; set; }
        public string? Alat { get; set; }
        public string? StatusKondisi { get; set; }
        public decimal DendaKondisi { get; set; }
        public string? Catatan { get; set; }
    }
}
