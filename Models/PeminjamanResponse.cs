namespace CampGearBackend.Models
{
    public class PeminjamanResponse
    {
        public uint Id { get; set; }
        public uint IdPeminjam { get; set; }
        public string? Peminjam { get; set; }
        public uint? IdPetugas { get; set; }
        public string? Petugas { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateOnly TanggalPeminjaman { get; set; }
        public DateOnly TanggalPengembalian { get; set; }
        public decimal TotalBiaya { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PeminjamanDetailResponse> Details { get; set; } = new();
    }

    public class PeminjamanDetailResponse
    {
        public uint Id { get; set; }
        public uint IdAlat { get; set; }
        public string? Alat { get; set; }
        public int Jumlah { get; set; }
        public decimal HargaSewaSaatTransaksi { get; set; }
        public List<uint> IdUnitAlat { get; set; } = new();
        public List<string> KodeInventaris { get; set; } = new();
    }
}
