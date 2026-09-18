using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MorningBrew.Api.Domain.Entities;

[Table("CustomerPreferences")]
[PrimaryKey(nameof(CustomerId), nameof(ProductId))]
public class CustomerPreference
{
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public byte PreferenceLevel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
