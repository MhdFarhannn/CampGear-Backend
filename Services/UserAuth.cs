using System.Security.Claims;
using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public static class UserAuth
    {
        //CURRENT USER ID ALWAYS COMES FROM THE JWT CLAIM
        public static uint GetId(ClaimsPrincipal user)
        {
            var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!uint.TryParse(claim, out var id))
            {
                throw new AppException(401, "Invalid token");
            }

            return id;
        }

        //CURRENT USER ROLE FROM THE JWT CLAIM
        public static string GetRole(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }
    }
}
