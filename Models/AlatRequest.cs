using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class AlatRequest
    {
        [Range(1, uint.MaxValue, ErrorMessage = "idKategori is required")]
        public uint IdKategori { get; set; }

        [Required(ErrorMessage = "nama is required")]
        [MaxLength(150, ErrorMessage = "nama must not exceed 150 characters")]
        public string Nama { get; set; } = string.Empty;

        public string? Deskripsi { get; set; }

        [Range(0, 9999999999999.99, ErrorMessage = "hargaSewa must not be negative")]
        public decimal HargaSewa { get; set; }

        [MaxLength(255, ErrorMessage = "imagePath must not exceed 255 characters")]
        public string? ImagePath { get; set; }
    }
}
