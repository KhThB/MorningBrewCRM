using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("SurveyQuestions")]
public class SurveyQuestion
{
    [Key]
    [Column("QuestionID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int SurveyId { get; set; }

    [Required, Column(TypeName = "text")]
    public string QuestionText { get; set; } = null!;

    [Required, MaxLength(30)]
    public string QuestionType { get; set; } = "Text";

    [Column(TypeName = "text")]
    public string? OptionsJSON { get; set; }

    public int DisplayOrder { get; set; }
}
