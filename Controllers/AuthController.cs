using System.Security.Claims;
using CampGearBackend.Services;
using CampGearBackend.Models;

namespace CampGearBackend.Controllers
{
    public static class AuthController
    {
        public static void MapAuth(this WebApplication app)
        {
            var g = app.MapGroup("api/v1/auth");

            //LOGIN
            g.MapPost("/login", async (AuthService service, LoginRequest request) =>
            {
                try
                {
                    var response = await service.Login(request);
                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    var problem = Problem.Fault(ex);
                    return Results.Problem(title: problem.Title, statusCode: problem.StatusCode, detail: problem.Detail);
                }
            });

            //LOGOUT (JWT IS STATELESS, ONLY WRITES log_aktivitas, THE CLIENT DISCARDS THE TOKEN)
            g.MapPost("/logout", async (AuthService service, ClaimsPrincipal user) =>
            {
                try
                {
                    await service.Logout(UserAuth.GetId(user));
                    return Results.Ok();
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
