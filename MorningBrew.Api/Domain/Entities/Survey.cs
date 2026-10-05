using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("Surveys")]
public class Survey
{
    [Key]
    [Column("SurveyID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, MaxLength(255)]
    [Column("SurveyTitle")]
    public string Title { get; set; } = null!;

    [Required, Column(TypeName = "text")]
    public string Description { get; set; } = null!;

    public int CreatedBy { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Draft";

    public ICollection<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
}
