namespace CampGearBackend.Models
{
    public class User
    {
        public uint Id { get; set; }
        public uint IdRole { get; set; }
        public uint? IdJenisKelamin { get; set; }
        public string Nama { get; set; } = string.Empty;
        public string NoTelepon { get; set; } = string.Empty;
        public string? Alamat { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
