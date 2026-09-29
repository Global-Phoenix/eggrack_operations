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
          CASE
            WHEN r.user_id IS NOT NULL THEN CONCAT(
              COALESCE(NULLIF(TRIM(CONCAT_WS(' ', u.FirstName, u.LastName)), ''), CONCAT('会员 #', r.user_id)),
              CASE
                WHEN COALESCE(NULLIF(TRIM(u.Email), ''), NULLIF(TRIM(r.email), '')) IS NULL THEN ''
                ELSE CONCAT(' · ', COALESCE(NULLIF(TRIM(u.Email), ''), NULLIF(TRIM(r.email), '')))
              END
            )
            ELSE CONCAT('游客 · ', COALESCE(NULLIF(TRIM(r.email), ''), NULLIF(TRIM(r.guest_id), ''), '未登记邮箱'))
          END CategoryName,
          '客户提交' UploadedBy, FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc, 1 Status
        FROM purchase_request_files f
        JOIN purchase_requests r ON r.id = f.request_id
        JOIN purchase_request_versions v ON v.id = f.version_id AND v.request_id = f.request_id
        LEFT JOIN `user` u ON u.UserId = r.user_id
        WHERE NOT EXISTS (
          SELECT 1 FROM purchase_request_files newer
          WHERE newer.request_id = f.request_id
            AND newer.storage_path = f.storage_path
            AND newer.id > f.id
        )
        UNION ALL
        SELECT CONCAT('plan:', f.id) ItemKey, 'plan' SourceKind, f.id FileId,
          f.original_name OriginalName, f.mime_type MimeType, f.file_size FileSize,
          p.plan_number SourceNumber,
          CASE
            WHEN d.id IS NULL THEN '未分配部门'
            WHEN parent_department.id IS NULL THEN d.department_name
            ELSE CONCAT(parent_department.department_name, ' / ', d.department_name)
          END CategoryName,
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
        LEFT JOIN eggrack_auth_department parent_department
          ON parent_department.id = d.parent_id AND parent_department.deleted_at IS NULL
        ORDER BY UploadedAtUtc DESC, FileId DESC
        """;

        await using var db = await databases.OpenMySqlAsync("Eggrack", token);
        return await db.QueryAsync<FileCenterFileItem>(sql, cancellationToken: token);
    }

    public async Task<FileCenterStoredFile?> GetStoredFileAsync(string sourceKind, uint fileId, CancellationToken token = default)
    {
        var sql = sourceKind switch
        {
            "request" => """
                SELECT 'request' SourceKind, id FileId, original_name OriginalName,
                  storage_path StoragePath, mime_type MimeType, file_size FileSize
                FROM purchase_request_files WHERE id = @FileId LIMIT 1
                """,
            "plan" => """
                SELECT 'plan' SourceKind, id FileId, original_name OriginalName,
                  storage_path StoragePath, mime_type MimeType, file_size FileSize
                FROM purchase_plan_files WHERE id = @FileId AND status <> 3 LIMIT 1
                """,
            _ => null
        };
        if (sql is null) return null;
        await using var db = await databases.OpenMySqlAsync("Eggrack", token);
        var rows = await db.QueryAsync<FileCenterStoredFile>(sql, new { FileId = fileId }, cancellationToken: token);
        return rows.SingleOrDefault();
    }
}

public sealed record FileCenterStoredFile(
    string SourceKind,
    uint FileId,
    string OriginalName,
    string StoragePath,
    string MimeType,
    uint FileSize);