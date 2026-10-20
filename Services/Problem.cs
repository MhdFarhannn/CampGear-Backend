using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public static class Problem
    {
        //MAPS A SERVICE EXCEPTION TO THE HTTP ERROR RESPONSE
        //AppException KEEPS ITS OWN STATUS CODE, EVERYTHING ELSE BECOMES 500
        public static (string Title, int StatusCode, string Detail) Fault(Exception exception)
        {
            if (exception is AppException appException)
            {
                return (Title(appException.StatusCode), appException.StatusCode, appException.Message);
            }

            return ("Internal Server Error", 500, exception.Message);
        }

        public static string Title(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                409 => "Conflict",
                _ => "Internal Server Error"
            };
        }
    }
}
