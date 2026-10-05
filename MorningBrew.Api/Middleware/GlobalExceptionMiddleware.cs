// File: GlobalExceptionMiddleware.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MorningBrew.Api.Common;

namespace MorningBrew.Api.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception has occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            int statusCode;
            string message;

            switch (exception)
            {
                case InvalidOperationException e:
                    statusCode = (int)HttpStatusCode.BadRequest;
                    message = e.Message;
                    break;
                case UnauthorizedAccessException e:
                    statusCode = (int)HttpStatusCode.Unauthorized;
                    message = e.Message;
                    break;
                case KeyNotFoundException e:
                    statusCode = (int)HttpStatusCode.NotFound;
                    message = e.Message;
                    break;
                case ArgumentException e:
                    statusCode = (int)HttpStatusCode.BadRequest;
                    message = e.Message;
                    break;
                default:
                    statusCode = (int)HttpStatusCode.InternalServerError;
                    message = "An internal server error occurred.";
                    break;
            }

            context.Response.StatusCode = statusCode;
            var response = ApiResponse<object>.Failure(statusCode, message);
            var result = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            return context.Response.WriteAsync(result);
        }
    }
}
