using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class UnitAlatService
    {
        private readonly Database db;
        private readonly LogAktivitasService log;

        public UnitAlatService(Database _db, LogAktivitasService _log)
        {
            db = _db;
            log = _log;
        }

        private const string SelectQuery = @"
                SELECT ua.id, ua.id_alat, a.nama AS nama_alat,
                       ua.id_status_ketersediaan, sk.nama AS status_ketersediaan,
                       ua.kode_inventaris,
                       ua.id_status_barang, sb.nama AS status_barang,
                       ua.created_at
                FROM unit_alat ua
                JOIN alat a ON a.id = ua.id_alat
                JOIN status_ketersediaan sk ON sk.id = ua.id_status_ketersediaan
                JOIN status_barang sb ON sb.id = ua.id_status_barang";

        //GET ALL UNIT ALAT
        public async Task<PagedResult<UnitAlat>> GetAllUnitAlat(int page, int pageSize, uint? idAlat, uint? idStatusKetersediaan)
        {
            (page, pageSize) = Pagination.Normalize(page, pageSize);
            using var conn = db.connect();

            const string countQuery = @"
                SELECT COUNT(*)
                FROM unit_alat ua
                WHERE (@IdAlat IS NULL OR ua.id_alat = @IdAlat)
                  AND (@IdStatusKetersediaan IS NULL OR ua.id_status_ketersediaan = @IdStatusKetersediaan)";

            var query = SelectQuery + @"
                WHERE (@IdAlat IS NULL OR ua.id_alat = @IdAlat)
                  AND (@IdStatusKetersediaan IS NULL OR ua.id_status_ketersediaan = @IdStatusKetersediaan)
                ORDER BY ua.kode_inventaris
                LIMIT @Limit OFFSET @Offset";

            var param = new { IdAlat = idAlat, IdStatusKetersediaan = idStatusKetersediaan, Limit = pageSize, Offset = (page - 1) * pageSize };

            var total = await conn.ExecuteScalarAsync<long>(countQuery, param);
            var result = await conn.QueryAsync<UnitAlat>(query, param);

            return new PagedResult<UnitAlat> { Items = result.ToList(), Page = page, PageSize = pageSize, Total = total };
        }

        //GET UNIT ALAT BY ID
        public async Task<UnitAlat> GetUnitAlatById(uint id)
        {
            using var conn = db.connect();

            var query = SelectQuery + @"
                WHERE ua.id = @Id
                LIMIT 1";

            var result = await conn.QueryFirstOrDefaultAsync<UnitAlat>(query, new { Id = id });

            if (result == null)
            {
                throw new AppException(404, "Unit alat not found");
            }

            return result;
        }

        //CREATE (A NEW UNIT ALWAYS STARTS AS 'tersedia')
        public async Task<bool> CreateUnitAlat(UnitAlatRequest request, uint idUser)
        {
            using var conn = db.connect();

            const string checkAlat = @"
                SELECT COUNT(*) FROM alat WHERE id = @IdAlat";

            var alatExists = await conn.ExecuteScalarAsync<long>(checkAlat, new { IdAlat = request.IdAlat });

            if (alatExists == 0)
            {
                throw new AppException(404, "Alat not found");
            }

            const string checkKode = @"
                SELECT COUNT(*) FROM unit_alat WHERE kode_inventaris = @KodeInventaris";

            var kodeExists = await conn.ExecuteScalarAsync<long>(checkKode, new { KodeInventaris = request.KodeInventaris.Trim() });

            if (kodeExists > 0)
            {
                throw new AppException(409, "kodeInventaris is already used");
            }

            const string statusQuery = @"
                SELECT id FROM status_ketersediaan WHERE nama = 'tersedia'";

            var idTersedia = await conn.ExecuteScalarAsync<uint>(statusQuery);

            const string query = @"
                INSERT INTO unit_alat (id_alat, id_status_ketersediaan, kode_inventaris, id_status_barang)
                VALUES (@IdAlat, @IdStatusKetersediaan, @KodeInventaris, @IdStatusBarang)";

            var result = await conn.ExecuteAsync(query, new
            {
                IdAlat = request.IdAlat,
                IdStatusKetersediaan = idTersedia,
                KodeInventaris = request.KodeInventaris.Trim(),
                IdStatusBarang = request.IdStatusBarang
            });

            if (result > 0)
            {
                await log.Write(idUser, $"Menambah unit alat {request.KodeInventaris.Trim()}");
            }

            return result > 0;
        }

        //UPDATE (A UNIT THAT IS CURRENTLY BORROWED CANNOT BE CHANGED)
        public async Task<bool> UpdateUnitAlat(uint id, UnitAlatUpdateRequest request, uint idUser)
        {
            using var conn = db.connect();

            var current = await GetUnitAlatById(id);

            if (current.StatusKetersediaan == "dipinjam")
            {
                throw new AppException(409, "Unit alat is currently borrowed and cannot be changed");
            }

            const string checkKode = @"
                SELECT COUNT(*) FROM unit_alat WHERE kode_inventaris = @KodeInventaris AND id <> @Id";

            var kodeExists = await conn.ExecuteScalarAsync<long>(checkKode, new { KodeInventaris = request.KodeInventaris.Trim(), Id = id });

            if (kodeExists > 0)
            {
                throw new AppException(409, "kodeInventaris is already used");
            }

            const string query = @"
                UPDATE unit_alat
                SET kode_inventaris = @KodeInventaris,
                    id_status_barang = @IdStatusBarang,
                    id_status_ketersediaan = @IdStatusKetersediaan
                WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new
            {
                Id = id,
                KodeInventaris = request.KodeInventaris.Trim(),
                IdStatusBarang = request.IdStatusBarang,
                IdStatusKetersediaan = request.IdStatusKetersediaan
            });

            if (result > 0)
            {
                await log.Write(idUser, $"Mengubah unit alat {current.KodeInventaris}");
            }

            return result > 0;
        }

        //DELETE
        public async Task<bool> DeleteUnitAlat(uint id, uint idUser)
        {
            using var conn = db.connect();

            var current = await GetUnitAlatById(id);

            const string checkQuery = @"
                SELECT (SELECT COUNT(*) FROM detail_unit_peminjaman WHERE id_unit_alat = @Id)
                     + (SELECT COUNT(*) FROM detail_pengembalian WHERE id_unit_alat = @Id)";

            var referenced = await conn.ExecuteScalarAsync<long>(checkQuery, new { Id = id });

            if (referenced > 0)
            {
                throw new AppException(409, "Unit alat has loan history and cannot be deleted");
            }

            const string query = @"
                DELETE FROM unit_alat WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new { Id = id });

            if (result > 0)
            {
                await log.Write(idUser, $"Menghapus unit alat {current.KodeInventaris}");
            }

            return result > 0;
        }
    }
}
