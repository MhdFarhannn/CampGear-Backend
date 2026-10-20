namespace CampGearBackend.Models
{
    public class AppException : Exception
    {
        public int StatusCode { get; set; }

        public AppException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
