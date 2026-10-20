using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class UsersService
    {
        private readonly Database db;
        private readonly LogAktivitasService log;

        public UsersService(Database _db, LogAktivitasService _log)
        {
            db = _db;
            log = _log;
        }

        //GET ALL USERS
        public async Task<PagedResult<UserResponse>> GetAllUsers(int page, int pageSize, string? search, uint? idRole)
        {
            (page, pageSize) = Pagination.Normalize(page, pageSize);
            using var conn = db.connect();

            const string countQuery = @"
                SELECT COUNT(*)
                FROM users u
                WHERE (@Search IS NULL OR u.nama LIKE CONCAT('%', @Search, '%') OR u.no_telepon LIKE CONCAT('%', @Search, '%'))
                  AND (@IdRole IS NULL OR u.id_role = @IdRole)";

            const string query = @"
                SELECT u.id, u.nama, u.no_telepon, u.alamat, u.id_role,
                       r.nama AS role, u.id_jenis_kelamin,
                       jk.nama AS jenis_kelamin, u.created_at
                FROM users u
                JOIN roles r ON r.id = u.id_role
                LEFT JOIN jenis_kelamin jk ON jk.id = u.id_jenis_kelamin
                WHERE (@Search IS NULL OR u.nama LIKE CONCAT('%', @Search, '%') OR u.no_telepon LIKE CONCAT('%', @Search, '%'))
                  AND (@IdRole IS NULL OR u.id_role = @IdRole)
                ORDER BY u.id
                LIMIT @Limit OFFSET @Offset";

            var param = new { Search = Pagination.Search(search), IdRole = idRole, Limit = pageSize, Offset = (page - 1) * pageSize };

            var total = await conn.ExecuteScalarAsync<long>(countQuery, param);
            var result = await conn.QueryAsync<UserResponse>(query, param);

            return new PagedResult<UserResponse> { Items = result.ToList(), Page = page, PageSize = pageSize, Total = total };
        }

        //GET USER BY ID
        public async Task<UserResponse> GetUserById(uint id)
        {
            using var conn = db.connect();

            const string query = @"
                SELECT u.id, u.nama, u.no_telepon, u.alamat, u.id_role,
                       r.nama AS role, u.id_jenis_kelamin,
                       jk.nama AS jenis_kelamin, u.created_at
                FROM users u
                JOIN roles r ON r.id = u.id_role
                LEFT JOIN jenis_kelamin jk ON jk.id = u.id_jenis_kelamin
                WHERE u.id = @Id
                LIMIT 1";

            var result = await conn.QueryFirstOrDefaultAsync<UserResponse>(query, new { Id = id });

            if (result == null)
            {
                throw new AppException(404, "User not found");
            }

            return result;
        }

        //CREATE
        public async Task<bool> CreateUser(UserRequest request, uint idAdmin)
        {
            using var conn = db.connect();

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new AppException(400, "password is required");
            }

            var noTelepon = AuthService.Normalize(request.NoTelepon);

            const string checkQuery = @"
                SELECT COUNT(*) FROM users WHERE no_telepon = @NoTelepon";

            var exists = await conn.ExecuteScalarAsync<long>(checkQuery, new { NoTelepon = noTelepon });

            if (exists > 0)
            {
                throw new AppException(409, "noTelepon is already registered");
            }

            const string query = @"
                INSERT INTO users (id_role, id_jenis_kelamin, nama, password, no_telepon, alamat)
                VALUES (@IdRole, @IdJenisKelamin, @Nama, @Password, @NoTelepon, @Alamat)";

            var result = await conn.ExecuteAsync(query, new
            {
                IdRole = request.IdRole,
                IdJenisKelamin = request.IdJenisKelamin,
                Nama = request.Nama,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                NoTelepon = noTelepon,
                Alamat = request.Alamat
            });

            if (result > 0)
            {
                await log.Write(idAdmin, $"Menambah user {request.Nama}");
            }

            return result > 0;
        }

        //UPDATE
        public async Task<bool> UpdateUser(uint id, UserRequest request, uint idAdmin)
        {
            using var conn = db.connect();

            var current = await GetUserById(id);

            var noTelepon = AuthService.Normalize(request.NoTelepon);

            const string checkQuery = @"
                SELECT COUNT(*) FROM users WHERE no_telepon = @NoTelepon AND id <> @Id";

            var exists = await conn.ExecuteScalarAsync<long>(checkQuery, new { NoTelepon = noTelepon, Id = id });

            if (exists > 0)
            {
                throw new AppException(409, "noTelepon is already registered");
            }

            const string query = @"
                UPDATE users
                SET id_role = @IdRole,
                    id_jenis_kelamin = @IdJenisKelamin,
                    nama = @Nama,
                    no_telepon = @NoTelepon,
                    alamat = @Alamat,
                    password = COALESCE(@Password, password)
                WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new
            {
                Id = id,
                IdRole = request.IdRole,
                IdJenisKelamin = request.IdJenisKelamin,
                Nama = request.Nama,
                NoTelepon = noTelepon,
                Alamat = request.Alamat,
                Password = string.IsNullOrWhiteSpace(request.Password)
                    ? null
                    : BCrypt.Net.BCrypt.HashPassword(request.Password)
            });

            if (result > 0)
            {
                await log.Write(idAdmin, $"Mengubah user {current.Nama}");
            }

            return result > 0;
        }

        //DELETE
        public async Task<bool> DeleteUser(uint id, uint idAdmin)
        {
            using var conn = db.connect();

            var current = await GetUserById(id);

            const string checkQuery = @"
                SELECT (SELECT COUNT(*) FROM peminjaman WHERE id_peminjam = @Id OR id_petugas = @Id)
                     + (SELECT COUNT(*) FROM log_aktivitas WHERE id_user = @Id)";

            var referenced = await conn.ExecuteScalarAsync<long>(checkQuery, new { Id = id });

            if (referenced > 0)
            {
                throw new AppException(409, "User is referenced by loan or activity data and cannot be deleted");
            }

            const string query = @"
                DELETE FROM users WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new { Id = id });

            if (result > 0)
            {
                await log.Write(idAdmin, $"Menghapus user {current.Nama}");
            }

            return result > 0;
        }
    }
}
