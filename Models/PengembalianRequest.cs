using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class PengembalianRequest
    {
        [Range(1, uint.MaxValue, ErrorMessage = "idPeminjaman is required")]
        public uint IdPeminjaman { get; set; }

        [Required(ErrorMessage = "tanggalKembali is required")]
        public DateOnly? TanggalKembali { get; set; }

        public string? Catatan { get; set; }

        [Required(ErrorMessage = "units is required")]
        [MinLength(1, ErrorMessage = "units must contain at least one unit")]
        public List<PengembalianUnitRequest> Units { get; set; } = new();
    }

    public class PengembalianUnitRequest
    {
        [Range(1, uint.MaxValue, ErrorMessage = "idUnitAlat is required")]
        public uint IdUnitAlat { get; set; }

        [Required(ErrorMessage = "statusKondisi is required")]
        public string StatusKondisi { get; set; } = string.Empty;

        public string? Catatan { get; set; }
    }
}
