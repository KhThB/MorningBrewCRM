// File: Branch.cs
// Mô tả: Thực thể Branch (chi nhánh).
using System.ComponentModel.DataAnnotations;

namespace MorningBrew.Api.Domain.Entities
{
    public class Branch
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int? AreaId { get; set; }
    }
}
