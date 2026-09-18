using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MorningBrew.Api.Domain.Entities;

[Table("LoyaltyTransactions")]
public class LoyaltyTransaction
{
    [Key]
    [Column("LoyaltyTransactionID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public int? OrderId { get; set; }

    [Required, MaxLength(20)]
    [Column("TransactionType")]
    public string Type { get; set; } = "Earn";

    public int Points { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
