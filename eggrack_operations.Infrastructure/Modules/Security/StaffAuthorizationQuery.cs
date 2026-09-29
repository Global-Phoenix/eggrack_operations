using Eggrack.Operations.Domain.Modules.Security;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Security;

public sealed class StaffAuthorizationQuery(DatabaseSessionFactory databases)
{
    private const string DatabaseName = "Eggrack";

    public async Task<StaffAuthorization?> LoadAsync(
        string staffRef,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffRef);

        const string sql = """
            SELECT CAST(s.id AS SIGNED) AS StaffId,
                   s.staff_ref AS StaffRef,
                   CAST(s.auth_version AS SIGNED) AS AuthVersion,
                   p.permission_code AS PermissionCode,
                   rp.effect AS Effect,
                   COALESCE(rs.data_scope, r.default_scope) AS ScopeName,
                   CAST(sr.department_id AS SIGNED) AS DepartmentId
            FROM eggrack_auth_staff s
            LEFT JOIN eggrack_auth_staff_role sr
                ON sr.staff_id = s.id
               AND (sr.valid_from IS NULL OR sr.valid_from <= UTC_TIMESTAMP(3))
               AND (sr.valid_until IS NULL OR sr.valid_until > UTC_TIMESTAMP(3))
            LEFT JOIN eggrack_auth_role r
                ON r.id = sr.role_id AND r.status = 1
            LEFT JOIN eggrack_auth_role_permission rp
                ON rp.role_id = r.id
            LEFT JOIN eggrack_auth_permission p
                ON p.id = rp.permission_id AND p.status = 1
            LEFT JOIN eggrack_auth_role_scope rs
                ON rs.role_id = r.id AND rs.permission_id = p.id
            WHERE s.staff_ref = @StaffRef
              AND s.status = 1
              AND s.deleted_at IS NULL
            """;

        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        var rows = await session.QueryAsync<AuthorizationRow>(
            sql,
            new { StaffRef = staffRef },
            cancellationToken: cancellationToken);
        var staff = rows.FirstOrDefault();
        if (staff is null)
            return null;

        var grants = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.PermissionCode) &&
                        !string.IsNullOrWhiteSpace(x.Effect) &&
                        !string.IsNullOrWhiteSpace(x.ScopeName))
            .Select(x => new PermissionGrant(
                x.PermissionCode!,
                x.Effect!,
                ParseScope(x.ScopeName!),
                x.DepartmentId))
            .ToArray();

        return new StaffAuthorization(
            staff.StaffId,
            staff.StaffRef,
            staff.AuthVersion,
            grants);
    }

    private static DataScope ParseScope(string scope) => scope.ToUpperInvariant() switch
    {
        "ALL" => DataScope.All,
        "DEPARTMENT" => DataScope.Department,
        "SELF_OR_DEPARTMENT" => DataScope.SelfOrDepartment,
        "SELF" => DataScope.Self,
        _ => DataScope.None
    };

    private sealed class AuthorizationRow
    {
        public long StaffId { get; set; }
        public string StaffRef { get; set; } = string.Empty;
        public long AuthVersion { get; set; }
        public string? PermissionCode { get; set; }
        public string? Effect { get; set; }
        public string? ScopeName { get; set; }
        public long? DepartmentId { get; set; }
    }
}

