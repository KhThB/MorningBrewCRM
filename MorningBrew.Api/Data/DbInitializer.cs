// File: DbInitializer.cs
// Mô tả: Lớp hỗ trợ seed dữ liệu mẫu cho phát triển/test độc lập (Roles, Area, Branches, Users, Customer)
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Domain.Entities;

namespace MorningBrew.Api.Data
{
    public static class DbInitializer
    {
        // Ghi chú: Hàm tĩnh Seed nhận DbContext và sẽ tạo dữ liệu mẫu nếu bảng rỗng.
        public static void Seed(DbContext db)
        {
            // Block on async calls for simplicity when called from Program.cs
            SeedAsync(db).GetAwaiter().GetResult();
        }

        private static async System.Threading.Tasks.Task SeedAsync(DbContext db)
        {
            await db.Database.EnsureCreatedAsync();

            // Predefined IDs để dễ test
            var roleOwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var roleBranchManagerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var roleCustomerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

            const int areaId = 1;
            const int branch1Id = 1;
            const int branch2Id = 2;

            const int adminUserId = 1;
            const int customerUserId = 2;

            const int customerId = 1;

            // Roles
            if (!await db.Set<Role>().AnyAsync())
            {
                db.Add(new Role { Id = roleOwnerId, Name = "Owner" });
                db.Add(new Role { Id = roleBranchManagerId, Name = "BranchManager" });
                db.Add(new Role { Id = roleCustomerId, Name = "Customer" });
                await db.SaveChangesAsync();
            }

            // Area
            if (!await db.Set<Area>().AnyAsync(a => a.Id == areaId))
            {
                db.Add(new Area { Id = areaId, Name = "Area 1" });
                await db.SaveChangesAsync();
            }

            // Branches
            if (!await db.Set<Branch>().AnyAsync())
            {
                db.Add(new Branch { Id = branch1Id, Name = "Branch 1", AreaId = areaId });
                db.Add(new Branch { Id = branch2Id, Name = "Branch 2", AreaId = areaId });
                await db.SaveChangesAsync();
            }

            // Users (Admin + Customer)
            if (!await db.Set<AppUser>().AnyAsync())
            {
                db.Add(new AppUser { Id = adminUserId, UserName = "admin1", PasswordHash = "admin123", Email = "admin1@mb.local", BranchId = branch1Id, AreaId = areaId, Role = "BranchManager" });
                db.Add(new AppUser { Id = customerUserId, UserName = "customer1", PasswordHash = "customer123", Email = "customer1@mb.local", Role = "Customer" });
                await db.SaveChangesAsync();
            }

            // Customer record (domain Customer)
            if (!await db.Set<Customer>().AnyAsync(c => c.Id == customerId))
            {
                db.Add(new Customer
                {
                    Id = customerId,
                    UserId = customerUserId,
                    LoyaltyPoints = 100,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
