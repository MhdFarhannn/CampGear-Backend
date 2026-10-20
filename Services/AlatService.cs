using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class AlatService
    {
        private readonly Database db;
        private readonly LogAktivitasService log;

        public AlatService(Database _db, LogAktivitasService _log)
        {
            db = _db;
            log = _log;
        }

        //AVAILABLE STOCK = UNITS THAT ARE 'tersedia' AND NOT 'rusak_berat'
        private const string StockJoin = @"
                LEFT JOIN (
                    SELECT ua.id_alat, COUNT(*) AS stok
                    FROM unit_alat ua
                    JOIN status_ketersediaan sk ON sk.id = ua.id_status_ketersediaan
                    JOIN status_barang sb ON sb.id = ua.id_status_barang
                    WHERE sk.nama = 'tersedia' AND sb.nama <> 'rusak_berat'
                    GROUP BY ua.id_alat
                ) s ON s.id_alat = a.id";

        //GET ALL ALAT
        public async Task<PagedResult<AlatResponse>> GetAllAlat(int page, int pageSize, string? search, uint? idKategori)
        {
            (page, pageSize) = Pagination.Normalize(page, pageSize);
            using var conn = db.connect();

            const string countQuery = @"
                SELECT COUNT(*)
                FROM alat a
                WHERE (@Search IS NULL OR a.nama LIKE CONCAT('%', @Search, '%'))
                  AND (@IdKategori IS NULL OR a.id_kategori = @IdKategori)";

            var query = @"
                SELECT a.id, a.id_kategori, k.nama AS kategori, a.nama, a.deskripsi,
                       a.harga_sewa, a.image_path, COALESCE(s.stok, 0) AS stok_tersedia, a.created_at
                FROM alat a
                JOIN kategori k ON k.id = a.id_kategori" + StockJoin + @"
                WHERE (@Search IS NULL OR a.nama LIKE CONCAT('%', @Search, '%'))
                  AND (@IdKategori IS NULL OR a.id_kategori = @IdKategori)
                ORDER BY a.nama
                LIMIT @Limit OFFSET @Offset";

            var param = new { Search = Pagination.Search(search), IdKategori = idKategori, Limit = pageSize, Offset = (page - 1) * pageSize };

            var total = await conn.ExecuteScalarAsync<long>(countQuery, param);
            var result = await conn.QueryAsync<AlatResponse>(query, param);

            return new PagedResult<AlatResponse> { Items = result.ToList(), Page = page, PageSize = pageSize, Total = total };
        }

        //GET ALAT BY ID
        public async Task<AlatResponse> GetAlatById(uint id)
        {
            using var conn = db.connect();

            var query = @"
                SELECT a.id, a.id_kategori, k.nama AS kategori, a.nama, a.deskripsi,
                       a.harga_sewa, a.image_path, COALESCE(s.stok, 0) AS stok_tersedia, a.created_at
                FROM alat a
                JOIN kategori k ON k.id = a.id_kategori" + StockJoin + @"
                WHERE a.id = @Id
                LIMIT 1";

            var result = await conn.QueryFirstOrDefaultAsync<AlatResponse>(query, new { Id = id });

            if (result == null)
            {
                throw new AppException(404, "Alat not found");
            }

            return result;
        }

        //CREATE
        public async Task<bool> CreateAlat(AlatRequest request, uint idUser)
        {
            using var conn = db.connect();

            const string checkKategori = @"
                SELECT COUNT(*) FROM kategori WHERE id = @IdKategori";

            var kategoriExists = await conn.ExecuteScalarAsync<long>(checkKategori, new { IdKategori = request.IdKategori });

            if (kategoriExists == 0)
            {
                throw new AppException(404, "Kategori not found");
            }

            const string query = @"
                INSERT INTO alat (id_kategori, nama, deskripsi, harga_sewa, image_path)
                VALUES (@IdKategori, @Nama, @Deskripsi, @HargaSewa, @ImagePath)";

            var result = await conn.ExecuteAsync(query, request);

            if (result > 0)
            {
                await log.Write(idUser, $"Menambah alat {request.Nama}");
            }

            return result > 0;
        }

        //UPDATE
        public async Task<bool> UpdateAlat(uint id, AlatRequest request, uint idUser)
        {
            using var conn = db.connect();

            var current = await GetAlatById(id);

            const string checkKategori = @"
                SELECT COUNT(*) FROM kategori WHERE id = @IdKategori";

            var kategoriExists = await conn.ExecuteScalarAsync<long>(checkKategori, new { IdKategori = request.IdKategori });

            if (kategoriExists == 0)
            {
                throw new AppException(404, "Kategori not found");
            }

            const string query = @"
                UPDATE alat
                SET id_kategori = @IdKategori,
                    nama = @Nama,
                    deskripsi = @Deskripsi,
                    harga_sewa = @HargaSewa,
                    image_path = @ImagePath
                WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new
            {
                Id = id,
                IdKategori = request.IdKategori,
                Nama = request.Nama,
                Deskripsi = request.Deskripsi,
                HargaSewa = request.HargaSewa,
                ImagePath = request.ImagePath
            });

            if (result > 0)
            {
                await log.Write(idUser, $"Mengubah alat {current.Nama}");
            }

            return result > 0;
        }

        //DELETE
        public async Task<bool> DeleteAlat(uint id, uint idUser)
        {
            using var conn = db.connect();

            var current = await GetAlatById(id);

            const string checkQuery = @"
                SELECT (SELECT COUNT(*) FROM unit_alat WHERE id_alat = @Id)
                     + (SELECT COUNT(*) FROM detail_peminjaman WHERE id_alat = @Id)";

            var referenced = await conn.ExecuteScalarAsync<long>(checkQuery, new { Id = id });

            if (referenced > 0)
            {
                throw new AppException(409, "Alat still has units or loan history and cannot be deleted");
            }

            const string query = @"
                DELETE FROM alat WHERE id = @Id";

            var result = await conn.ExecuteAsync(query, new { Id = id });

            if (result > 0)
            {
                await log.Write(idUser, $"Menghapus alat {current.Nama}");
            }

            return result > 0;
        }
    }
}
