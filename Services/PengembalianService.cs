using System.Data;
using Dapper;
using CampGearBackend.Models;
using Microsoft.Extensions.Options;

namespace CampGearBackend.Services
{
    public class PengembalianService
    {
        private readonly Database db;
        private readonly LogAktivitasService log;
        private readonly DendaSettings denda;

        public PengembalianService(Database _db, LogAktivitasService _log, IOptions<DendaSettings> _denda)
        {
            db = _db;
            log = _log;
            denda = _denda.Value;
        }

        private const string SelectQuery = @"
                SELECT pr.id, pr.id_peminjaman, u.nama AS peminjam, pr.tanggal_kembali,
                       pr.keterlambatan_pengembalian, pr.denda, pr.catatan
                FROM pengembalian pr
                JOIN peminjaman p ON p.id = pr.id_peminjaman
                JOIN users u ON u.id = p.id_peminjam";

        //GET ALL PENGEMBALIAN
        public async Task<PagedResult<PengembalianResponse>> GetAllPengembalian(int page, int pageSize, DateOnly? from, DateOnly? to)
        {
            (page, pageSize) = Pagination.Normalize(page, pageSize);
            using var conn = db.connect();

            const string filter = @"
                WHERE (@From IS NULL OR pr.tanggal_kembali >= @From)
                  AND (@To IS NULL OR pr.tanggal_kembali <= @To)";

            const string query = SelectQuery + filter + @"
                ORDER BY pr.id DESC
                LIMIT @Limit OFFSET @Offset";

            var param = new { From = from?.ToDateTime(TimeOnly.MinValue), To = to?.ToDateTime(TimeOnly.MinValue), Limit = pageSize, Offset = (page - 1) * pageSize };

            var total = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM pengembalian pr" + filter, param);
            var result = (await conn.QueryAsync<PengembalianResponse>(query, param)).ToList();
            await LoadDetails(conn, result);

            return new PagedResult<PengembalianResponse> { Items = result, Page = page, PageSize = pageSize, Total = total };
        }

        //GET PENGEMBALIAN BY ID
        public async Task<PengembalianResponse> GetPengembalianById(uint id, uint idUser, string role)
        {
            using var conn = db.connect();

            var query = SelectQuery + @"
                WHERE pr.id = @Id
                LIMIT 1";

            var result = await conn.QueryFirstOrDefaultAsync<PengembalianResponse>(query, new { Id = id });

            if (result == null)
            {
                throw new AppException(404, "Pengembalian not found");
            }

            const string ownerQuery = @"
                SELECT id_peminjam FROM peminjaman WHERE id = @IdPeminjaman";

            var idPeminjam = await conn.ExecuteScalarAsync<uint>(ownerQuery, new { IdPeminjaman = result.IdPeminjaman });

            if (role == "Peminjam" && idPeminjam != idUser)
            {
                throw new AppException(403, "You may only view your own pengembalian");
            }

            await LoadDetails(conn, new List<PengembalianResponse> { result });

            return result;
        }

