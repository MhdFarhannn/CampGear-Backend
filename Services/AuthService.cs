using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class AuthService
    {
        private readonly Database db;
        private readonly JwtService jwt;

        public AuthService(Database _db, JwtService _jwt)
        {
            db = _db;
            jwt = _jwt;
        }

        //LOGIN
        public async Task<LoginResponse> Login(LoginRequest request)
        {
            using var conn = db.connect();

            const string query = @"
                SELECT u.id, u.nama, u.password, r.nama AS role
                FROM users u
                JOIN roles r ON r.id = u.id_role
                WHERE u.no_telepon = @NoTelepon
                LIMIT 1";

            var user = await conn.QueryFirstOrDefaultAsync<LoginUserRow>(query,
                new { NoTelepon = Normalize(request.NoTelepon) });

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new AppException(401, "Phone number or password is incorrect");
            }

            var token = jwt.CreateToken(user.Id, user.Nama, user.Role);

            const string logQuery = @"
                INSERT INTO log_aktivitas (id_user, aktivitas)
                VALUES (@IdUser, 'Login')";

            await conn.ExecuteAsync(logQuery, new { IdUser = user.Id });

            return new LoginResponse
            {
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                User = new LoginUser { Id = user.Id, Nama = user.Nama, Role = user.Role }
            };
        }

        //LOGOUT
        public async Task Logout(uint idUser)
        {
            using var conn = db.connect();

            const string query = @"
                INSERT INTO log_aktivitas (id_user, aktivitas)
                VALUES (@IdUser, 'Logout')";

            await conn.ExecuteAsync(query, new { IdUser = idUser });
        }

        //REMOVE SPACES AND HYPHENS SO LOGIN STAYS CONSISTENT WITH SAVED DATA
        public static string Normalize(string noTelepon)
        {
            return (noTelepon ?? string.Empty).Replace(" ", "").Replace("-", "");
        }

        private class LoginUserRow
        {
            public uint Id { get; set; }
            public string Nama { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }
    }
}
