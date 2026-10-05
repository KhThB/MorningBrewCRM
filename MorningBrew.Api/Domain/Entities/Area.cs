// File: Area.cs
// Mô tả: Thực thể Area (khu vực) dùng cho phân vùng quản lý.
using System.ComponentModel.DataAnnotations;

namespace MorningBrew.Api.Domain.Entities
{
    public class Area
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
