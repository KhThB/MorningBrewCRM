// File: Role.cs
// Mô tả: Thực thể Role đơn giản để phục vụ seeding và test.
using System;
using System.ComponentModel.DataAnnotations;

namespace MorningBrew.Api.Domain.Entities
{
    public class Role
    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
