using System.Text;
using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class LaporanService
    {
        private readonly Database db;

        public LaporanService(Database _db)
        {
            db = _db;
        }

        //LOAN REPORT FOR A DATE RANGE (AGGREGATION PER LOAN)
        public async Task<List<ReportPeminjaman>> GetLaporanPeminjaman(DateOnly from, DateOnly to)
        {
            using var conn = db.connect();

            const string query = @"
                SELECT p.id, u.nama AS peminjam, pt.nama AS petugas, sp.nama AS status,
                       p.tanggal_peminjaman, p.tanggal_pengembalian, p.total_biaya,
                       COALESCE(SUM(dp.jumlah), 0) AS jumlah_alat
                FROM peminjaman p
                JOIN users u ON u.id = p.id_peminjam
                LEFT JOIN users pt ON pt.id = p.id_petugas
                JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                LEFT JOIN detail_peminjaman dp ON dp.id_peminjaman = p.id
                WHERE p.tanggal_peminjaman >= @From AND p.tanggal_peminjaman < DATE_ADD(@To, INTERVAL 1 DAY)
                GROUP BY p.id, u.nama, pt.nama, sp.nama, p.tanggal_peminjaman, p.tanggal_pengembalian, p.total_biaya
                ORDER BY p.tanggal_peminjaman, p.id";

            var result = await conn.QueryAsync<ReportPeminjaman>(query, new { From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) });

            return result.ToList();
        }

        //RETURN REPORT FOR A DATE RANGE (JOIN + SUBQUERY PER RETURN)
        public async Task<List<ReportPengembalian>> GetLaporanPengembalian(DateOnly from, DateOnly to)
        {
            using var conn = db.connect();

            const string query = @"
                SELECT pr.id, pr.id_peminjaman, u.nama AS peminjam, pr.tanggal_kembali,
                       pr.keterlambatan_pengembalian, pr.denda,
                       (SELECT COALESCE(SUM(dp.denda_kondisi), 0)
                        FROM detail_pengembalian dp
                        WHERE dp.id_pengembalian = pr.id) AS denda_kondisi
                FROM pengembalian pr
                JOIN peminjaman p ON p.id = pr.id_peminjaman
                JOIN users u ON u.id = p.id_peminjam
                WHERE pr.tanggal_kembali >= @From AND pr.tanggal_kembali < DATE_ADD(@To, INTERVAL 1 DAY)
                ORDER BY pr.tanggal_kembali, pr.id";

            var result = await conn.QueryAsync<ReportPengembalian>(query, new { From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) });

            return result.ToList();
        }

        //CSV OUTPUT SO THE REPORT CAN BE PRINTED OR OPENED IN A SPREADSHEET
        public static string ToCsv<T>(List<T> rows)
        {
            var properties = typeof(T).GetProperties();
            var builder = new StringBuilder();

            builder.AppendLine(string.Join(";", properties.Select(p => p.Name)));

            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(";", properties.Select(p => p.GetValue(row)?.ToString() ?? "")));
            }

            return builder.ToString();
        }
    }
}
