using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("Feedbacks")]
public class Feedback
{
    [Key]
    [Column("FeedbackID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public int? BranchId { get; set; }
    public int? ProductId { get; set; }

    [Required, MaxLength(30)]
    public string FeedbackType { get; set; } = null!;

    [Required, Column(TypeName = "text")]
    public string Content { get; set; } = null!;

    public byte? Rating { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "New";

    public int? HandledBy { get; set; }

    [Column(TypeName = "text")]
    public string? ResponseContent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
}
