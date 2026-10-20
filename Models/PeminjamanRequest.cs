using System.ComponentModel.DataAnnotations;

namespace CampGearBackend.Models
{
    public class PeminjamanRequest
    {
        [Required(ErrorMessage = "tanggalPeminjaman is required")]
        public DateOnly? TanggalPeminjaman { get; set; }

        [Required(ErrorMessage = "tanggalPengembalian is required")]
        public DateOnly? TanggalPengembalian { get; set; }

        [Required(ErrorMessage = "items is required")]
        [MinLength(1, ErrorMessage = "items must contain at least one alat")]
        public List<PeminjamanItemRequest> Items { get; set; } = new();
    }

    public class PeminjamanItemRequest
    {
        [Range(1, uint.MaxValue, ErrorMessage = "idAlat is required")]
        public uint IdAlat { get; set; }

        [Range(1, 1000, ErrorMessage = "jumlah must be between 1 and 1000")]
        public int Jumlah { get; set; }
    }
}
