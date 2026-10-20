namespace CampGearBackend.Models
{
    public class AlatResponse
    {
        public uint Id { get; set; }
        public uint IdKategori { get; set; }
        public string? Kategori { get; set; }
        public string Nama { get; set; } = string.Empty;
        public string? Deskripsi { get; set; }
        public decimal HargaSewa { get; set; }
        public string? ImagePath { get; set; }
        public int StokTersedia { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
