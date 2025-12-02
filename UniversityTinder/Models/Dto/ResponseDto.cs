using System.Net;

namespace UniversityTinder.Models.Dto
{
    public class ResponseDto
    {
        public object? Result { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string Message { get; set; } = "";

    }
}
