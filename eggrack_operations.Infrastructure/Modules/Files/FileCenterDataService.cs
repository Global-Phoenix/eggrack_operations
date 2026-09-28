using Eggrack.Operations.Application.Modules.Files;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Files;

public sealed class FileCenterDataService(DatabaseSessionFactory databases)
{
    public async Task<IReadOnlyList<FileCenterFileItem>> GetPurchaseFilesAsync(CancellationToken token = default)
    {
        const string sql = """
        SELECT CONCAT('request:', f.id) ItemKey, 'request' SourceKind, f.id FileId,
          f.original_name OriginalName, f.mime_type MimeType, f.file_size FileSize,
          r.request_number SourceNumber,
          COALESCE(d.department_name, '未分配部门') DepartmentName,
          '采购申请' UploadedBy, FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc, 1 Status
        FROM purchase_request_files f
        JOIN purchase_requests r ON r.id = f.request_id
        LEFT JOIN purchase_plans p ON p.id = (
          SELECT p2.id FROM purchase_plans p2
          WHERE p2.request_id = f.request_id
          ORDER BY p2.updated_at DESC, p2.id DESC LIMIT 1
        )
        LEFT JOIN eggrack_auth_staff_department sd
          ON sd.staff_id = p.assigned_buyer_id AND sd.is_primary = 1
        LEFT JOIN eggrack_auth_department d
          ON d.id = sd.department_id AND d.deleted_at IS NULL
        UNION ALL
        SELECT CONCAT('plan:', f.id) ItemKey, 'plan' SourceKind, f.id FileId,
          f.original_name OriginalName, f.mime_type MimeType, f.file_size FileSize,
          p.plan_number SourceNumber,
          COALESCE(d.department_name, '未分配部门') DepartmentName,
          COALESCE(s.staff_name, CONCAT('员工 #', f.uploaded_by)) UploadedBy,
          FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc, f.status Status
        FROM purchase_plan_files f
        JOIN purchase_plans p ON p.id = f.plan_id
        LEFT JOIN eggrack_auth_staff s
          ON s.id = f.uploaded_by AND s.deleted_at IS NULL
        LEFT JOIN eggrack_auth_staff_department sd
          ON sd.staff_id = p.assigned_buyer_id AND sd.is_primary = 1
        LEFT JOIN eggrack_auth_department d
          ON d.id = sd.department_id AND d.deleted_at IS NULL
        ORDER BY UploadedAtUtc DESC, FileId DESC
        """;

        await using var db = await databases.OpenMySqlAsync("Eggrack", token);
        return await db.QueryAsync<FileCenterFileItem>(sql, cancellationToken: token);
    }
}
