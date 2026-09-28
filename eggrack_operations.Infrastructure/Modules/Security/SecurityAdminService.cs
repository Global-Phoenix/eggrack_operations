using System.Text.Json;
using Eggrack.Operations.Application.Modules.Security;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Security;

public sealed class SecurityAdminService(DatabaseSessionFactory databases)
{
    private const string DatabaseName = "Eggrack";

    public async Task<SecurityDashboard> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
              (SELECT COUNT(*) FROM eggrack_auth_staff WHERE deleted_at IS NULL) StaffCount,
              (SELECT COUNT(*) FROM eggrack_auth_staff WHERE status=1 AND deleted_at IS NULL) EnabledStaffCount,
              (SELECT COUNT(*) FROM eggrack_auth_department WHERE status=1 AND deleted_at IS NULL) DepartmentCount,
              (SELECT COUNT(*) FROM eggrack_auth_role WHERE status=1) RoleCount
            """;
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return (await session.QueryAsync<SecurityDashboard>(sql, cancellationToken: cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<StaffListItem>> GetStaffAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.id, s.staff_ref StaffRef, s.staff_name StaffName, s.email,
              s.status=1 IsEnabled,
              COALESCE(GROUP_CONCAT(DISTINCT d.department_name ORDER BY d.department_name SEPARATOR '、'),'—') Departments,
              COALESCE(GROUP_CONCAT(DISTINCT r.role_name ORDER BY r.role_level SEPARATOR '、'),'未授权') Roles
            FROM eggrack_auth_staff s
            LEFT JOIN eggrack_auth_staff_department sd ON sd.staff_id=s.id
            LEFT JOIN eggrack_auth_department d ON d.id=sd.department_id AND d.deleted_at IS NULL
            LEFT JOIN eggrack_auth_staff_role sr ON sr.staff_id=s.id
              AND (sr.valid_from IS NULL OR sr.valid_from<=UTC_TIMESTAMP(3))
              AND (sr.valid_until IS NULL OR sr.valid_until>UTC_TIMESTAMP(3))
            LEFT JOIN eggrack_auth_role r ON r.id=sr.role_id AND r.status=1
            WHERE s.deleted_at IS NULL
              AND (@Keyword IS NULL OR s.staff_name LIKE CONCAT('%',@Keyword,'%')
                   OR s.staff_ref LIKE CONCAT('%',@Keyword,'%'))
            GROUP BY s.id,s.staff_ref,s.staff_name,s.email,s.status
            ORDER BY s.status DESC,s.staff_name
            LIMIT 200
            """;
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<StaffListItem>(sql, new { Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim() }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<RoleListItem>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT r.id,r.role_code Code,r.role_name Name,r.role_level Level,
              r.default_scope DefaultScope,r.status=1 IsEnabled,
              COUNT(DISTINCT sr.staff_id) StaffCount,
              COUNT(DISTINCT rp.permission_id) PermissionCount
            FROM eggrack_auth_role r
            LEFT JOIN eggrack_auth_staff_role sr ON sr.role_id=r.id
            LEFT JOIN eggrack_auth_role_permission rp ON rp.role_id=r.id
            GROUP BY r.id,r.role_code,r.role_name,r.role_level,r.default_scope,r.status
            ORDER BY r.role_level
            """;
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<RoleListItem>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DepartmentOption>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id,department_name Name FROM eggrack_auth_department WHERE status=1 AND deleted_at IS NULL ORDER BY sort_order,department_name";
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<DepartmentOption>(sql, cancellationToken: cancellationToken);
    }

    public async Task AssignRoleAsync(long staffId, long roleId, long? departmentId, string operatorRef, CancellationToken cancellationToken = default)
    {
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        var roles = await session.QueryAsync<RoleRule>(
            "SELECT role_code Code FROM eggrack_auth_role WHERE id=@RoleId AND status=1",
            new { RoleId = roleId }, cancellationToken: cancellationToken);
        var role = roles.SingleOrDefault() ?? throw new InvalidOperationException("角色不存在或已停用");
        var globalRole = role.Code is "super_admin" or "boss";
        if (globalRole && departmentId.HasValue) throw new InvalidOperationException("全局角色不能指定部门");
        if (!globalRole && !departmentId.HasValue) throw new InvalidOperationException("部门角色必须指定部门");

        await session.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            const string assignSql = """
                INSERT INTO eggrack_auth_staff_role(staff_id,role_id,department_id,granted_by)
                SELECT @StaffId,@RoleId,@DepartmentId,g.id
                FROM eggrack_auth_staff g WHERE g.staff_ref=@OperatorRef
                ON DUPLICATE KEY UPDATE granted_by=VALUES(granted_by),valid_until=NULL
                """;
            await session.ExecuteAsync(assignSql, new { StaffId = staffId, RoleId = roleId, DepartmentId = departmentId, OperatorRef = operatorRef }, cancellationToken: cancellationToken);
            await session.ExecuteAsync("UPDATE eggrack_auth_staff SET auth_version=auth_version+1 WHERE id=@StaffId", new { StaffId = staffId }, cancellationToken: cancellationToken);
            await session.ExecuteAsync(
                "INSERT INTO eggrack_auth_audit_log(operator_ref,action_code,target_type,target_ref,after_data) VALUES(@OperatorRef,'auth.role.assign','staff',@TargetRef,@AfterData)",
                new { OperatorRef = operatorRef, TargetRef = staffId.ToString(), AfterData = JsonSerializer.Serialize(new { roleId, departmentId }) },
                cancellationToken: cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        catch
        {
            await session.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task CreateStaffAsync(
        string staffRef,
        string staffName,
        string email,
        long roleId,
        long? departmentId,
        string operatorRef,
        CancellationToken cancellationToken = default)
    {
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        var roles = await session.QueryAsync<RoleRule>(
            "SELECT role_code Code FROM eggrack_auth_role WHERE id=@RoleId AND status=1",
            new { RoleId = roleId }, cancellationToken: cancellationToken);
        var role = roles.SingleOrDefault() ?? throw new InvalidOperationException("角色不存在或已停用");
        var globalRole = role.Code is "super_admin" or "boss";
        if (globalRole && departmentId.HasValue) throw new InvalidOperationException("全局角色不能指定部门");
        if (!globalRole && !departmentId.HasValue) throw new InvalidOperationException("部门角色必须指定部门");

        await session.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            const string insertStaff = """
                INSERT INTO eggrack_auth_staff(staff_ref,staff_name,email,status,auth_version)
                VALUES(@StaffRef,@StaffName,@Email,1,1)
                """;
            await session.ExecuteAsync(insertStaff, new { StaffRef = staffRef, StaffName = staffName, Email = email }, cancellationToken: cancellationToken);
            var staffId = (await session.QueryAsync<InsertedId>(
                "SELECT LAST_INSERT_ID() Id",
                cancellationToken: cancellationToken)).Single().Id;
            const string assignRole = """
                INSERT INTO eggrack_auth_staff_role(staff_id,role_id,department_id,granted_by)
                SELECT @StaffId,@RoleId,@DepartmentId,g.id
                FROM eggrack_auth_staff g WHERE g.staff_ref=@OperatorRef
                """;
            if (await session.ExecuteAsync(assignRole, new { StaffId = staffId, RoleId = roleId, DepartmentId = departmentId, OperatorRef = operatorRef }, cancellationToken: cancellationToken) != 1)
                throw new InvalidOperationException("无法识别当前操作人员");
            await session.ExecuteAsync(
                "INSERT INTO eggrack_auth_audit_log(operator_ref,action_code,target_type,target_ref,after_data) VALUES(@OperatorRef,'auth.staff.create','staff',@TargetRef,@AfterData)",
                new { OperatorRef = operatorRef, TargetRef = staffId.ToString(), AfterData = JsonSerializer.Serialize(new { staffName, email, roleId, departmentId }) },
                cancellationToken: cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        catch
        {
            await session.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SetStaffEnabledAsync(
        long staffId,
        bool enabled,
        string operatorRef,
        CancellationToken cancellationToken = default)
    {
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        await session.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            var changed = await session.ExecuteAsync(
                "UPDATE eggrack_auth_staff SET status=@Status,auth_version=auth_version+1 WHERE id=@StaffId AND deleted_at IS NULL",
                new { StaffId = staffId, Status = enabled ? 1 : 0 }, cancellationToken: cancellationToken);
            if (changed != 1) throw new InvalidOperationException("人员不存在或已删除");
            await session.ExecuteAsync(
                "INSERT INTO eggrack_auth_audit_log(operator_ref,action_code,target_type,target_ref,after_data) VALUES(@OperatorRef,@ActionCode,'staff',@TargetRef,@AfterData)",
                new
                {
                    OperatorRef = operatorRef,
                    ActionCode = enabled ? "auth.staff.enable" : "auth.staff.disable",
                    TargetRef = staffId.ToString(),
                    AfterData = JsonSerializer.Serialize(new { enabled })
                }, cancellationToken: cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        catch
        {
            await session.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed record RoleRule(string Code);
    private sealed record InsertedId(long Id);
}

