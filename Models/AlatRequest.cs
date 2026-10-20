namespace CampGearBackend.Models
{
    public class AlatRequest
    {
        public uint IdKategori { get; set; }
        public string Nama { get; set; } = string.Empty;
        public string? Deskripsi { get; set; }
        public decimal HargaSewa { get; set; }
        public string? ImagePath { get; set; }
    }
}
