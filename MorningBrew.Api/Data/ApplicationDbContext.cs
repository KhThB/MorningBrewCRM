using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Domain.Entities;

namespace MorningBrew.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPreference> CustomerPreferences => Set<CustomerPreference>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<CustomerPromotion> CustomerPromotions => Set<CustomerPromotion>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<SurveyRecipient> SurveyRecipients => Set<SurveyRecipient>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<CustomerPreference>(entity =>
        {
            entity.HasKey(x => new { x.CustomerId, x.ProductId });

            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerPromotion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Promotion>()
                .WithMany()
                .HasForeignKey(x => x.PromotionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.HandledBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Survey>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasMany(x => x.Questions)
                .WithOne()
                .HasForeignKey(x => x.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SurveyQuestion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasMany<SurveyResponse>()
                .WithOne()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyRecipient>(entity =>
        {
            entity.HasKey(x => new { x.SurveyId, x.CustomerId });
            entity.HasOne<Survey>()
                .WithMany()
                .HasForeignKey(x => x.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyResponse>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne<SurveyQuestion>()
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
