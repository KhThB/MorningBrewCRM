using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("Promotions")]
public class Promotion
{
    [Key]
    [Column("PromotionID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Title { get; set; } = null!;

    [Required, MaxLength(150)]
    [Column("PromotionName")]
    public string Name { get; set; } = null!;

    [Required, Column(TypeName = "text")]
    public string Description { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [Required, MaxLength(20)]
    public string DiscountType { get; set; } = "Percent";

    [Column(TypeName = "decimal(12,2)")]
    public decimal DiscountValue { get; set; }

    public byte Status { get; set; } = 1;
    public int? BranchId { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public DateTime StartAt { get => StartDate; set => StartDate = value; }

    [NotMapped]
    public DateTime EndAt { get => EndDate; set => EndDate = value; }

    [NotMapped]
    public bool IsActive { get => Status == 1; set => Status = (byte)(value ? 1 : 0); }
}
