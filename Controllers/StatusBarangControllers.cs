using CampGearBackend.Services;
using CampGearBackend.Models;

namespace CampGearBackend.Controllers
{
    public static class StatusBarangController
    {
        public static void MapStatusBarang(this WebApplication app)
        {
            var g = app.MapGroup("api/v1/status-barang");

            //MENAMBAHKAN STATUS BARANG
            g.MapPost("/", async (StatusBarangServices service, StatusBarang statusBarang) =>
            {
                try
                {
                    await service.CreateStatusBarang(statusBarang);
                    return Results.Ok();
                }
                catch (Exception e)
                {
                    return Results.Problem(title: "Internal Server Error", statusCode: 500, detail: e.Message);
                }
            });

            //MENGHAPUS STATUS BARANG
            g.MapDelete("/{id}", async (StatusBarangServices service, int id) =>
            {
                try
                {
                    await service.DeleteStatusBarang(id);
                    return Results.Ok();
                }
                catch (Exception e)
                {
                    return Results.Problem(title: "Internal Server Error", statusCode: 500, detail: e.Message);
                }
            });

            //EDIT STATUS BARANG
            g.MapPatch("/{id}", async (StatusBarangServices service, int id, StatusBarang statusBarang) =>
            {
                try
                {
                    await service.PatchStatusBarang(id, statusBarang);
                    return Results.Ok();
                }
                catch (Exception e)
                {
                    return Results.Problem(title: "Internal Server Error", statusCode: 500, detail: e.Message);
                }
            });

            //GET ALL STATUS BARANG
            g.MapGet("/", async (StatusBarangServices service) =>
            {
                try
                {
                    var statusBarangs = await service.GetAllStatusBarang();
                    return Results.Ok(statusBarangs);
                }
                catch (Exception e)
                {
                    return Results.Problem(title: "Internal Server Error", statusCode: 500, detail: e.Message);
                }
            });
        }
    }
}
