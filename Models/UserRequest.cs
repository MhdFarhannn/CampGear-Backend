using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class UserRequest
    {
        [Range(1, uint.MaxValue, ErrorMessage = "idRole is required")]
        public uint IdRole { get; set; }

        public uint? IdJenisKelamin { get; set; }

        [Required(ErrorMessage = "nama is required")]
        [MaxLength(100, ErrorMessage = "nama must not exceed 100 characters")]
        public string Nama { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "password must not exceed 100 characters")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "noTelepon is required")]
        [MaxLength(20, ErrorMessage = "noTelepon must not exceed 20 characters")]
        public string NoTelepon { get; set; } = string.Empty;

        public string? Alamat { get; set; }
    }
}
