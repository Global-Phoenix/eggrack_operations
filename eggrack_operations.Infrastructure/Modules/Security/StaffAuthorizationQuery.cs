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

        const string staffSql = """
            SELECT id AS StaffId, staff_ref AS StaffRef, auth_version AS AuthVersion
            FROM eggrack_auth_staff
            WHERE staff_ref = @StaffRef AND status = 1 AND deleted_at IS NULL
            LIMIT 1
            """;
        const string grantSql = """
            SELECT
                p.permission_code AS PermissionCode,
                rp.effect AS Effect,
                COALESCE(rs.data_scope, r.default_scope) AS ScopeName,
                sr.department_id AS DepartmentId
            FROM eggrack_auth_staff_role sr
            INNER JOIN eggrack_auth_role r
                ON r.id = sr.role_id AND r.status = 1
            INNER JOIN eggrack_auth_role_permission rp
                ON rp.role_id = r.id
            INNER JOIN eggrack_auth_permission p
                ON p.id = rp.permission_id AND p.status = 1
            LEFT JOIN eggrack_auth_role_scope rs
                ON rs.role_id = r.id AND rs.permission_id = p.id
            WHERE sr.staff_id = @StaffId
              AND (sr.valid_from IS NULL OR sr.valid_from <= UTC_TIMESTAMP(3))
              AND (sr.valid_until IS NULL OR sr.valid_until > UTC_TIMESTAMP(3))
            """;

        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        var staff = (await session.QueryAsync<StaffRow>(
            staffSql,
            new { StaffRef = staffRef },
            cancellationToken: cancellationToken)).SingleOrDefault();
        if (staff is null)
            return null;

        var rows = await session.QueryAsync<GrantRow>(
            grantSql,
            new { staff.StaffId },
            cancellationToken: cancellationToken);
        var grants = rows.Select(x => new PermissionGrant(
            x.PermissionCode,
            x.Effect,
            ParseScope(x.ScopeName),
            x.DepartmentId)).ToArray();

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

    private sealed record StaffRow(long StaffId, string StaffRef, long AuthVersion);
    private sealed record GrantRow(
        string PermissionCode,
        string Effect,
        string ScopeName,
        long? DepartmentId);
}

