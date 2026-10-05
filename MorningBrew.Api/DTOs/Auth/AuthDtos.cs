// File: AuthDtos.cs
using System;

namespace MorningBrew.Api.DTOs.Auth
{
    public record LoginRequest(string Username, string Password);
    public record UserInfoDto(int Id, string Username, string Email, string Role, int? BranchId, int? AreaId);
    public record LoginResponse(string Token, DateTime ExpiresAt, UserInfoDto User);
}
