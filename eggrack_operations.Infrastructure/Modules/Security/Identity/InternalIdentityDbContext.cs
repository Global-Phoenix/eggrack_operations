using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eggrack.Operations.Infrastructure.Modules.Security.Identity;

public sealed class InternalIdentityDbContext(
    DbContextOptions<InternalIdentityDbContext> options)
    : IdentityDbContext<InternalIdentityUser,IdentityRole<long>,long>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<InternalIdentityUser>(entity =>
        {
            entity.ToTable("eggrack_identity_user");
            entity.Property(x=>x.StaffRef).HasMaxLength(128).IsRequired();
            entity.Property(x=>x.DisplayName).HasMaxLength(128).IsRequired();
            entity.HasIndex(x=>x.StaffRef).IsUnique();
        });
        builder.Entity<IdentityRole<long>>().ToTable("eggrack_identity_role");
        builder.Entity<IdentityUserRole<long>>().ToTable("eggrack_identity_user_role");
        builder.Entity<IdentityUserClaim<long>>().ToTable("eggrack_identity_user_claim");
        builder.Entity<IdentityUserLogin<long>>().ToTable("eggrack_identity_user_login");
        builder.Entity<IdentityRoleClaim<long>>().ToTable("eggrack_identity_role_claim");
        builder.Entity<IdentityUserToken<long>>().ToTable("eggrack_identity_user_token");
    }
}

