using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class KategoriRequest
    {
        [Required(ErrorMessage = "nama is required")]
        [MaxLength(100, ErrorMessage = "nama must not exceed 100 characters")]
        public string Nama { get; set; } = string.Empty;
    }
}
