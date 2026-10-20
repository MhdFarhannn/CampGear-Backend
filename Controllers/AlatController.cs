using System.Security.Claims;
using CampGearBackend.Services;
using CampGearBackend.Models;

namespace CampGearBackend.Controllers
{
    public static class AlatController
    {
        public static void MapAlat(this WebApplication app)
        {
            var g = app.MapGroup("api/v1/alat");

            //MENAMBAHKAN ALAT
            g.MapPost("/", async (AlatService service, ClaimsPrincipal user, AlatRequest request) =>
            {
                try
                {
                    await service.CreateAlat(request, UserAuth.GetId(user));
                    return Results.Ok();
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            }).RequireAuthorization(policy => policy.RequireRole("Admin"));

            //EDIT ALAT
            g.MapPut("/{id}", async (AlatService service, ClaimsPrincipal user, uint id, AlatRequest request) =>
            {
                try
                {
                    await service.UpdateAlat(id, request, UserAuth.GetId(user));
                    return Results.Ok();
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            }).RequireAuthorization(policy => policy.RequireRole("Admin"));

            //MENGHAPUS ALAT
            g.MapDelete("/{id}", async (AlatService service, ClaimsPrincipal user, uint id) =>
            {
                try
                {
                    await service.DeleteAlat(id, UserAuth.GetId(user));
                    return Results.Ok();
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            }).RequireAuthorization(policy => policy.RequireRole("Admin"));

            //GET ALL ALAT (INCLUDES AVAILABLE STOCK)
            g.MapGet("/", async (AlatService service, int? page, int? pageSize, string? search, uint? idKategori) =>
            {
                try
                {
                    var alats = await service.GetAllAlat(page ?? 1, pageSize ?? 20, search, idKategori);
                    return Results.Ok(alats);
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            }).RequireAuthorization();

            //GET ALAT BY ID
            g.MapGet("/{id}", async (AlatService service, uint id) =>
            {
                try
                {
                    var alat = await service.GetAlatById(id);
                    return Results.Ok(alat);
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            }).RequireAuthorization();
        }
    }
}
