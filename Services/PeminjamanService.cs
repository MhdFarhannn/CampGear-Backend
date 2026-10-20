using Dapper;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class PeminjamanService
    {
        private readonly Database db;
        private readonly LogAktivitasService log;

        public PeminjamanService(Database _db, LogAktivitasService _log)
        {
            db = _db;
            log = _log;
        }

        private const string SelectQuery = @"
                SELECT p.id, p.id_peminjam, u.nama AS peminjam, p.id_petugas, pt.nama AS petugas,
                       p.id_status_peminjaman, sp.nama AS status,
                       p.tanggal_peminjaman, p.tanggal_pengembalian, p.total_biaya, p.created_at
                FROM peminjaman p
                JOIN users u ON u.id = p.id_peminjam
                LEFT JOIN users pt ON pt.id = p.id_petugas
                JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman";

        //GET ALL PEMINJAMAN (ADMIN + PETUGAS)
        public async Task<PagedResult<PeminjamanResponse>> GetAllPeminjaman(int page, int pageSize, string? status,
            uint? idPeminjam, DateOnly? from, DateOnly? to)
        {
            (page, pageSize) = Pagination.Normalize(page, pageSize);
            using var conn = db.connect();

            var filter = @"
                WHERE (@Status IS NULL OR sp.nama = @Status)
                  AND (@IdPeminjam IS NULL OR p.id_peminjam = @IdPeminjam)
                  AND (@From IS NULL OR p.tanggal_peminjaman >= @From)
                  AND (@To IS NULL OR p.tanggal_peminjaman <= @To)";

            var param = new
            {
                Status = Pagination.Search(status),
                IdPeminjam = idPeminjam,
                From = from?.ToDateTime(TimeOnly.MinValue),
                To = to?.ToDateTime(TimeOnly.MinValue),
                Limit = pageSize,
                Offset = (page - 1) * pageSize
            };

            var total = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM peminjaman p JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman" + filter, param);

            var query = SelectQuery + filter + @"
                ORDER BY p.id DESC
                LIMIT @Limit OFFSET @Offset";

            var result = (await conn.QueryAsync<PeminjamanResponse>(query, param)).ToList();
            await LoadDetails(conn, result);

            return new PagedResult<PeminjamanResponse> { Items = result, Page = page, PageSize = pageSize, Total = total };
        }

        //GET ALL PEMINJAMAN OF THE LOGGED IN PEMINJAM
        public async Task<PagedResult<PeminjamanResponse>> GetMyPeminjaman(uint idPeminjam, int page, int pageSize, string? status)
        {
            return await GetAllPeminjaman(page, pageSize, status, idPeminjam, null, null);
        }

        //GET PEMINJAMAN BY ID (PEMINJAM MAY ONLY READ THEIR OWN LOAN)
        public async Task<PeminjamanResponse> GetPeminjamanById(uint id, uint idUser, string role)
        {
            using var conn = db.connect();

            var query = SelectQuery + @"
                WHERE p.id = @Id
                LIMIT 1";

            var result = await conn.QueryFirstOrDefaultAsync<PeminjamanResponse>(query, new { Id = id });

            if (result == null)
            {
                throw new AppException(404, "Peminjaman not found");
            }

            if (role == "Peminjam" && result.IdPeminjam != idUser)
            {
                throw new AppException(403, "You may only view your own peminjaman");
            }

            await LoadDetails(conn, new List<PeminjamanResponse> { result });

            return result;
        }

        //CREATE (STATUS = 'diajukan', ALL ITEMS ARE SAVED IN ONE TRANSACTION)
        public async Task<bool> CreatePeminjaman(PeminjamanRequest request, uint idPeminjam)
        {
            var tanggalPeminjaman = request.TanggalPeminjaman!.Value;
            var tanggalPengembalian = request.TanggalPengembalian!.Value;

            if (tanggalPengembalian < tanggalPeminjaman)
            {
                throw new AppException(400, "tanggalPengembalian must not be before tanggalPeminjaman");
            }

            var duplicate = request.Items.GroupBy(i => i.IdAlat).Any(g => g.Count() > 1);
            if (duplicate)
            {
                throw new AppException(400, "Each alat may only appear once in items");
            }

            using var conn = db.connect();
            await conn.OpenAsync();
            using var trx = await conn.BeginTransactionAsync();

            try
            {
                const string insertQuery = @"
                    INSERT INTO peminjaman (id_peminjam, id_status_peminjaman, tanggal_peminjaman, tanggal_pengembalian, total_biaya)
                    VALUES (@IdPeminjam, (SELECT id FROM status_peminjaman WHERE nama = 'diajukan'),
                            @TanggalPeminjaman, @TanggalPengembalian, 0)";

                uint idPeminjaman;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trx;
                    cmd.CommandText = insertQuery;
                    cmd.Parameters.AddWithValue("@IdPeminjam", idPeminjam);
                    cmd.Parameters.AddWithValue("@TanggalPeminjaman", tanggalPeminjaman.ToDateTime(TimeOnly.MinValue));
                    cmd.Parameters.AddWithValue("@TanggalPengembalian", tanggalPengembalian.ToDateTime(TimeOnly.MinValue));

                    await cmd.ExecuteNonQueryAsync();
                    idPeminjaman = (uint)cmd.LastInsertedId;
                }

                decimal totalBiaya = 0;

                const string detailQuery = @"
                    INSERT INTO detail_peminjaman (id_peminjaman, id_alat, jumlah, harga_sewa_saat_transaksi)
                    SELECT @IdPeminjaman, a.id, @Jumlah, a.harga_sewa
                    FROM alat a
                    WHERE a.id = @IdAlat";

                foreach (var item in request.Items)
                {
                    var inserted = await conn.ExecuteAsync(detailQuery, new
                    {
                        IdPeminjaman = idPeminjaman,
                        IdAlat = item.IdAlat,
                        Jumlah = item.Jumlah
                    }, trx);

                    if (inserted == 0)
                    {
                        throw new AppException(404, $"Alat #{item.IdAlat} not found");
                    }

                    totalBiaya += await conn.ExecuteScalarAsync<decimal>(@"
                        SELECT a.harga_sewa * @Jumlah * fn_hitung_lama_sewa(@TanggalPeminjaman, @TanggalPengembalian)
                        FROM alat a
                        WHERE a.id = @IdAlat", new
                    {
                        IdAlat = item.IdAlat,
                        Jumlah = item.Jumlah,
                        TanggalPeminjaman = tanggalPeminjaman.ToDateTime(TimeOnly.MinValue),
                        TanggalPengembalian = tanggalPengembalian.ToDateTime(TimeOnly.MinValue)
                    }, trx);
                }

                const string totalQuery = @"
                    UPDATE peminjaman
                    SET total_biaya = @TotalBiaya
                    WHERE id = @Id";

                await conn.ExecuteAsync(totalQuery, new { Id = idPeminjaman, TotalBiaya = totalBiaya }, trx);

                await trx.CommitAsync();

                await log.Write(idPeminjam, $"Mengajukan peminjaman #{idPeminjaman}");

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

        //APPROVE (diajukan -> disetujui, id_petugas FROM JWT)
        public async Task<bool> ApprovePeminjaman(uint id, uint idPetugas)
        {
            using var conn = db.connect();

            const string query = @"
                UPDATE peminjaman p
                JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                SET p.id_petugas = @IdPetugas,
                    p.id_status_peminjaman = (SELECT id FROM status_peminjaman WHERE nama = 'disetujui')
                WHERE p.id = @Id AND sp.nama = 'diajukan'";

            var result = await conn.ExecuteAsync(query, new { Id = id, IdPetugas = idPetugas });

            if (result == 0)
            {
                throw new AppException(409, "Only peminjaman with status 'diajukan' can be approved");
            }

            await log.Write(idPetugas, $"Menyetujui peminjaman #{id}");

            return true;
        }

        //REJECT (diajukan -> ditolak)
        public async Task<bool> RejectPeminjaman(uint id, uint idPetugas, string? catatan)
        {
            using var conn = db.connect();

            const string query = @"
                UPDATE peminjaman p
                JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                SET p.id_petugas = @IdPetugas,
                    p.id_status_peminjaman = (SELECT id FROM status_peminjaman WHERE nama = 'ditolak')
                WHERE p.id = @Id AND sp.nama = 'diajukan'";

            var result = await conn.ExecuteAsync(query, new { Id = id, IdPetugas = idPetugas });

            if (result == 0)
            {
                throw new AppException(409, "Only peminjaman with status 'diajukan' can be rejected");
            }

            var note = string.IsNullOrWhiteSpace(catatan) ? "" : $" ({catatan.Trim()})";
            await log.Write(idPetugas, $"Menolak peminjaman #{id}{note}");

            return true;
        }

        //BORROW / HANDOVER (disetujui -> dipinjam, ASSIGN AVAILABLE UNITS)
        public async Task<bool> BorrowPeminjaman(uint id, uint idPetugas)
        {
            using var conn = db.connect();
            await conn.OpenAsync();
            using var trx = await conn.BeginTransactionAsync();

            try
            {
                const string checkQuery = @"
                    SELECT p.id
                    FROM peminjaman p
                    JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                    WHERE p.id = @Id AND sp.nama = 'disetujui'
                    FOR UPDATE";

                var exists = await conn.ExecuteScalarAsync<uint?>(checkQuery, new { Id = id }, trx);

                if (exists == null)
                {
                    throw new AppException(409, "Only peminjaman with status 'disetujui' can be handed over");
                }

                const string detailQuery = @"
                    SELECT id, id_alat, jumlah
                    FROM detail_peminjaman
                    WHERE id_peminjaman = @Id
                    ORDER BY id";

                var details = (await conn.QueryAsync<DetailRow>(detailQuery, new { Id = id }, trx)).ToList();

                const string unitQuery = @"
                    SELECT ua.id
                    FROM unit_alat ua
                    WHERE ua.id_alat = @IdAlat
                      AND ua.id_status_ketersediaan = (SELECT id FROM status_ketersediaan WHERE nama = 'tersedia')
                      AND ua.id_status_barang <> (SELECT id FROM status_barang WHERE nama = 'rusak_berat')
                    ORDER BY ua.id
                    LIMIT @Jumlah
                    FOR UPDATE";

                const string insertUnit = @"
                    INSERT INTO detail_unit_peminjaman (id_detail_peminjaman, id_unit_alat)
                    VALUES (@IdDetail, @IdUnitAlat)";

                const string updateUnit = @"
                    UPDATE unit_alat
                    SET id_status_ketersediaan = (SELECT id FROM status_ketersediaan WHERE nama = 'dipinjam')
                    WHERE id = @Id";

                foreach (var detail in details)
                {
                    var units = (await conn.QueryAsync<uint>(unitQuery, new { IdAlat = detail.IdAlat, Jumlah = detail.Jumlah }, trx)).ToList();

                    if (units.Count < detail.Jumlah)
                    {
                        throw new AppException(409, $"Not enough available units for alat #{detail.IdAlat}");
                    }

                    foreach (var idUnit in units)
                    {
                        await conn.ExecuteAsync(insertUnit, new { IdDetail = detail.Id, IdUnitAlat = idUnit }, trx);
                        await conn.ExecuteAsync(updateUnit, new { Id = idUnit }, trx);
                    }
                }

                const string updateQuery = @"
                    UPDATE peminjaman
                    SET id_status_peminjaman = (SELECT id FROM status_peminjaman WHERE nama = 'dipinjam')
                    WHERE id = @Id";

                await conn.ExecuteAsync(updateQuery, new { Id = id }, trx);

                await trx.CommitAsync();

                await log.Write(idPetugas, $"Menyerahkan alat peminjaman #{id}");

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

        //UPDATE (ADMIN, ONLY WHILE STATUS IS 'diajukan')
        public async Task<bool> UpdatePeminjaman(uint id, PeminjamanRequest request, uint idAdmin)
        {
            var tanggalPeminjaman = request.TanggalPeminjaman!.Value;
            var tanggalPengembalian = request.TanggalPengembalian!.Value;

            if (tanggalPengembalian < tanggalPeminjaman)
            {
                throw new AppException(400, "tanggalPengembalian must not be before tanggalPeminjaman");
            }

            var duplicate = request.Items.GroupBy(i => i.IdAlat).Any(g => g.Count() > 1);
            if (duplicate)
            {
                throw new AppException(400, "Each alat may only appear once in items");
            }

            using var conn = db.connect();
            await conn.OpenAsync();
            using var trx = await conn.BeginTransactionAsync();

            try
            {
                const string checkQuery = @"
                    SELECT p.id_peminjam
                    FROM peminjaman p
                    JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                    WHERE p.id = @Id AND sp.nama = 'diajukan'
                    FOR UPDATE";

                var idPeminjam = await conn.ExecuteScalarAsync<uint?>(checkQuery, new { Id = id }, trx);

                if (idPeminjam == null)
                {
                    throw new AppException(409, "Only peminjaman with status 'diajukan' can be edited");
                }

                const string deleteQuery = @"
                    DELETE FROM detail_peminjaman WHERE id_peminjaman = @Id";

                await conn.ExecuteAsync(deleteQuery, new { Id = id }, trx);

                decimal totalBiaya = 0;

                foreach (var item in request.Items)
                {
                    const string insertQuery = @"
                        INSERT INTO detail_peminjaman (id_peminjaman, id_alat, jumlah, harga_sewa_saat_transaksi)
                        SELECT @Id, a.id, @Jumlah, a.harga_sewa
                        FROM alat a
                        WHERE a.id = @IdAlat";

                    var inserted = await conn.ExecuteAsync(insertQuery, new { Id = id, IdAlat = item.IdAlat, Jumlah = item.Jumlah }, trx);

                    if (inserted == 0)
                    {
                        throw new AppException(404, $"Alat #{item.IdAlat} not found");
                    }

                    totalBiaya += await conn.ExecuteScalarAsync<decimal>(@"
                        SELECT a.harga_sewa * @Jumlah * fn_hitung_lama_sewa(@TanggalPeminjaman, @TanggalPengembalian)
                        FROM alat a
                        WHERE a.id = @IdAlat", new
                    {
                        IdAlat = item.IdAlat,
                        Jumlah = item.Jumlah,
                        TanggalPeminjaman = tanggalPeminjaman.ToDateTime(TimeOnly.MinValue),
                        TanggalPengembalian = tanggalPengembalian.ToDateTime(TimeOnly.MinValue)
                    }, trx);
                }

                const string updateQuery = @"
                    UPDATE peminjaman
                    SET tanggal_peminjaman = @TanggalPeminjaman,
                        tanggal_pengembalian = @TanggalPengembalian,
                        total_biaya = @TotalBiaya
                    WHERE id = @Id";

                await conn.ExecuteAsync(updateQuery, new
                {
                    Id = id,
                    TanggalPeminjaman = tanggalPeminjaman.ToDateTime(TimeOnly.MinValue),
                    TanggalPengembalian = tanggalPengembalian.ToDateTime(TimeOnly.MinValue),
                    TotalBiaya = totalBiaya
                }, trx);

                await trx.CommitAsync();

                await log.Write(idAdmin, $"Mengubah peminjaman #{id}");

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

        //DELETE (ADMIN, ONLY 'diajukan' OR 'ditolak')
        public async Task<bool> DeletePeminjaman(uint id, uint idAdmin)
        {
            using var conn = db.connect();
            await conn.OpenAsync();
            using var trx = await conn.BeginTransactionAsync();

            try
            {
                const string checkQuery = @"
                    SELECT sp.nama
                    FROM peminjaman p
                    JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                    WHERE p.id = @Id
                    FOR UPDATE";

                var status = await conn.ExecuteScalarAsync<string?>(checkQuery, new { Id = id }, trx);

                if (status == null)
                {
                    throw new AppException(404, "Peminjaman not found");
                }

                if (status != "diajukan" && status != "ditolak")
                {
                    throw new AppException(409, "Only peminjaman with status 'diajukan' or 'ditolak' can be deleted");
                }

                await conn.ExecuteAsync("DELETE FROM detail_peminjaman WHERE id_peminjaman = @Id", new { Id = id }, trx);
                await conn.ExecuteAsync("DELETE FROM peminjaman WHERE id = @Id", new { Id = id }, trx);

                await trx.CommitAsync();

                await log.Write(idAdmin, $"Menghapus peminjaman #{id}");

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

        //MONITOR RETURNS (BORROWED, DUE TODAY, OVERDUE)
        public async Task<PagedResult<MonitorPeminjaman>> GetMonitorPeminjaman(int page, int pageSize, string? status)
        {
            (page, pageSize) = Pagination.Normalize(page, pageSize);
            using var conn = db.connect();

            const string filter = @"
                FROM peminjaman p
                JOIN users u ON u.id = p.id_peminjam
                JOIN status_peminjaman sp ON sp.id = p.id_status_peminjaman
                WHERE sp.nama IN ('disetujui', 'dipinjam')
                  AND (@Status IS NULL
                       OR (@Status = 'jatuh_tempo' AND p.tanggal_pengembalian = CURRENT_DATE)
                       OR (@Status = 'terlambat' AND p.tanggal_pengembalian < CURRENT_DATE))";

            const string query = @"
                SELECT p.id, u.nama AS peminjam, u.no_telepon,
                       p.tanggal_peminjaman, p.tanggal_pengembalian,
                       GREATEST(0, DATEDIFF(CURRENT_DATE, p.tanggal_pengembalian)) AS hari_terlambat,
                       p.total_biaya" + filter + @"
                ORDER BY p.tanggal_pengembalian
                LIMIT @Limit OFFSET @Offset";

            var param = new { Status = Pagination.Search(status), Limit = pageSize, Offset = (page - 1) * pageSize };

            var total = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) " + filter, param);
            var result = await conn.QueryAsync<MonitorPeminjaman>(query, param);

            return new PagedResult<MonitorPeminjaman> { Items = result.ToList(), Page = page, PageSize = pageSize, Total = total };
        }

        private async Task LoadDetails(MySql.Data.MySqlClient.MySqlConnection conn, List<PeminjamanResponse> loans)
        {
            if (loans.Count == 0)
            {
                return;
            }

            const string query = @"
                SELECT dp.id_peminjaman, dp.id, dp.id_alat, a.nama AS alat, dp.jumlah,
                       dp.harga_sewa_saat_transaksi, ua.id AS id_unit_alat, ua.kode_inventaris
                FROM detail_peminjaman dp
                JOIN alat a ON a.id = dp.id_alat
                LEFT JOIN detail_unit_peminjaman dup ON dup.id_detail_peminjaman = dp.id
                LEFT JOIN unit_alat ua ON ua.id = dup.id_unit_alat
                WHERE dp.id_peminjaman IN @Ids
                ORDER BY dp.id, ua.kode_inventaris";

            var rows = await conn.QueryAsync<DetailUnitRow>(query, new { Ids = loans.Select(l => l.Id).ToArray() });

            var byLoan = loans.ToDictionary(l => l.Id);

            foreach (var row in rows)
            {
                if (!byLoan.TryGetValue(row.IdPeminjaman, out var loan))
                {
                    continue;
                }

                var detail = loan.Details.FirstOrDefault(d => d.Id == row.Id);
                if (detail == null)
                {
                    detail = new PeminjamanDetailResponse
                    {
                        Id = row.Id,
                        IdAlat = row.IdAlat,
                        Alat = row.Alat,
                        Jumlah = row.Jumlah,
                        HargaSewaSaatTransaksi = row.HargaSewaSaatTransaksi
                    };
                    loan.Details.Add(detail);
                }

                if (!string.IsNullOrEmpty(row.KodeInventaris) && !detail.KodeInventaris.Contains(row.KodeInventaris))
                {
                    detail.IdUnitAlat.Add(row.IdUnitAlat!.Value);
                    detail.KodeInventaris.Add(row.KodeInventaris);
                }
            }
        }

        private class DetailRow
        {
            public uint Id { get; set; }
            public uint IdAlat { get; set; }
            public int Jumlah { get; set; }
        }

        private class DetailUnitRow
        {
            public uint IdPeminjaman { get; set; }
            public uint Id { get; set; }
            public uint IdAlat { get; set; }
            public string? Alat { get; set; }
            public int Jumlah { get; set; }
            public decimal HargaSewaSaatTransaksi { get; set; }
            public uint? IdUnitAlat { get; set; }
            public string? KodeInventaris { get; set; }
        }
    }
}
