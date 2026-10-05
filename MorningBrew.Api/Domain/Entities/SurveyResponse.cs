using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("SurveyResponses")]
public class SurveyResponse
{
    [Key]
    [Column("ResponseID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public int CustomerId { get; set; }

    [Required, Column("AnswerValue", TypeName = "text")]
    public string Answer { get; set; } = null!;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
