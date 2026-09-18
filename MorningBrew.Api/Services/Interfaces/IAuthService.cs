// File: IAuthService.cs
using System.Threading.Tasks;
using MorningBrew.Api.DTOs.Auth;

namespace MorningBrew.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
    }
}
