using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class StatusBarangServices
    {
        private readonly Database db;

        public StatusBarangServices(Database _db)
        {
            db = _db;
        }

        //CREATE
        public async Task<bool> CreateStatusBarang(StatusBarang statusBarang)
        {
            using var conn = db.connect();

            const string query = @"
                INSERT INTO status_barang (nama)
                VALUES (@Nama)";

            var result = await conn.ExecuteAsync(query, statusBarang);

            return result > 0;
        }

        //DELETE
        public async Task<bool> DeleteStatusBarang(int id)
        {
            using var conn = db.connect();

            const string query = @"
                DELETE FROM status_barang
                WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new { Id = id });

            return result > 0;
        }

        //Patch
        public async Task<bool> PatchStatusBarang(int id, StatusBarang statusBarang)
        {
            using var conn = db.connect();

            const string query = @"
                UPDATE status_barang
                SET nama = @Nama
                WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new { Id = id, Nama = statusBarang.Nama });

            return result > 0;
        }

        //GET ALL STATUS BARANG
        public async Task<List<StatusBarang>> GetAllStatusBarang()
        {
            using var conn = db.connect();

            const string query = @"
                SELECT * FROM status_barang";

            var result = await conn.QueryAsync<StatusBarang>(query);

            return result.ToList();
        }
    }
}