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

    public async Task<IReadOnlyList<PermissionListItem>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id,permission_code Code,permission_name Name,resource_type ResourceType,status=1 IsEnabled FROM eggrack_auth_permission ORDER BY resource_type,permission_code";
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<PermissionListItem>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<RolePermissionGrant>> GetRolePermissionGrantsAsync(CancellationToken cancellationToken = default)
    {
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<RolePermissionGrant>("SELECT role_id RoleId,permission_id PermissionId FROM eggrack_auth_role_permission WHERE effect='ALLOW'", cancellationToken: cancellationToken);
    }

    public async Task SaveRolePermissionsAsync(long roleId, IReadOnlyCollection<long> permissionIds, string operatorRef, CancellationToken cancellationToken = default)
    {
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        var role = (await session.QueryAsync<RoleRule>("SELECT role_code Code FROM eggrack_auth_role WHERE id=@RoleId AND status=1", new { RoleId = roleId }, cancellationToken: cancellationToken)).SingleOrDefault()
            ?? throw new InvalidOperationException("角色不存在或已停用");
        if (role.Code == "super_admin") throw new InvalidOperationException("超级管理员固定拥有全部权限，不能手动删改");
        var distinctIds = permissionIds.Distinct().ToArray();
        if (distinctIds.Length > 0)
        {
            var valid = (await session.QueryAsync<CountRow>("SELECT COUNT(*) Value FROM eggrack_auth_permission WHERE status=1 AND id IN @PermissionIds", new { PermissionIds = distinctIds }, cancellationToken: cancellationToken)).Single().Value;
            if (valid != distinctIds.Length) throw new InvalidOperationException("提交内容包含不存在或已停用的权限点");
        }
        await session.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            await session.ExecuteAsync("DELETE FROM eggrack_auth_role_permission WHERE role_id=@RoleId", new { RoleId = roleId }, cancellationToken: cancellationToken);
            if (distinctIds.Length > 0)
                await session.ExecuteAsync("INSERT INTO eggrack_auth_role_permission(role_id,permission_id,effect) SELECT @RoleId,id,'ALLOW' FROM eggrack_auth_permission WHERE id IN @PermissionIds", new { RoleId = roleId, PermissionIds = distinctIds }, cancellationToken: cancellationToken);
            await session.ExecuteAsync("UPDATE eggrack_auth_staff s INNER JOIN eggrack_auth_staff_role sr ON sr.staff_id=s.id SET s.auth_version=s.auth_version+1 WHERE sr.role_id=@RoleId", new { RoleId = roleId }, cancellationToken: cancellationToken);
            await session.ExecuteAsync("INSERT INTO eggrack_auth_audit_log(operator_ref,action_code,target_type,target_ref,after_data) VALUES(@OperatorRef,'auth.permission.manage','role',@TargetRef,@AfterData)", new { OperatorRef = operatorRef, TargetRef = roleId.ToString(), AfterData = JsonSerializer.Serialize(new { permissionIds = distinctIds }) }, cancellationToken: cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        catch { await session.RollbackAsync(cancellationToken); throw; }
    }

    public async Task<IReadOnlyList<DepartmentOption>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id,department_name Name FROM eggrack_auth_department WHERE status=1 AND deleted_at IS NULL ORDER BY sort_order,department_name";
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<DepartmentOption>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DepartmentListItem>> GetDepartmentListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT d.id,d.parent_id ParentId,d.department_code Code,d.department_name Name,p.department_name ParentName,
              d.sort_order SortOrder,d.status=1 IsEnabled,
              COUNT(DISTINCT sd.staff_id) StaffCount,COUNT(DISTINCT sr.id) RoleCount
            FROM eggrack_auth_department d
            LEFT JOIN eggrack_auth_department p ON p.id=d.parent_id
            LEFT JOIN eggrack_auth_staff_department sd ON sd.department_id=d.id
            LEFT JOIN eggrack_auth_staff_role sr ON sr.department_id=d.id
            WHERE d.deleted_at IS NULL
            GROUP BY d.id,d.parent_id,d.department_code,d.department_name,p.department_name,d.sort_order,d.status
            ORDER BY d.sort_order,d.department_name
            """;
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<DepartmentListItem>(sql, cancellationToken: cancellationToken);
    }

    public async Task SaveDepartmentAsync(long? id, string code, string name, long? parentId, int sortOrder, string operatorRef, CancellationToken cancellationToken = default)
    {
        if (id.HasValue && parentId == id) throw new InvalidOperationException("部门不能将自己设为上级部门");
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        await session.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (id.HasValue)
            {
                var changed = await session.ExecuteAsync("UPDATE eggrack_auth_department SET department_code=@Code,department_name=@Name,parent_id=@ParentId,sort_order=@SortOrder WHERE id=@Id AND deleted_at IS NULL", new { Id = id.Value, Code = code, Name = name, ParentId = parentId, SortOrder = sortOrder }, cancellationToken: cancellationToken);
                if (changed != 1) throw new InvalidOperationException("部门不存在或已删除");
            }
            else
            {
                await session.ExecuteAsync("INSERT INTO eggrack_auth_department(department_code,department_name,parent_id,sort_order,status) VALUES(@Code,@Name,@ParentId,@SortOrder,1)", new { Code = code, Name = name, ParentId = parentId, SortOrder = sortOrder }, cancellationToken: cancellationToken);
            }
            await session.ExecuteAsync("INSERT INTO eggrack_auth_audit_log(operator_ref,action_code,target_type,target_ref,after_data) VALUES(@OperatorRef,'auth.department.manage','department',@TargetRef,@AfterData)", new { OperatorRef = operatorRef, TargetRef = id?.ToString() ?? code, AfterData = JsonSerializer.Serialize(new { code, name, parentId, sortOrder }) }, cancellationToken: cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        catch { await session.RollbackAsync(cancellationToken); throw; }
    }

    public async Task SetDepartmentEnabledAsync(long id, bool enabled, string operatorRef, CancellationToken cancellationToken = default)
    {
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        if (!enabled)
        {
            var usage = (await session.QueryAsync<CountRow>("SELECT (SELECT COUNT(*) FROM eggrack_auth_staff_department WHERE department_id=@Id)+(SELECT COUNT(*) FROM eggrack_auth_staff_role WHERE department_id=@Id)+(SELECT COUNT(*) FROM eggrack_auth_department WHERE parent_id=@Id AND deleted_at IS NULL) Value", new { Id = id }, cancellationToken: cancellationToken)).Single().Value;
            if (usage > 0) throw new InvalidOperationException("该部门仍有关联人员、角色授权或下级部门，不能停用");
        }
        var changed = await session.ExecuteAsync("UPDATE eggrack_auth_department SET status=@Status WHERE id=@Id AND deleted_at IS NULL", new { Id = id, Status = enabled ? 1 : 0 }, cancellationToken: cancellationToken);
        if (changed != 1) throw new InvalidOperationException("部门不存在或已删除");
        await session.ExecuteAsync("INSERT INTO eggrack_auth_audit_log(operator_ref,action_code,target_type,target_ref,after_data) VALUES(@OperatorRef,'auth.department.status','department',@TargetRef,@AfterData)", new { OperatorRef = operatorRef, TargetRef = id.ToString(), AfterData = JsonSerializer.Serialize(new { enabled }) }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<StaffRoleAssignment>> GetRoleAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT sr.id,sr.staff_id StaffId,r.role_code RoleCode,r.role_name RoleName,d.department_name DepartmentName
            FROM eggrack_auth_staff_role sr
            INNER JOIN eggrack_auth_role r ON r.id=sr.role_id
            LEFT JOIN eggrack_auth_department d ON d.id=sr.department_id
            WHERE (sr.valid_from IS NULL OR sr.valid_from<=UTC_TIMESTAMP(3))
              AND (sr.valid_until IS NULL OR sr.valid_until>UTC_TIMESTAMP(3))
            ORDER BY r.role_level,d.department_name
            """;
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        return await session.QueryAsync<StaffRoleAssignment>(sql, cancellationToken: cancellationToken);
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

    public async Task RevokeRoleAsync(long assignmentId, string operatorRef, CancellationToken cancellationToken = default)
    {
        await using var session = await databases.OpenMySqlAsync(DatabaseName, cancellationToken);
        var rows = await session.QueryAsync<RevocationRule>("""
            SELECT sr.staff_id StaffId,s.staff_ref StaffRef,r.role_code RoleCode
            FROM eggrack_auth_staff_role sr
            INNER JOIN eggrack_auth_staff s ON s.id=sr.staff_id
            INNER JOIN eggrack_auth_role r ON r.id=sr.role_id
            WHERE sr.id=@AssignmentId
            """, new { AssignmentId = assignmentId }, cancellationToken: cancellationToken);
        var assignment = rows.SingleOrDefault() ?? throw new InvalidOperationException("角色授权不存在");
        if (assignment.RoleCode == "super_admin" && assignment.StaffRef == operatorRef)
            throw new InvalidOperationException("不能撤销当前登录账号自己的超级管理员角色");
        if (assignment.RoleCode == "super_admin")
        {
            var counts = await session.QueryAsync<CountRow>("""
                SELECT COUNT(DISTINCT sr.staff_id) Value
                FROM eggrack_auth_staff_role sr
                INNER JOIN eggrack_auth_role r ON r.id=sr.role_id AND r.role_code='super_admin'
                INNER JOIN eggrack_auth_staff s ON s.id=sr.staff_id AND s.status=1 AND s.deleted_at IS NULL
                WHERE (sr.valid_until IS NULL OR sr.valid_until>UTC_TIMESTAMP(3))
                """, cancellationToken: cancellationToken);
            if (counts.Single().Value <= 1)
                throw new InvalidOperationException("系统必须至少保留一个启用的超级管理员");
        }

        await session.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            await session.ExecuteAsync("DELETE FROM eggrack_auth_staff_role WHERE id=@AssignmentId", new { AssignmentId = assignmentId }, cancellationToken: cancellationToken);
            await session.ExecuteAsync("UPDATE eggrack_auth_staff SET auth_version=auth_version+1 WHERE id=@StaffId", new { assignment.StaffId }, cancellationToken: cancellationToken);
            await session.ExecuteAsync(
                "INSERT INTO eggrack_auth_audit_log(operator_ref,action_code,target_type,target_ref,before_data) VALUES(@OperatorRef,'auth.role.revoke','staff',@TargetRef,@BeforeData)",
                new { OperatorRef = operatorRef, TargetRef = assignment.StaffId.ToString(), BeforeData = JsonSerializer.Serialize(new { assignment.RoleCode }) },
                cancellationToken: cancellationToken);
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
    private sealed record RevocationRule(long StaffId, string StaffRef, string RoleCode);
    private sealed record CountRow(long Value);
}

