namespace CampGearBackend.Models
{
    public class UserResponse
    {
        public uint Id { get; set; }
        public string Nama { get; set; } = string.Empty;
        public string? NoTelepon { get; set; }
        public string? Alamat { get; set; }
        public uint IdRole { get; set; }
        public string? Role { get; set; }
        public uint? IdJenisKelamin { get; set; }
        public string? JenisKelamin { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
