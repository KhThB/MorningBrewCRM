using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("Products")]
public class Product
{
    [Key]
    [Column("ProductID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int CategoryId { get; set; }

    [Required, MaxLength(150)]
    [Column("ProductName")]
    public string Name { get; set; } = null!;

    [Required, Column(TypeName = "text")]
    public string Description { get; set; } = null!;

    [Column(TypeName = "decimal(12,2)")]
    public decimal BasePrice { get; set; }

    [MaxLength(255)]
    public string? ImageURL { get; set; }

    public byte Status { get; set; } = 1;
}
