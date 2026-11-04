using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zrobim.Api.Users;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        // email is unique case-insensitively: unique index on lower(email),
        // created with raw SQL in the InitialCreate migration (EF has no expression indexes).
        b.ToTable("users");

        b.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(u => u.Email).HasMaxLength(255);
        b.Property(u => u.Phone).HasMaxLength(30);
        b.Property(u => u.PasswordHash).HasMaxLength(255);
        b.Property(u => u.FirstName).HasMaxLength(100);
        b.Property(u => u.LastName).HasMaxLength(100);
        b.Property(u => u.AvatarUrl).HasMaxLength(500);
        // DB default true; sentinel true = "not set", so an explicit false is still written
        b.Property(u => u.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        b.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(u => u.Phone).IsUnique().HasDatabaseName("users_phone_key");
    }
}
