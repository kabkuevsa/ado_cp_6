using System.Text.Json.Serialization;

namespace task6.Models
{
    public class ApiErrorResponse
    {
        public string Message { get; set; }
        public string ErrorType { get; set; }
        public DateTime Timestamp { get; set; }
        public string RequestId { get; set; }
        public Dictionary<string, string> Details { get; set; }

        public ApiErrorResponse()
        {
            Timestamp = DateTime.UtcNow;
            Details = new Dictionary<string, string>();
        }

        public static ApiErrorResponse Create(string message, string errorType = "GeneralError")
        {
            return new ApiErrorResponse
            {
                Message = message,
                ErrorType = errorType
            };
        }
    }
}