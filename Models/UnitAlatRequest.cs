using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class UnitAlatRequest
    {
        [Range(1, uint.MaxValue, ErrorMessage = "idAlat is required")]
        public uint IdAlat { get; set; }

        [Required(ErrorMessage = "kodeInventaris is required")]
        [MaxLength(50, ErrorMessage = "kodeInventaris must not exceed 50 characters")]
        public string KodeInventaris { get; set; } = string.Empty;

        [Range(1, uint.MaxValue, ErrorMessage = "idStatusBarang is required")]
        public uint IdStatusBarang { get; set; }
    }
}
