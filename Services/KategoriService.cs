using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class KategoriService
    {
        private readonly Database db;
        private readonly LogAktivitasService log;

        public KategoriService(Database _db, LogAktivitasService _log)
        {
            db = _db;
            log = _log;
        }

        //GET ALL KATEGORI
        public async Task<List<Kategori>> GetAllKategori()
        {
            using var conn = db.connect();

            const string query = @"
                SELECT id, nama
                FROM kategori
                ORDER BY nama";

            var result = await conn.QueryAsync<Kategori>(query);

            return result.ToList();
        }

        //GET KATEGORI BY ID
        public async Task<Kategori> GetKategoriById(uint id)
        {
            using var conn = db.connect();

            const string query = @"
                SELECT id, nama
                FROM kategori
                WHERE id = @Id
                LIMIT 1";

            var result = await conn.QueryFirstOrDefaultAsync<Kategori>(query, new { Id = id });

            if (result == null)
            {
                throw new AppException(404, "Kategori not found");
            }

            return result;
        }

        //CREATE
        public async Task<bool> CreateKategori(KategoriRequest request, uint idUser)
        {
            if (string.IsNullOrWhiteSpace(request.Nama))
            {
                throw new AppException(400, "nama is required");
            }

            using var conn = db.connect();

            const string checkQuery = @"
                SELECT COUNT(*) FROM kategori WHERE nama = @Nama";

            var exists = await conn.ExecuteScalarAsync<long>(checkQuery, new { Nama = request.Nama.Trim() });

            if (exists > 0)
            {
                throw new AppException(409, "Kategori already exists");
            }

            const string query = @"
                INSERT INTO kategori (nama)
                VALUES (@Nama)";

            var result = await conn.ExecuteAsync(query, new { Nama = request.Nama.Trim() });

            if (result > 0)
            {
                await log.Write(idUser, $"Menambah kategori {request.Nama.Trim()}");
            }

            return result > 0;
        }

        //UPDATE
        public async Task<bool> UpdateKategori(uint id, KategoriRequest request, uint idUser)
        {
            if (string.IsNullOrWhiteSpace(request.Nama))
            {
                throw new AppException(400, "nama is required");
            }

            using var conn = db.connect();

            var current = await GetKategoriById(id);

            const string checkQuery = @"
                SELECT COUNT(*) FROM kategori WHERE nama = @Nama AND id <> @Id";

            var exists = await conn.ExecuteScalarAsync<long>(checkQuery, new { Nama = request.Nama.Trim(), Id = id });

            if (exists > 0)
            {
                throw new AppException(409, "Kategori already exists");
            }

            const string query = @"
                UPDATE kategori
                SET nama = @Nama
                WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new { Id = id, Nama = request.Nama.Trim() });

            if (result > 0)
            {
                await log.Write(idUser, $"Mengubah kategori {current.Nama}");
            }

            return result > 0;
        }

        //DELETE
        public async Task<bool> DeleteKategori(uint id, uint idUser)
        {
            using var conn = db.connect();

            var current = await GetKategoriById(id);

            const string checkQuery = @"
                SELECT COUNT(*) FROM alat WHERE id_kategori = @Id";

            var referenced = await conn.ExecuteScalarAsync<long>(checkQuery, new { Id = id });

            if (referenced > 0)
            {
                throw new AppException(409, "Kategori is used by alat and cannot be deleted");
            }

            const string query = @"
                DELETE FROM kategori
                WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new { Id = id });

            if (result > 0)
            {
                await log.Write(idUser, $"Menghapus kategori {current.Nama}");
            }

            return result > 0;
        }
    }
}
