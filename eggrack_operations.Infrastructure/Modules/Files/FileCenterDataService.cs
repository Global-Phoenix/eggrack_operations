using Eggrack.Operations.Application.Modules.Files;
using Eggrack.Operations.Infrastructure.Database;
using Eggrack.Operations.Infrastructure.Modules.Wholesale;

namespace Eggrack.Operations.Infrastructure.Modules.Files;

public sealed class FileCenterDataService(DatabaseSessionFactory databases,ProcurementScopePolicy scopePolicy)
{
    public async Task<IReadOnlyList<FileCenterFileItem>> GetPurchaseFilesAsync(bool includeInternal,CancellationToken token = default)
    {
        const string sql = """
        SELECT CONCAT('request:', f.id) ItemKey, 'request' SourceKind, r.id SourceId, f.id FileId,
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
          '客户提交' UploadedBy, 'customer' VisibilityCode,
          FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc, 1 Status
        FROM purchase_request_files f
        JOIN purchase_requests r ON r.id = f.request_id
        JOIN purchase_request_versions v ON v.id = f.version_id AND v.request_id = f.request_id
        LEFT JOIN purchase_plans access_plan ON access_plan.request_id=r.id
        LEFT JOIN `user` u ON u.UserId = r.user_id
        WHERE NOT EXISTS (
          SELECT 1 FROM purchase_request_files newer
          WHERE newer.request_id = f.request_id
            AND newer.storage_path = f.storage_path
            AND newer.id > f.id
        )
          AND (@ScopeAll=1 OR (@ScopeSelf=1 AND access_plan.assigned_buyer_id=@ScopeStaffId)
            OR access_plan.department_id IN @ScopeDepartmentIds)
        UNION ALL
        SELECT CONCAT('plan:', f.id) ItemKey, 'plan' SourceKind, p.id SourceId, f.id FileId,
          f.original_name OriginalName, f.mime_type MimeType, f.file_size FileSize,
          p.plan_number SourceNumber,
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
          COALESCE(s.staff_name, CONCAT('员工 #', f.uploaded_by)) UploadedBy,
          f.visibility_code VisibilityCode, FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc, f.status Status
        FROM purchase_plan_files f
        JOIN purchase_plans p ON p.id = f.plan_id
        JOIN purchase_requests r ON r.id = p.request_id
        LEFT JOIN `user` u ON u.UserId = r.user_id
        LEFT JOIN eggrack_auth_staff s
          ON s.id = f.uploaded_by AND s.deleted_at IS NULL
        WHERE f.status=1
          AND (@IncludeInternal=1 OR f.visibility_code='customer' OR f.is_customer_visible=1)
          AND (@ScopeAll=1 OR (@ScopeSelf=1 AND p.assigned_buyer_id=@ScopeStaffId)
          OR p.department_id IN @ScopeDepartmentIds)
        ORDER BY UploadedAtUtc DESC, FileId DESC
        """;

        await using var db = await databases.OpenMySqlAsync("Eggrack", token);
        var scope=scopePolicy.Current();
        return await db.QueryAsync<FileCenterFileItem>(sql,
            ProcurementScopePolicy.Params(scope,new{IncludeInternal=includeInternal}),cancellationToken:token);
    }

    public async Task<FileCenterStoredFile?> GetStoredFileAsync(string sourceKind, uint fileId, CancellationToken token = default)
    {
        var sql = sourceKind switch
        {
            "request" => """
                SELECT 'request' SourceKind, id FileId, original_name OriginalName,
                  storage_path StoragePath, mime_type MimeType, file_size FileSize,
                  'customer' VisibilityCode
                FROM purchase_request_files WHERE id = @FileId LIMIT 1
                """,
            "plan" => """
                SELECT 'plan' SourceKind, id FileId, original_name OriginalName,
                  storage_path StoragePath, mime_type MimeType, file_size FileSize,
                  visibility_code VisibilityCode
                FROM purchase_plan_files WHERE id = @FileId AND status = 1 LIMIT 1
                """,
            _ => null
        };
        if (sql is null) return null;
        await using var db = await databases.OpenMySqlAsync("Eggrack", token);
        if(sourceKind=="request")
        {
            var requestId=(await db.QueryAsync<uint>(
                "SELECT request_id FROM purchase_request_files WHERE id=@FileId LIMIT 1",
                new{FileId=fileId},cancellationToken:token)).SingleOrDefault();
            if(requestId==0)return null;
            await scopePolicy.EnsureRequestAsync(db,requestId,token);
        }
        else
        {
            var planId=(await db.QueryAsync<uint>(
                "SELECT plan_id FROM purchase_plan_files WHERE id=@FileId AND status=1 LIMIT 1",
                new{FileId=fileId},cancellationToken:token)).SingleOrDefault();
            if(planId==0)return null;
            await scopePolicy.EnsurePlanAsync(db,planId,token);
        }
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
    uint FileSize,
    string VisibilityCode)
{
    public bool IsInternal => SourceKind == "plan" &&
        !VisibilityCode.Equals("customer",StringComparison.OrdinalIgnoreCase);
}
