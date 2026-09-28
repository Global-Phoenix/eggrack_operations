using Microsoft.AspNetCore.Identity;

namespace Eggrack.Operations.Infrastructure.Modules.Security.Identity;

public sealed class InternalIdentityUser : IdentityUser<long>
{
    public required string StaffRef { get; set; }
    public required string DisplayName { get; set; }
    public bool MustEnableTwoFactor { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

