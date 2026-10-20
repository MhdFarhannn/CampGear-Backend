using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "noTelepon is required")]
        public string NoTelepon { get; set; } = string.Empty;

        [Required(ErrorMessage = "password is required")]
        public string Password { get; set; } = string.Empty;
    }
}
