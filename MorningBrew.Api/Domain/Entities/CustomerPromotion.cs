using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("CustomerPromotions")]
public class CustomerPromotion
{
    [Key]
    [Column("CustomerPromotionID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public int PromotionId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Sent";

    [NotMapped]
    public DateTime AssignedAt { get => SentAt; set => SentAt = value; }
}
