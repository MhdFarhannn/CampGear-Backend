using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class UnitAlatUpdateRequest
    {
        [Required(ErrorMessage = "kodeInventaris is required")]
        [MaxLength(50, ErrorMessage = "kodeInventaris must not exceed 50 characters")]
        public string KodeInventaris { get; set; } = string.Empty;

        [Range(1, uint.MaxValue, ErrorMessage = "idStatusBarang is required")]
        public uint IdStatusBarang { get; set; }

        [Range(1, uint.MaxValue, ErrorMessage = "idStatusKetersediaan is required")]
        public uint IdStatusKetersediaan { get; set; }
    }
}
