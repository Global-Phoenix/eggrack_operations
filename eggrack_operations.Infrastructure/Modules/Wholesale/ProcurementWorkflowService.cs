using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Common.Models;
using Eggrack.Operations.Infrastructure.Database;
using System.Data.Common;
using System.Text.Json;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService
{
    private sealed record DocumentInquiryLink(uint PlanItemId,uint SupplierId);
    private sealed record DocumentSampleLink(uint PlanItemId,uint? SupplierId);
    public async Task<IReadOnlyList<SupplierListItem>> GetSuppliersAsync(string? keyword,CancellationToken token=default)
    {
        const string sql="SELECT id Id,supplier_name Name,supplier_code Code,address Address,legal_representative LegalRepresentative,contact_name ContactName,contact_phone ContactPhone,website Website,contact_json ContactJson,status Status,FROM_UNIXTIME(updated_at) UpdatedAtUtc FROM procurement_suppliers WHERE (@Keyword IS NULL OR supplier_name LIKE CONCAT('%',@Keyword,'%') OR supplier_code LIKE CONCAT('%',@Keyword,'%') OR contact_name LIKE CONCAT('%',@Keyword,'%') OR contact_phone LIKE CONCAT('%',@Keyword,'%') OR contact_json LIKE CONCAT('%',@Keyword,'%')) ORDER BY supplier_name";
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.QueryAsync<SupplierListItem>(sql,new{Keyword=string.IsNullOrWhiteSpace(keyword)?null:keyword.Trim()},cancellationToken:token);
    }

    public async Task<long> CreateSupplierAsync(CreateSupplierCommand command,CancellationToken token=default)
    {
        ValidateSupplier(command);
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        try
        {
            return await InsertSupplierAsync(db,command,DateTimeOffset.UtcNow.ToUnixTimeSeconds(),token);
        }
        catch(DbException error) when(error.Message.Contains("uk_procurement_supplier_code",StringComparison.OrdinalIgnoreCase)
          || error.Message.Contains("Duplicate entry",StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("供应商编码已存在。",error);
        }
    }

    public async Task<uint> CreatePlanSupplierAsync(uint planId,CreateSupplierCommand command,long staffId,CancellationToken token=default)
    {
        ValidateSupplier(command);
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        try
        {
            return await db.ExecuteInTransactionAsync<uint>(async transactionToken=>
            {
                var status=(await db.QueryAsync<byte>("SELECT status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken)).SingleOrDefault();
                if(status is not(1 or 2 or 7))throw new BusinessRuleException("采购计划当前状态不能新增供应商。","procurement.supplier.plan-state");
                var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var supplierId=await InsertSupplierAsync(db,command,now,transactionToken);
                await AddWorkflowEventAsync(db,planId,"supplier.created-from-plan",status,status,"supplier",supplierId,command.Name,staffId,now,transactionToken);
                return supplierId;
            },cancellationToken:token);
        }
        catch(DbException error) when(error.Message.Contains("uk_procurement_supplier_code",StringComparison.OrdinalIgnoreCase)
          || error.Message.Contains("Duplicate entry",StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("供应商编码已存在。",error);
        }
    }

    private static void ValidateSupplier(CreateSupplierCommand command)
    {
        if(string.IsNullOrWhiteSpace(command.Name))throw new InvalidOperationException("供应商名称不能为空。");
        if(!string.IsNullOrWhiteSpace(command.Website)&&(!Uri.TryCreate(command.Website,UriKind.Absolute,out var website)||website.Scheme is not("http" or "https")))
            throw new InvalidOperationException("供应商网址必须是有效的 http 或 https 地址。");
    }

    private static async Task<uint> InsertSupplierAsync(DatabaseSession db,CreateSupplierCommand command,long now,CancellationToken token)
    {
        await db.ExecuteAsync("INSERT procurement_suppliers(supplier_name,supplier_code,address,legal_representative,contact_name,contact_phone,website,status,created_at,updated_at) VALUES(@Name,@Code,@Address,@LegalRepresentative,@ContactName,@ContactPhone,@Website,'active',@Now,@Now)",new{Name=command.Name.Trim(),Code=Clean(command.Code),Address=Clean(command.Address),LegalRepresentative=Clean(command.LegalRepresentative),ContactName=Clean(command.ContactName),ContactPhone=Clean(command.ContactPhone),Website=Clean(command.Website),Now=now},cancellationToken:token);
        return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<long> RecordInquiryAsync(RecordInquiryCommand command,CancellationToken token=default)
    {
        if(command.UnitPriceCny<0||command.Moq<0||command.LeadDays<0) throw new InvalidOperationException("询价数据不能为负数。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanItemAsync(db,command.PlanItemId,token);
        await EnsureSourcingEditableAsync(db,command.PlanItemId,token);
        await db.ExecuteAsync("INSERT procurement_inquiries(plan_item_id,supplier_id,unit_price_cny,moq,lead_days,valid_until,terms,created_at) VALUES(@PlanItemId,@SupplierId,@UnitPriceCny,@Moq,@LeadDays,@ValidUntil,@Terms,@Now)",new{command.PlanItemId,command.SupplierId,command.UnitPriceCny,command.Moq,command.LeadDays,ValidUntil=command.ValidUntil?.ToString("yyyy-MM-dd"),command.Terms,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        return (await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<long> RecordSampleAsync(RecordSampleCommand command,CancellationToken token=default)
    {
        if(command.CostCny<0) throw new InvalidOperationException("样品成本不能为负数。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanItemAsync(db,command.PlanItemId,token);
        await EnsureSourcingEditableAsync(db,command.PlanItemId,token);
        await db.ExecuteAsync("INSERT procurement_samples(plan_item_id,supplier_id,status,cost_cny,tracking_number,notes,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@Status,@CostCny,@TrackingNumber,@Notes,@Now,@Now)",new{command.PlanItemId,command.SupplierId,command.Status,command.CostCny,command.TrackingNumber,command.Notes,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:token);
        return (await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }
    public async Task<QuoteDecisionResult> ApproveQuoteAsync(QuoteDecisionCommand command,string recipient,CancellationToken token=default)
        => await FinalApproveQuoteAsync(command,recipient,token);

    public async Task<QuoteDecisionResult> RejectQuoteAsync(QuoteDecisionCommand command,CancellationToken token=default)
        => await RejectPhaseOneQuoteAsync(command,token);

    public async Task<SourcingWorkspace> GetSourcingWorkspaceAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        const string requestSql="""
        SELECT p.request_id RequestId,p.request_version_id VersionId,p.plan_number PlanNumber,
          r.request_number RequestNumber,v.version_number VersionNumber,
          COALESCE(v.company_name,v.contact_name) CustomerName,v.email Email,
          FROM_UNIXTIME(v.submitted_at) SubmittedAtUtc
        FROM purchase_plans p
        JOIN purchase_requests r ON r.id=p.request_id
        JOIN purchase_request_versions v ON v.id=p.request_version_id AND v.request_id=p.request_id
        WHERE p.id=@PlanId
        """;
        var requestRow=(await db.QueryAsync<SourcingRequestRow>(requestSql,new{PlanId=planId},cancellationToken:token)).Single();
        const string requestItemSql="""
        SELECT id Id,version_id VersionId,product_name ProductName,quantity Quantity,
          quantity_unit Unit,sku Sku,brand Brand,description Description,specifications Specifications,
          color Color,size Size,packaging_requirements PackagingRequirements,
          customization_requirements CustomizationRequirements,customer_note CustomerNote
        FROM purchase_request_version_items
        WHERE request_id=@RequestId AND version_id=@VersionId ORDER BY sort_order,id
        """;
        var requestItems=await db.QueryAsync<PurchaseRequestVersionItemDetail>(requestItemSql,new{requestRow.RequestId,requestRow.VersionId},cancellationToken:token);
        const string attachmentSql="""
        SELECT id Id,version_id VersionId,original_name OriginalName,mime_type MimeType,file_size FileSize
        FROM purchase_request_files
        WHERE request_id=@RequestId AND version_id=@VersionId ORDER BY uploaded_at,id
        """;
        var attachments=await db.QueryAsync<PurchaseRequestAttachmentDetail>(attachmentSql,new{requestRow.RequestId,requestRow.VersionId},cancellationToken:token);
        var requestContext=new ProcurementRequestContext(requestRow.RequestNumber,requestRow.PlanNumber,
          requestRow.VersionNumber,requestRow.CustomerName,requestRow.Email,requestRow.SubmittedAtUtc,
          requestItems,attachments);
        var planItems=await db.QueryAsync<ProcurementPlanItemOption>("SELECT id Id,request_item_id RequestItemId,product_name ProductName,quantity Quantity,quantity_unit Unit,sku Sku,brand Brand,description Description,specifications Specifications,color Color,size Size,packaging_requirements PackagingRequirements,customization_requirements CustomizationRequirements,internal_note InternalNote FROM purchase_plan_items WHERE plan_id=@PlanId ORDER BY sort_order,id",new{PlanId=planId},cancellationToken:token);
        var suppliers=await db.QueryAsync<SupplierListItem>("SELECT id Id,supplier_name Name,supplier_code Code,address Address,legal_representative LegalRepresentative,contact_name ContactName,contact_phone ContactPhone,website Website,contact_json ContactJson,status Status,FROM_UNIXTIME(updated_at) UpdatedAtUtc FROM procurement_suppliers ORDER BY supplier_name",cancellationToken:token);
        var candidates=await db.QueryAsync<CandidateProductItem>("SELECT c.id Id,c.plan_item_id PlanItemId,c.supplier_id SupplierId,c.product_name ProductName,s.supplier_name SupplierName,c.reference_url ReferenceUrl,c.specification_json SpecificationJson,c.status Status FROM procurement_candidate_products c JOIN purchase_plan_items i ON i.id=c.plan_item_id LEFT JOIN procurement_suppliers s ON s.id=c.supplier_id WHERE i.plan_id=@PlanId ORDER BY c.updated_at DESC,c.id DESC",new{PlanId=planId},cancellationToken:token);
        var inquiries=await db.QueryAsync<InquiryItem>("SELECT q.id Id,q.plan_item_id PlanItemId,q.supplier_id SupplierId,i.product_name ProductName,s.supplier_name SupplierName,q.offered_product_name OfferedProductName,q.length_cm LengthCm,q.width_cm WidthCm,q.height_cm HeightCm,q.weight_kg WeightKg,q.color Color,q.size_details SizeDetails,q.parameter_details ParameterDetails,q.currency Currency,q.unit_price_cny UnitPrice,q.moq Moq,q.lead_days LeadDays,q.valid_until ValidUntil,q.terms Terms,q.status Status,q.notes Notes,q.revision_no RevisionNo,0 IsSelected FROM procurement_inquiries q JOIN purchase_plan_items i ON i.id=q.plan_item_id JOIN procurement_suppliers s ON s.id=q.supplier_id WHERE i.plan_id=@PlanId ORDER BY q.updated_at DESC,q.id DESC",new{PlanId=planId},cancellationToken:token);
        var samples=await db.QueryAsync<SampleItem>("SELECT x.id Id,x.plan_item_id PlanItemId,x.supplier_id SupplierId,i.product_name ProductName,s.supplier_name SupplierName,x.quantity Quantity,x.status Status,x.cost_cny CostCny,x.tracking_number TrackingNumber,x.notes Notes FROM procurement_samples x JOIN purchase_plan_items i ON i.id=x.plan_item_id LEFT JOIN procurement_suppliers s ON s.id=x.supplier_id WHERE i.plan_id=@PlanId ORDER BY x.updated_at DESC,x.id DESC",new{PlanId=planId},cancellationToken:token);
        var sampleFiles=await db.QueryAsync<SampleFileItem>("SELECT f.id Id,f.sample_id SampleId,f.file_type_id FileTypeId,CASE WHEN f.file_type IN('product','product_spec','specification','design','promotion','marketing','inspection','customer_source','customer_original','image','video','pdf','document') THEN '产品资料' WHEN f.file_type IN('quote','supplier_quote','internal_quote','internal_comparison','quotation') THEN '报价资料' WHEN f.file_type IN('sample','sample_photo','sample_image','sample_video','sample_file') THEN '样品资料' WHEN f.file_type IN('logistics','shipping','tracking','warehouse_logistics') THEN '物流资料' ELSE '其他' END FileTypeName,f.original_name OriginalName,f.description Description,f.mime_type MimeType,f.file_size FileSize,FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc,f.plan_item_id PlanItemId,f.supplier_id SupplierId,f.inquiry_id InquiryId,f.visibility_code Visibility FROM purchase_plan_files f LEFT JOIN procurement_file_types t ON t.id=f.file_type_id WHERE f.plan_id=@PlanId AND f.status<>3 ORDER BY f.uploaded_at DESC,f.id DESC",new{PlanId=planId},cancellationToken:token);
        var fileTypes=await db.QueryAsync<ProcurementManagedOption>("SELECT id Id,type_code Code,type_name Name,description Description,allowed_extensions AllowedExtensions,max_file_size_mb MaxFileSizeMb FROM procurement_file_types WHERE is_active=1 ORDER BY sort_order,id",cancellationToken:token);
        var mails=await db.QueryAsync<MailTaskItem>("SELECT id Id,recipient Recipient,status Status,template_code TemplateCode,attempts Attempts,last_error LastError,FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(sent_at) SentAtUtc FROM procurement_mail_tasks WHERE plan_id=@PlanId ORDER BY created_at DESC,id DESC",new{PlanId=planId},cancellationToken:token);
        var invoices=await db.QueryAsync<ProformaInvoiceSummary>("SELECT id Id,pi_number Number,CASE status WHEN 1 THEN 'Draft' WHEN 2 THEN 'Approved' WHEN 3 THEN 'Issued' ELSE 'Cancelled' END Status,total_amount TotalAmount,currency Currency,FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(issued_at) IssuedAtUtc FROM proforma_invoices WHERE purchase_plan_id=@PlanId ORDER BY id DESC LIMIT 1",new{PlanId=planId},cancellationToken:token);
        var invoice=invoices.SingleOrDefault();
        var pricing=invoice is null?null:await GetProformaInvoicePricingAsync(db,invoice.Id,token);
        return new(requestContext,planItems,suppliers,candidates,inquiries,samples,sampleFiles,fileTypes,mails,invoice,pricing);
    }

    public async Task<uint> SaveCandidateAsync(SaveCandidateProductCommand command,long staffId,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.ProductName)) throw new InvalidOperationException("候选产品名称不能为空。");
        if(!command.SupplierId.HasValue)throw new InvalidOperationException("请选择供应商。");
        var specificationJson=string.IsNullOrWhiteSpace(command.SpecificationDetails)
            ?null
            :JsonSerializer.Serialize(new{description=command.SpecificationDetails.Trim()});
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanItemAsync(db,command.PlanItemId,token);
        var planId=await EnsureSourcingEditableAsync(db,command.PlanItemId,token);
        return await db.ExecuteInTransactionAsync<uint>(async transactionToken=>
        {
            var supplierValid=(await db.QueryAsync<long>("SELECT COUNT(*) FROM procurement_suppliers WHERE id=@SupplierId AND status='active'",new{SupplierId=command.SupplierId.Value},cancellationToken:transactionToken)).Single();
            if(supplierValid!=1)throw new InvalidOperationException("供应商不存在或已停用。");
            if(command.Id.HasValue)await EnsureRecordPlanAsync(db,"procurement_candidate_products",command.Id.Value,planId,transactionToken);
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            uint id;
            if(command.Id.HasValue)
            {
                var changed=await db.ExecuteAsync("UPDATE procurement_candidate_products SET plan_item_id=@PlanItemId,supplier_id=@SupplierId,product_name=@ProductName,reference_url=@ReferenceUrl,specification_json=@SpecificationJson,status=@Status,updated_at=@Now WHERE id=@Id",new{command.Id,command.PlanItemId,command.SupplierId,ProductName=command.ProductName.Trim(),command.ReferenceUrl,SpecificationJson=specificationJson,command.Status,Now=now},cancellationToken:transactionToken);
                if(changed!=1)throw new InvalidOperationException("候选产品不存在。");
                id=command.Id.Value;
            }
            else
            {
                await db.ExecuteAsync("INSERT procurement_candidate_products(plan_item_id,supplier_id,product_name,reference_url,specification_json,status,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@ProductName,@ReferenceUrl,@SpecificationJson,@Status,@Now,@Now)",new{command.PlanItemId,command.SupplierId,ProductName=command.ProductName.Trim(),command.ReferenceUrl,SpecificationJson=specificationJson,command.Status,Now=now},cancellationToken:transactionToken);
                id=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            }
            await AddWorkflowEventAsync(db,planId,command.Id.HasValue?"candidate.updated":"candidate.bound",null,null,"candidate",id,command.ProductName,staffId,now,transactionToken);
            return id;
        },cancellationToken:token);
    }

    public async Task<uint> SaveInquiryAsync(SaveInquiryCommand command,CancellationToken token=default)
    {
        if(command.UnitPrice<0||command.Moq<0||command.LeadDays<0||command.LengthCm<0||command.WidthCm<0||command.HeightCm<0||command.WeightKg<0)throw new InvalidOperationException("询价数据和产品参数不能为负数。");if(command.Currency is not("CNY" or "USD"))throw new InvalidOperationException("币种仅支持 CNY 或 USD。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanItemAsync(db,command.PlanItemId,token);var editablePlanId=await EnsureSourcingEditableAsync(db,command.PlanItemId,token);if(command.Id.HasValue)await EnsureRecordPlanAsync(db,"procurement_inquiries",command.Id.Value,editablePlanId,token);var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if(command.Id.HasValue){var changed=await db.ExecuteAsync("UPDATE procurement_inquiries SET plan_item_id=@PlanItemId,supplier_id=@SupplierId,offered_product_name=@OfferedProductName,length_cm=@LengthCm,width_cm=@WidthCm,height_cm=@HeightCm,weight_kg=@WeightKg,color=@Color,size_details=@SizeDetails,parameter_details=@ParameterDetails,currency=@Currency,unit_price_cny=@UnitPrice,moq=@Moq,lead_days=@LeadDays,valid_until=@ValidUntil,terms=@Terms,status=@Status,notes=@Notes,updated_at=@Now WHERE id=@Id",new{command.Id,command.PlanItemId,command.SupplierId,command.OfferedProductName,command.LengthCm,command.WidthCm,command.HeightCm,command.WeightKg,command.Color,command.SizeDetails,command.ParameterDetails,command.Currency,command.UnitPrice,command.Moq,command.LeadDays,command.ValidUntil,command.Terms,command.Status,command.Notes,Now=now},cancellationToken:token);if(changed!=1)throw new InvalidOperationException("询价记录不存在。");return command.Id.Value;}
        await db.ExecuteAsync("INSERT procurement_inquiries(plan_item_id,supplier_id,offered_product_name,length_cm,width_cm,height_cm,weight_kg,color,size_details,parameter_details,currency,unit_price_cny,moq,lead_days,valid_until,terms,status,notes,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@OfferedProductName,@LengthCm,@WidthCm,@HeightCm,@WeightKg,@Color,@SizeDetails,@ParameterDetails,@Currency,@UnitPrice,@Moq,@LeadDays,@ValidUntil,@Terms,@Status,@Notes,@Now,@Now)",new{command.PlanItemId,command.SupplierId,command.OfferedProductName,command.LengthCm,command.WidthCm,command.HeightCm,command.WeightKg,command.Color,command.SizeDetails,command.ParameterDetails,command.Currency,command.UnitPrice,command.Moq,command.LeadDays,command.ValidUntil,command.Terms,command.Status,command.Notes,Now=now},cancellationToken:token);return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task<uint> SaveSampleAsync(SaveSampleCommand command,long staffId,CancellationToken token=default)
    {
        if(command.Quantity<=0||command.CostCny<0)throw new InvalidOperationException("样品数量必须大于零，费用不能为负数。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanItemAsync(db,command.PlanItemId,token);var editablePlanId=await EnsureSourcingEditableAsync(db,command.PlanItemId,token);if(command.Id.HasValue)await EnsureRecordPlanAsync(db,"procurement_samples",command.Id.Value,editablePlanId,token);
        return await db.ExecuteInTransactionAsync<uint>(async transactionToken=>
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();uint id;
            if(command.Id.HasValue)
            {
                var changed=await db.ExecuteAsync("UPDATE procurement_samples SET plan_item_id=@PlanItemId,supplier_id=@SupplierId,quantity=@Quantity,status=@Status,cost_cny=@CostCny,tracking_number=@TrackingNumber,notes=@Notes,updated_at=@Now WHERE id=@Id",new{command.Id,command.PlanItemId,command.SupplierId,command.Quantity,command.Status,command.CostCny,command.TrackingNumber,command.Notes,Now=now},cancellationToken:transactionToken);
                if(changed!=1)throw new InvalidOperationException("样品记录不存在。");id=command.Id.Value;
            }
            else
            {
                await db.ExecuteAsync("INSERT procurement_samples(plan_item_id,supplier_id,quantity,status,cost_cny,tracking_number,notes,created_at,updated_at) VALUES(@PlanItemId,@SupplierId,@Quantity,@Status,@CostCny,@TrackingNumber,@Notes,@Now,@Now)",new{command.PlanItemId,command.SupplierId,command.Quantity,command.Status,command.CostCny,command.TrackingNumber,command.Notes,Now=now},cancellationToken:transactionToken);
                id=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            }
            await AddWorkflowEventAsync(db,editablePlanId,command.Id.HasValue?"sample.updated":"sample.created",2,2,"sample",id,command.Notes,staffId,now,transactionToken);
            return id;
        },cancellationToken:token);
    }
    public async Task<uint> SavePlanItemAsync(uint planId,SaveProcurementPlanItemCommand command,ulong staffId,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.ProductName))throw new InvalidOperationException("计划产品名称不能为空。");
        if(command.Quantity<=0||string.IsNullOrWhiteSpace(command.Unit))throw new InvalidOperationException("计划产品数量必须大于 0，并填写单位。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,planId,token);
        var status=(await db.QueryAsync<byte>("SELECT status FROM purchase_plans WHERE id=@PlanId",new{PlanId=planId},cancellationToken:token)).Single();
        if(status>2)throw new InvalidOperationException("采购计划已进入成本或审批阶段，不能修改计划产品。");
        if(command.RequestItemId.HasValue)
        {
            var valid=(await db.QueryAsync<long>("SELECT COUNT(*) FROM purchase_request_version_items i JOIN purchase_plans p ON p.request_id=i.request_id AND p.request_version_id=i.version_id WHERE p.id=@PlanId AND i.id=@RequestItemId",new{PlanId=planId,RequestItemId=command.RequestItemId.Value},cancellationToken:token)).Single();
            if(valid!=1)throw new InvalidOperationException("引用的申请产品不属于当前计划申请版本。");
        }
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if(command.Id.HasValue)
        {
            var changed=await db.ExecuteAsync("UPDATE purchase_plan_items SET request_item_id=@RequestItemId,product_name=@ProductName,quantity=@Quantity,quantity_unit=@Unit,sku=@Sku,brand=@Brand,description=@Description,specifications=@Specifications,color=@Color,size=@Size,packaging_requirements=@PackagingRequirements,customization_requirements=@CustomizationRequirements,internal_note=@InternalNote,updated_by=@StaffId,updated_at=@Now WHERE id=@Id AND plan_id=@PlanId",new{command.Id,PlanId=planId,command.RequestItemId,ProductName=command.ProductName.Trim(),command.Quantity,Unit=command.Unit.Trim(),Sku=Clean(command.Sku),Brand=Clean(command.Brand),Description=Clean(command.Description),Specifications=Clean(command.Specifications),Color=Clean(command.Color),Size=Clean(command.Size),PackagingRequirements=Clean(command.PackagingRequirements),CustomizationRequirements=Clean(command.CustomizationRequirements),InternalNote=Clean(command.InternalNote),StaffId=staffId,Now=now},cancellationToken:token);
            if(changed!=1)throw new InvalidOperationException("计划产品不存在。");return command.Id.Value;
        }
        var sort=(await db.QueryAsync<uint>("SELECT COALESCE(MAX(sort_order),0)+1 FROM purchase_plan_items WHERE plan_id=@PlanId",new{PlanId=planId},cancellationToken:token)).Single();
        await db.ExecuteAsync("INSERT purchase_plan_items(plan_id,request_item_id,product_key,sort_order,product_name,sku,brand,description,quantity,quantity_unit,specifications,color,size,packaging_requirements,customization_requirements,internal_note,buyer_id,assigned_by,assigned_at,created_by,updated_by,created_at,updated_at) SELECT id,@RequestItemId,@ProductKey,@Sort,@ProductName,@Sku,@Brand,@Description,@Quantity,@Unit,@Specifications,@Color,@Size,@PackagingRequirements,@CustomizationRequirements,@InternalNote,assigned_buyer_id,@StaffId,@Now,@StaffId,@StaffId,@Now,@Now FROM purchase_plans WHERE id=@PlanId",new{PlanId=planId,command.RequestItemId,ProductKey=Guid.NewGuid().ToString(),Sort=sort,ProductName=command.ProductName.Trim(),command.Quantity,Unit=command.Unit.Trim(),Sku=Clean(command.Sku),Brand=Clean(command.Brand),Description=Clean(command.Description),Specifications=Clean(command.Specifications),Color=Clean(command.Color),Size=Clean(command.Size),PackagingRequirements=Clean(command.PackagingRequirements),CustomizationRequirements=Clean(command.CustomizationRequirements),InternalNote=Clean(command.InternalNote),StaffId=staffId,Now=now},cancellationToken:token);
        return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }
    public async Task DeletePlanItemAsync(uint planId,uint itemId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,planId,token);
        var refs=(await db.QueryAsync<long>("SELECT (SELECT COUNT(*) FROM procurement_candidate_products WHERE plan_item_id=@ItemId)+(SELECT COUNT(*) FROM procurement_inquiries WHERE plan_item_id=@ItemId)+(SELECT COUNT(*) FROM procurement_samples WHERE plan_item_id=@ItemId)",new{ItemId=itemId},cancellationToken:token)).Single();
        if(refs>0)throw new InvalidOperationException("该计划产品已有询价或样品记录，不能删除。");
        var changed=await db.ExecuteAsync("DELETE i FROM purchase_plan_items i JOIN purchase_plans p ON p.id=i.plan_id WHERE i.id=@ItemId AND i.plan_id=@PlanId AND p.status IN(1,2)",new{ItemId=itemId,PlanId=planId},cancellationToken:token);
        if(changed!=1)throw new InvalidOperationException("计划产品不存在或当前状态不能删除。");
    }
    public async Task<uint> AddProcurementDocumentAsync(uint planId,uint? planItemId,uint? supplierId,uint? inquiryId,uint? sampleId,string fileCategory,string visibility,string? title,string originalName,string storagePath,string checksum,string mimeType,uint fileSize,string? description,ulong staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        fileCategory=ProcurementFileCategories.Normalize(fileCategory);
        if(visibility is not("internal" or "supplier" or "customer"))throw new InvalidOperationException("文件可见范围无效。");
        if(planItemId.HasValue&&(await db.QueryAsync<long>("SELECT COUNT(*) FROM purchase_plan_items WHERE id=@PlanItemId AND plan_id=@PlanId",new{PlanItemId=planItemId.Value,PlanId=planId},cancellationToken:token)).Single()!=1)throw new InvalidOperationException("计划产品不属于当前采购计划。");
        if(supplierId.HasValue&&(await db.QueryAsync<long>("SELECT COUNT(*) FROM procurement_suppliers WHERE id=@SupplierId AND status='active'",new{SupplierId=supplierId.Value},cancellationToken:token)).Single()!=1)throw new InvalidOperationException("供应商不存在或已停用。");
        if(inquiryId.HasValue)
        {
            var inquiry=(await db.QueryAsync<DocumentInquiryLink>("SELECT q.plan_item_id PlanItemId,q.supplier_id SupplierId FROM procurement_inquiries q JOIN purchase_plan_items i ON i.id=q.plan_item_id WHERE q.id=@InquiryId AND i.plan_id=@PlanId",new{InquiryId=inquiryId.Value,PlanId=planId},cancellationToken:token)).SingleOrDefault()??throw new InvalidOperationException("供应商报价不属于当前采购计划。");
            if(planItemId.HasValue&&planItemId.Value!=inquiry.PlanItemId)throw new InvalidOperationException("文件关联的计划产品与报价不一致。");
            if(supplierId.HasValue&&supplierId.Value!=inquiry.SupplierId)throw new InvalidOperationException("文件关联的供应商与报价不一致。");
            planItemId??=inquiry.PlanItemId;supplierId??=inquiry.SupplierId;
        }
        if(sampleId.HasValue)
        {
            var sample=(await db.QueryAsync<DocumentSampleLink>("SELECT x.plan_item_id PlanItemId,x.supplier_id SupplierId FROM procurement_samples x JOIN purchase_plan_items i ON i.id=x.plan_item_id WHERE x.id=@SampleId AND i.plan_id=@PlanId",new{SampleId=sampleId.Value,PlanId=planId},cancellationToken:token)).SingleOrDefault()??throw new InvalidOperationException("样品记录不属于当前采购计划。");
            if(planItemId.HasValue&&planItemId.Value!=sample.PlanItemId)throw new InvalidOperationException("文件关联的计划产品与样品不一致。");
            if(supplierId.HasValue&&sample.SupplierId.HasValue&&supplierId.Value!=sample.SupplierId.Value)throw new InvalidOperationException("文件关联的供应商与样品不一致。");
            planItemId??=sample.PlanItemId;supplierId??=sample.SupplierId;
        }
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.ExecuteAsync("INSERT purchase_plan_files(plan_id,plan_item_id,supplier_id,inquiry_id,sample_id,file_type,file_type_id,visibility_code,is_customer_visible,customer_visible_by,customer_visible_at,title,description,original_name,storage_path,checksum_sha256,mime_type,file_size,status,uploaded_by,updated_by,uploaded_at,updated_at) VALUES(@PlanId,@PlanItemId,@SupplierId,@InquiryId,@SampleId,@FileType,NULL,@Visibility,@CustomerVisible,@CustomerVisibleBy,@CustomerVisibleAt,@Title,@Description,@OriginalName,@StoragePath,@Checksum,@MimeType,@FileSize,1,@StaffId,@StaffId,@Now,@Now)",new{PlanId=planId,PlanItemId=planItemId,SupplierId=supplierId,InquiryId=inquiryId,SampleId=sampleId,FileType=fileCategory,Visibility=visibility,CustomerVisible=visibility=="customer"?1:0,CustomerVisibleBy=visibility=="customer"?(ulong?)staffId:null,CustomerVisibleAt=visibility=="customer"?(long?)now:null,Title=Clean(title)??originalName,OriginalName=originalName,Description=Clean(description),StoragePath=storagePath,Checksum=checksum,MimeType=mimeType,FileSize=fileSize,StaffId=staffId,Now=now},cancellationToken:token);
        return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }
    private sealed record PlanForApproval(uint Id,uint RequestId,uint RequestVersionId,decimal? TotalCostUsd,decimal? ProfitRate,byte Status);
    private sealed class SourcingRequestRow
    {
        public uint RequestId { get; set; }
        public uint VersionId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string PlanNumber { get; set; } = string.Empty;
        public uint VersionNumber { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime SubmittedAtUtc { get; set; }
    }
    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}
