// File: ApiResponse.cs
using System.Collections.Generic;

namespace MorningBrew.Api.Common
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Success")
        {
            return new ApiResponse<T> { IsSuccess = true, StatusCode = 200, Message = message, Data = data };
        }

        public static ApiResponse<T> Failure(int statusCode, string message, List<string>? errors = null)
        {
            return new ApiResponse<T> { IsSuccess = false, StatusCode = statusCode, Message = message, Errors = errors };
        }
    }
}