        //CREATE (sp_proses_pengembalian IS CALLED ONCE PER UNIT IN ONE TRANSACTION)
        public async Task<bool> CreatePengembalian(PengembalianRequest request, uint idUser, string role)
        {
            //CHECK OWNERSHIP FIRST: THE "dipinjam" MESSAGE MUST NOT LEAK WHETHER A LOAN EXISTS
            using (var checkConn = db.connect())
            {
                const string ownerQuery = @"
                    SELECT id_peminjam FROM peminjaman WHERE id = @Id LIMIT 1";

                var idOwner = await checkConn.QueryFirstOrDefaultAsync<uint?>(ownerQuery, new { Id = request.IdPeminjaman });

                if (idOwner == null)
                {
                    throw new AppException(404, "Peminjaman not found");
                }

                if (role == "Peminjam" && idOwner != idUser)
                {
                    throw new AppException(403, "You may only return your own peminjaman");
                }
            }

            using var conn = db.connect();
            await conn.OpenAsync();
            using var trx = await conn.BeginTransactionAsync();

            try
            {
                const string loanQuery = @"
                    SELECT p.id_peminjam, p.id_status_peminjaman, p.tanggal_pengembalian, sp.nama AS status
                    FROM peminjaman p
                    JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                    WHERE p.id = @Id
                    FOR UPDATE";

                var loan = await conn.QueryFirstOrDefaultAsync<LoanRow>(loanQuery, new { Id = request.IdPeminjaman }, trx);

                if (loan == null)
                {
                    throw new AppException(404, "Peminjaman not found");
                }

                if (role == "Peminjam" && loan.IdPeminjam != idUser)
                {
                    throw new AppException(403, "You may only return your own peminjaman");
                }

                if (loan.Status != "dipinjam")
                {
                    throw new AppException(409, "Only peminjaman with status 'dipinjam' can be returned");
                }

                const string returnedQuery = @"
                    SELECT COUNT(*)
                    FROM detail_pengembalian dp
                    JOIN pengembalian pr ON pr.id = dp.id_pengembalian
                    WHERE pr.id_peminjaman = @Id";

                var alreadyReturned = await conn.ExecuteScalarAsync<long>(returnedQuery, new { Id = request.IdPeminjaman }, trx);

                if (alreadyReturned > 0)
                {
                    throw new AppException(409, "This peminjaman has already been returned");
                }

                const string unitQuery = @"
                    SELECT du.id_unit_alat
                    FROM detail_unit_peminjaman du
                    JOIN detail_peminjaman dp ON dp.id = du.id_detail_peminjaman
                    WHERE dp.id_peminjaman = @IdPeminjaman";

                var loanUnits = (await conn.QueryAsync<uint>(unitQuery, new { IdPeminjaman = request.IdPeminjaman }, trx)).ToList();

                var returnedIds = request.Units.Select(u => u.IdUnitAlat).ToList();

                if (returnedIds.Count != returnedIds.Distinct().Count())
                {
                    throw new AppException(400, "The same unit may only appear once in units");
                }

                var unknown = returnedIds.Where(u => !loanUnits.Contains(u)).ToList();
                if (unknown.Count > 0)
                {
                    throw new AppException(400, "units contain an idUnitAlat that does not belong to this peminjaman");
                }

                foreach (var unit in request.Units)
                {
                    var param = new DynamicParameters();
                    param.Add("p_id_peminjaman", request.IdPeminjaman);
                    param.Add("p_tanggal_kembali", request.TanggalKembali!.Value.ToDateTime(TimeOnly.MinValue));
                    param.Add("p_catatan", request.Catatan);
                    param.Add("p_id_unit_alat", unit.IdUnitAlat);
                    param.Add("p_status_kondisi", unit.StatusKondisi.Trim().ToLower());
                    param.Add("p_catatan_unit", unit.Catatan);
                    param.Add("p_denda_per_hari", denda.TerlambatPerHari);
                    param.Add("p_denda_baik", 0m);
                    param.Add("p_denda_rusak_ringan", denda.RusakRingan);
                    param.Add("p_denda_rusak_berat", denda.RusakBerat);
                    param.Add("p_denda_hilang", denda.Hilang);
                    param.Add("p_id_pengembalian", dbType: DbType.UInt32, direction: ParameterDirection.Output);
                    param.Add("p_total_denda", dbType: DbType.Decimal, direction: ParameterDirection.Output);

                    await conn.ExecuteAsync("sp_proses_pengembalian", param, trx, commandType: CommandType.StoredProcedure);

                    if (unit.StatusKondisi.Trim().ToLower() == "hilang")
                    {
                        const string lostQuery = @"
                            UPDATE unit_alat
                            SET id_status_barang = (SELECT id FROM status_barang WHERE nama = 'rusak_berat'),
                                id_status_ketersediaan = (SELECT id FROM status_ketersediaan WHERE nama = 'tidak_tersedia')
                            WHERE id = @Id";

                        await conn.ExecuteAsync(lostQuery, new { Id = unit.IdUnitAlat }, trx);
                    }
                }

                const string updateQuery = @"
                    UPDATE peminjaman
                    SET id_status_peminjaman = (SELECT id FROM status_peminjaman WHERE nama = 'selesai')
                    WHERE id = @Id";

                await conn.ExecuteAsync(updateQuery, new { Id = request.IdPeminjaman }, trx);

                await trx.CommitAsync();

                await log.Write(idUser, $"Memproses pengembalian peminjaman #{request.IdPeminjaman}");

                return true;
            }
            catch (AppException)
            {
                await trx.RollbackAsync();
                throw;
            }
            catch (Exception)
            {
                await trx.RollbackAsync();
                throw;
            }
        }

        private async Task LoadDetails(MySql.Data.MySqlClient.MySqlConnection conn, List<PengembalianResponse> returns)
        {
            if (returns.Count == 0)
            {
                return;
            }

            const string query = @"
                SELECT dp.id_pengembalian, ua.id AS id_unit_alat, ua.kode_inventaris, a.nama AS alat,
                       sk.nama AS status_kondisi, dp.denda_kondisi, dp.catatan
                FROM detail_pengembalian dp
                JOIN unit_alat ua ON ua.id = dp.id_unit_alat
                JOIN alat a ON a.id = ua.id_alat
                JOIN status_kondisi_kembali sk ON sk.id = dp.id_status_kondisi_kembali
                WHERE dp.id_pengembalian IN @Ids
                ORDER BY dp.id";

            var rows = await conn.QueryAsync<DetailRow>(query, new { Ids = returns.Select(r => r.Id).ToArray() });

            var byReturn = returns.ToDictionary(r => r.Id);

            foreach (var row in rows)
            {
                if (byReturn.TryGetValue(row.IdPengembalian, out var header))
                {
                    header.Details.Add(new PengembalianUnitResponse
                    {
                        IdUnitAlat = row.IdUnitAlat,
                        KodeInventaris = row.KodeInventaris,
                        Alat = row.Alat,
                        StatusKondisi = row.StatusKondisi,
                        DendaKondisi = row.DendaKondisi,
                        Catatan = row.Catatan
                    });
                }
            }
        }

        private class LoanRow
        {
            public uint IdPeminjam { get; set; }
            public uint IdStatusPeminjaman { get; set; }
            public DateOnly TanggalPengembalian { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private class DetailRow
        {
            public uint IdPengembalian { get; set; }
            public uint IdUnitAlat { get; set; }
            public string? KodeInventaris { get; set; }
            public string? Alat { get; set; }
            public string? StatusKondisi { get; set; }
            public decimal DendaKondisi { get; set; }
            public string? Catatan { get; set; }
        }
    }
}
