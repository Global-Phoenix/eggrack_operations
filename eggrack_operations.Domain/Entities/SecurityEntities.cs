namespace Eggrack.Operations.Domain.Entities;

public abstract class AuditableEntity
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class SystemUser : AuditableEntity
{
    public required string UserName { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public string? Email { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class SystemRole : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class SystemPermission : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Module { get; set; }
}

public sealed class SystemMenu : AuditableEntity
{
    public long? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Area { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? Icon { get; set; }
    public string? PermissionCode { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}

public sealed class UserRole
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
}

public sealed class RolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
}
