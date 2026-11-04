using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zrobim.Api.Users;

namespace Zrobim.Api.Favorites;

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.ToTable("favorites");

        b.HasKey(f => new { f.ClientId, f.MasterId });
        b.Property(f => f.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne<User>().WithMany().HasForeignKey(f => f.ClientId);
        b.HasOne(f => f.Master).WithMany().HasForeignKey(f => f.MasterId);
    }
}
