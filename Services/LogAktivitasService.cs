using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class LogAktivitasService
    {
        private readonly Database db;

        public LogAktivitasService(Database _db)
        {
            db = _db;
        }

        //WRITE ONE ACTIVITY ROW
        public async Task Write(uint idUser, string aktivitas)
        {
            using var conn = db.connect();

            const string query = @"
                INSERT INTO log_aktivitas (id_user, aktivitas)
                VALUES (@IdUser, @Aktivitas)";

            await conn.ExecuteAsync(query, new { IdUser = idUser, Aktivitas = aktivitas });
        }

        //GET ALL LOG AKTIVITAS
        public async Task<PagedResult<LogAktivitas>> GetAllLogAktivitas(int page, int pageSize, uint? idUser, DateOnly? from, DateOnly? to)
        {
            (page, pageSize) = Pagination.Normalize(page, pageSize);
            using var conn = db.connect();

            const string countQuery = @"
                SELECT COUNT(*)
                FROM log_aktivitas l
                WHERE (@IdUser IS NULL OR l.id_user = @IdUser)
                  AND (@From IS NULL OR l.waktu >= @From)
                  AND (@To IS NULL OR l.waktu < DATE_ADD(@To, INTERVAL 1 DAY))";

            const string query = @"
                SELECT l.id, l.id_user, u.nama AS nama_user, l.aktivitas, l.waktu
                FROM log_aktivitas l
                JOIN users u ON u.id = l.id_user
                WHERE (@IdUser IS NULL OR l.id_user = @IdUser)
                  AND (@From IS NULL OR l.waktu >= @From)
                  AND (@To IS NULL OR l.waktu < DATE_ADD(@To, INTERVAL 1 DAY))
                ORDER BY l.id DESC
                LIMIT @Limit OFFSET @Offset";

            var param = new { IdUser = idUser, From = from?.ToDateTime(TimeOnly.MinValue), To = to?.ToDateTime(TimeOnly.MinValue), Limit = pageSize, Offset = (page - 1) * pageSize };

            var total = await conn.ExecuteScalarAsync<long>(countQuery, param);
            var result = await conn.QueryAsync<LogAktivitas>(query, param);

            return new PagedResult<LogAktivitas> { Items = result.ToList(), Page = page, PageSize = pageSize, Total = total };
        }
    }
}
