// File: AppUser.cs
// Mô tả: Thực thể người dùng giản lược để seeding và test JWT claims.
using System;
using System.ComponentModel.DataAnnotations;

namespace MorningBrew.Api.Domain.Entities
{
    public class AppUser
    {
        [Key]
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string PasswordHash { get; set; } = null!; // Thêm PasswordHash
        public int? BranchId { get; set; }
        public int? AreaId { get; set; }
        public string Role { get; set; } = null!;
    }
}
