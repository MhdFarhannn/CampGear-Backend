namespace CampGearBackend.Models
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public LoginUser User { get; set; } = new();
    }

    public class LoginUser
    {
        public uint Id { get; set; }
        public string Nama { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
