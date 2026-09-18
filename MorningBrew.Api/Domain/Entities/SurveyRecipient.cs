using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MorningBrew.Api.Domain.Entities;

[Table("SurveyRecipients")]
[PrimaryKey(nameof(SurveyId), nameof(CustomerId))]
public class SurveyRecipient
{
    public int SurveyId { get; set; }
    public int CustomerId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Sent";
}
