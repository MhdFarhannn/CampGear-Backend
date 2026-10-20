using System.Text;
using CampGearBackend.Services;
using CampGearBackend.Models;

namespace CampGearBackend.Controllers
{
    public static class LaporanController
    {
        public static void MapLaporan(this WebApplication app)
        {
            var g = app.MapGroup("api/v1/laporan");

            //LAPORAN PEMINJAMAN
            g.MapGet("/peminjaman", async (LaporanService service, DateOnly? from, DateOnly? to, string? format) =>
            {
                try
                {
                    var (start, end) = Range(from, to);
                    var rows = await service.GetLaporanPeminjaman(start, end);

                    if (format == "csv")
                    {
                        return Results.File(Encoding.UTF8.GetBytes(LaporanService.ToCsv(rows)),
                            "text/csv; charset=utf-8", $"laporan-peminjaman-{start}-{end}.csv");
                    }

                    return Results.Ok(new { from = start, to = end, totalBiaya = rows.Sum(r => r.TotalBiaya), items = rows });
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            }).RequireAuthorization(policy => policy.RequireRole("Admin", "Petugas"));

            //LAPORAN PENGEMBALIAN
            g.MapGet("/pengembalian", async (LaporanService service, DateOnly? from, DateOnly? to, string? format) =>
            {
                try
                {
                    var (start, end) = Range(from, to);
                    var rows = await service.GetLaporanPengembalian(start, end);

                    if (format == "csv")
                    {
                        return Results.File(Encoding.UTF8.GetBytes(LaporanService.ToCsv(rows)),
                            "text/csv; charset=utf-8", $"laporan-pengembalian-{start}-{end}.csv");
                    }

                    return Results.Ok(new { from = start, to = end, totalDenda = rows.Sum(r => r.Denda), items = rows });
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            }).RequireAuthorization(policy => policy.RequireRole("Admin", "Petugas"));
        }

        //DEFAULT RANGE = BULAN BERJALAN
        private static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
        {
            var start = from ?? new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = to ?? DateOnly.FromDateTime(DateTime.Today);

            if (end < start)
            {
                throw new AppException(400, "to must not be before from");
            }

            return (start, end);
        }
    }
}
