using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Common.Models;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService
{
    private sealed record PhaseOnePlan(byte Status,decimal CnyPerUsd,decimal ProfitRate,decimal TotalCostCny,decimal TotalCostUsd,decimal SuggestedQuoteUsd);
    private sealed record ProductCostRow(uint PlanItemId,string ProductName,decimal Quantity,string Unit,uint? InquiryId,string? SupplierName,string? Currency,decimal? UnitPrice,bool IsCurrent,string? InquiryStatus);
    private sealed record SnapshotIdentity(uint Id,uint RevisionNo,ulong SubmittedBy,decimal TotalCostUsd,decimal SuggestedQuoteUsd);
    private sealed record ReviewIdentity(ulong ActedBy,decimal ProposedQuoteUsd);
    private sealed record InquiryRevision(uint Id,uint PlanItemId,uint SupplierId,uint RevisionNo,bool IsCurrent);

    public async Task<uint> SaveInquiryRevisionAsync(SaveInquiryCommand command,long staffId,CancellationToken token=default)
    {
        var currency=(command.Currency??string.Empty).Trim().ToUpperInvariant();
        var status=(command.Status??"draft").Trim().ToLowerInvariant();
        if(command.UnitPrice<0||command.Moq<0||command.LeadDays<0||command.LengthCm<0||command.WidthCm<0||command.HeightCm<0||command.WeightKg<0)
            throw new InvalidOperationException("询价数据和产品参数不能为负数。");
        if(currency is not("CNY" or "USD"))throw new InvalidOperationException("币种仅支持 CNY 或 USD。");
        if(status is not("draft" or "sent" or "quoted" or "closed"))throw new InvalidOperationException("报价状态无效。");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanItemAsync(db,command.PlanItemId,token);
        var planId=await EnsureSourcingEditableAsync(db,command.PlanItemId,token);
        return await db.ExecuteInTransactionAsync<uint>(async transactionToken=>
        {
            uint revision=1;uint? previousId=null;
            if(command.Id.HasValue)
            {
                var old=(await db.QueryAsync<InquiryRevision>("SELECT id Id,plan_item_id PlanItemId,supplier_id SupplierId,revision_no RevisionNo,is_current IsCurrent FROM procurement_inquiries WHERE id=@Id FOR UPDATE",new{command.Id},cancellationToken:transactionToken)).SingleOrDefault()
                    ??throw new BusinessRuleException("要修改的报价不存在。","procurement.inquiry.missing");
                if(old.PlanItemId!=command.PlanItemId)throw new BusinessRuleException("报价不能移动到其他计划产品。","procurement.inquiry.cross-item");
                if(!old.IsCurrent)throw new BusinessRuleException("该报价已有新版本，请刷新后再操作。","procurement.inquiry.stale");
                var changed=await db.ExecuteAsync("UPDATE procurement_inquiries SET is_current=0,status='superseded',updated_at=@Now WHERE id=@Id AND is_current=1",new{command.Id,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:transactionToken);
                if(changed!=1)throw new BusinessRuleException("报价已被其他操作修改，请刷新后重试。","procurement.inquiry.concurrent-change");
                var deselected=await db.ExecuteAsync("DELETE FROM procurement_selected_inquiries WHERE inquiry_id=@Id",new{command.Id},cancellationToken:transactionToken);
                if(deselected>0)
                    await db.ExecuteAsync("UPDATE purchase_plan_items SET purchase_unit_price_cny=NULL,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanItemId",
                        new{command.PlanItemId,StaffId=staffId,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:transactionToken);
                revision=old.RevisionNo+1;previousId=old.Id;
            }
            var supplierValid=(await db.QueryAsync<long>("SELECT COUNT(*) FROM procurement_suppliers WHERE id=@SupplierId AND status='active'",new{command.SupplierId},cancellationToken:transactionToken)).Single();
            if(supplierValid!=1)throw new BusinessRuleException("供应商不存在或已停用。","procurement.supplier.inactive");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await LinkPlanSupplierAsync(db,planId,command.SupplierId,staffId,"inquiry",now,transactionToken);
            await db.ExecuteAsync("""
            INSERT procurement_inquiries(revision_no,previous_inquiry_id,is_current,plan_item_id,supplier_id,offered_product_name,length_cm,width_cm,height_cm,weight_kg,color,size_details,parameter_details,currency,unit_price_cny,moq,lead_days,valid_until,terms,status,notes,created_at,updated_at)
            VALUES(@Revision,@PreviousId,1,@PlanItemId,@SupplierId,@OfferedProductName,@LengthCm,@WidthCm,@HeightCm,@WeightKg,@Color,@SizeDetails,@ParameterDetails,@Currency,@UnitPrice,@Moq,@LeadDays,@ValidUntil,@Terms,@Status,@Notes,@Now,@Now)
            """,new{Revision=revision,PreviousId=previousId,command.PlanItemId,command.SupplierId,command.OfferedProductName,command.LengthCm,command.WidthCm,command.HeightCm,command.WeightKg,command.Color,command.SizeDetails,command.ParameterDetails,Currency=currency,command.UnitPrice,command.Moq,command.LeadDays,command.ValidUntil,command.Terms,Status=status,command.Notes,Now=now},cancellationToken:transactionToken);
            var id=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            await AddWorkflowEventAsync(db,planId,previousId.HasValue?"inquiry.revised":"inquiry.created",null,null,"inquiry",id,$"供应商报价 V{revision}",staffId,now,transactionToken);
            return id;
        },cancellationToken:token);
    }

    public async Task SelectInquiryAsync(uint planId,SelectInquiryCommand command,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        await db.ExecuteInTransactionAsync(async transactionToken=>
        {
            var valid=(await db.QueryAsync<long>("""
            SELECT COUNT(*) FROM procurement_inquiries q
            JOIN purchase_plan_items i ON i.id=q.plan_item_id JOIN purchase_plans p ON p.id=i.plan_id
            WHERE p.id=@PlanId AND p.status=2 AND i.id=@PlanItemId AND q.id=@InquiryId
              AND q.plan_item_id=i.id AND q.is_current=1 AND q.status='quoted' AND q.unit_price_cny>0
            FOR UPDATE
            """,new{PlanId=planId,command.PlanItemId,command.InquiryId},cancellationToken:transactionToken)).Single();
            if(valid!=1)throw new BusinessRuleException("只能选择当前计划产品中有效且已报价的最新报价。","procurement.inquiry.select-invalid");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("""
            INSERT INTO procurement_selected_inquiries(plan_item_id,inquiry_id,selected_by,selected_at)
            VALUES(@PlanItemId,@InquiryId,@StaffId,@Now)
            ON DUPLICATE KEY UPDATE inquiry_id=VALUES(inquiry_id),selected_by=VALUES(selected_by),selected_at=VALUES(selected_at)
            """,new{command.PlanItemId,command.InquiryId,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            await db.ExecuteAsync("""
            UPDATE purchase_plan_items i
            JOIN purchase_plans p ON p.id=i.plan_id
            JOIN procurement_inquiries q ON q.id=@InquiryId AND q.plan_item_id=i.id
            SET i.purchase_unit_price_cny=CASE WHEN q.currency='USD'
                  THEN q.unit_price_cny*COALESCE(NULLIF(p.cny_per_usd,0),7.12)
                  ELSE q.unit_price_cny END,
                i.updated_by=@StaffId,i.updated_at=@Now
            WHERE i.id=@PlanItemId AND i.plan_id=@PlanId
            """,new{PlanId=planId,command.PlanItemId,command.InquiryId,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            await AddWorkflowEventAsync(db,planId,"inquiry.selected",2,2,"inquiry",command.InquiryId,null,staffId,now,transactionToken);
        },cancellationToken:token);
    }

    public async Task<SourcingWorkspace> GetPhaseOneWorkspaceAsync(uint planId,CancellationToken token=default)
    {
        var workspace=await GetSourcingWorkspaceAsync(planId,token);
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var status=(await db.QueryAsync<byte>("SELECT status FROM purchase_plans WHERE id=@PlanId",new{PlanId=planId},cancellationToken:token)).Single();
        var inquiries=await db.QueryAsync<InquiryItem>("""
        SELECT q.id Id,q.plan_item_id PlanItemId,q.supplier_id SupplierId,i.product_name ProductName,s.supplier_name SupplierName,
          q.offered_product_name OfferedProductName,q.length_cm LengthCm,q.width_cm WidthCm,q.height_cm HeightCm,q.weight_kg WeightKg,
          q.color Color,q.size_details SizeDetails,q.parameter_details ParameterDetails,q.currency Currency,q.unit_price_cny UnitPrice,
          q.moq Moq,q.lead_days LeadDays,q.valid_until ValidUntil,q.terms Terms,q.status Status,q.notes Notes,q.revision_no RevisionNo,
          EXISTS(SELECT 1 FROM procurement_selected_inquiries x WHERE x.inquiry_id=q.id) IsSelected
        FROM procurement_inquiries q JOIN purchase_plan_items i ON i.id=q.plan_item_id JOIN procurement_suppliers s ON s.id=q.supplier_id
        WHERE i.plan_id=@PlanId AND q.is_current=1 ORDER BY i.sort_order,q.updated_at DESC,q.id DESC
        """,new{PlanId=planId},cancellationToken:token);
        var documents=await db.QueryAsync<SampleFileItem>("""
        SELECT f.id Id,f.sample_id SampleId,f.file_type_id FileTypeId,COALESCE(t.type_name,'其他附件') FileTypeName,
          f.original_name OriginalName,f.description Description,f.mime_type MimeType,f.file_size FileSize,
          FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc,f.plan_item_id PlanItemId,f.supplier_id SupplierId,
          f.inquiry_id InquiryId,f.visibility_code Visibility
        FROM purchase_plan_files f LEFT JOIN procurement_file_types t ON t.id=f.file_type_id
        WHERE f.plan_id=@PlanId AND f.status<>3 ORDER BY f.uploaded_at DESC,f.id DESC
        """,new{PlanId=planId},cancellationToken:token);
        var reviews=await db.QueryAsync<ProcurementQuoteReviewItem>("SELECT id Id,stage Stage,decision Decision,proposed_quote_usd ProposedQuoteUsd,actual_profit_rate ActualProfitRate,note Note,acted_by ActedBy,FROM_UNIXTIME(acted_at) ActedAtUtc FROM procurement_quote_reviews WHERE plan_id=@PlanId ORDER BY acted_at DESC,id DESC",new{PlanId=planId},cancellationToken:token);
        var events=await db.QueryAsync<ProcurementWorkflowEventItem>("SELECT id Id,event_code EventCode,from_status FromStatus,to_status ToStatus,note Note,actor_id ActorId,FROM_UNIXTIME(created_at) CreatedAtUtc FROM procurement_workflow_events WHERE plan_id=@PlanId ORDER BY created_at DESC,id DESC LIMIT 100",new{PlanId=planId},cancellationToken:token);
        return workspace with{Inquiries=inquiries,SampleFiles=documents,PlanStatus=status,QuoteReviews=reviews,WorkflowEvents=events};
    }

    public async Task<ProcurementCosts> GetPhaseOneCostsAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,planId,token);
        var plan=(await db.QueryAsync<PhaseOnePlan>("SELECT status Status,COALESCE(cny_per_usd,7.12) CnyPerUsd,COALESCE(profit_rate,0.15) ProfitRate,COALESCE(total_cost_cny,0) TotalCostCny,COALESCE(total_cost_usd,0) TotalCostUsd,0 SuggestedQuoteUsd FROM purchase_plans WHERE id=@PlanId",new{PlanId=planId},cancellationToken:token)).Single();
        var items=await db.QueryAsync<ProcurementCostItem>("SELECT id Id,cost_type_id TypeId,cost_name Name,amount_cny AmountCny,sort_order SortOrder FROM procurement_plan_cost_items WHERE plan_id=@PlanId AND cost_scope='shared' ORDER BY sort_order,id",new{PlanId=planId},cancellationToken:token);
        var types=await db.QueryAsync<ProcurementManagedOption>("SELECT id Id,type_code Code,type_name Name,description Description,NULL AllowedExtensions,NULL MaxFileSizeMb FROM procurement_cost_types WHERE is_active=1 AND type_code<>'product' ORDER BY sort_order,id",cancellationToken:token);
        var products=await GetProductCostsAsync(db,planId,plan.CnyPerUsd,false,token);
        var snapshot=(await db.QueryAsync<ProcurementCostSnapshotSummary>("SELECT id Id,revision_no RevisionNo,status Status,product_cost_cny ProductCostCny,shared_cost_cny SharedCostCny,total_cost_cny TotalCostCny,total_cost_usd TotalCostUsd,suggested_quote_usd SuggestedQuoteUsd,submitted_by SubmittedBy,FROM_UNIXTIME(submitted_at) SubmittedAtUtc FROM procurement_cost_snapshots WHERE plan_id=@PlanId ORDER BY revision_no DESC LIMIT 1",new{PlanId=planId},cancellationToken:token)).SingleOrDefault();
        var total=products.Sum(x=>x.AmountCny)+items.Sum(x=>x.AmountCny);var usd=decimal.Round(total/plan.CnyPerUsd,2,MidpointRounding.AwayFromZero);var quote=decimal.Round(usd/(1-plan.ProfitRate),2,MidpointRounding.AwayFromZero);
        return new(plan.CnyPerUsd,plan.ProfitRate,total,usd,quote,items,types,products,snapshot);
    }

    public async Task<ProcurementCosts> SaveDraftCostsAsync(uint planId,SaveProcurementCostsCommand command,long staffId,CancellationToken token=default)
    {
        ValidateCostInput(command);await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,planId,token);
        var types=await db.QueryAsync<ProcurementManagedOption>("SELECT id Id,type_code Code,type_name Name,description Description,NULL AllowedExtensions,NULL MaxFileSizeMb FROM procurement_cost_types WHERE is_active=1 AND type_code<>'product' ORDER BY sort_order,id",cancellationToken:token);
        var shared=NormalizeSharedCosts(command,types.ToDictionary(x=>x.Id));
        await db.ExecuteInTransactionAsync(async transactionToken=>
        {
            var status=(await db.QueryAsync<byte>("SELECT status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken)).Single();
            if(status!=ProcurementWorkflowStatus.Sourcing)throw new BusinessRuleException("只有寻源阶段可以修改成本草稿。","procurement.cost.edit-state");
            var products=await GetProductCostsAsync(db,planId,command.CnyPerUsd,true,transactionToken);
            var productTotal=products.Sum(x=>x.AmountCny);var sharedTotal=shared.Sum(x=>x.AmountCny);var total=productTotal+sharedTotal;var usd=decimal.Round(total/command.CnyPerUsd,2,MidpointRounding.AwayFromZero);var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("DELETE FROM procurement_plan_cost_items WHERE plan_id=@PlanId AND cost_scope='shared'",new{PlanId=planId},cancellationToken:transactionToken);
            for(var index=0;index<shared.Length;index++)await db.ExecuteAsync("INSERT procurement_plan_cost_items(plan_id,cost_type_id,cost_scope,cost_name,amount_cny,sort_order,created_by,updated_by,created_at,updated_at) VALUES(@PlanId,@TypeId,'shared',@Name,@Amount,@Sort,@StaffId,@StaffId,@Now,@Now)",new{PlanId=planId,shared[index].TypeId,shared[index].Name,Amount=shared[index].AmountCny,Sort=index,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            await db.ExecuteAsync("UPDATE purchase_plans SET product_cost_cny=@ProductTotal,other_cost_cny=@SharedTotal,total_cost_cny=@Total,cny_per_usd=@Rate,total_cost_usd=@Usd,profit_method=2,profit_rate=@Profit,cost_updated_by=@StaffId,cost_updated_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=2",new{PlanId=planId,ProductTotal=productTotal,SharedTotal=sharedTotal,Total=total,Rate=command.CnyPerUsd,Usd=usd,Profit=command.ProfitRate,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            await AddWorkflowEventAsync(db,planId,"cost.draft-saved",2,2,null,null,null,staffId,now,transactionToken);
        },cancellationToken:token);
        return await GetPhaseOneCostsAsync(planId,token);
    }

    public async Task<SubmitCostReviewResult> SubmitCostReviewAsync(uint planId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.ExecuteInTransactionAsync<SubmitCostReviewResult>(async transactionToken=>
        {
            var plan=(await db.QueryAsync<PhaseOnePlan>("SELECT status Status,cny_per_usd CnyPerUsd,profit_rate ProfitRate,total_cost_cny TotalCostCny,total_cost_usd TotalCostUsd,0 SuggestedQuoteUsd FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken)).Single();
            if(plan.Status!=ProcurementWorkflowStatus.Sourcing)throw new BusinessRuleException("只有寻源阶段可以提交成本审核。","procurement.cost.submit-state");
            if(plan.CnyPerUsd<=0)throw new BusinessRuleException("请先保存有效汇率和成本。","procurement.cost.rate-invalid");
            var products=await GetProductCostsAsync(db,planId,plan.CnyPerUsd,true,transactionToken);
            var shared=await db.QueryAsync<ProcurementCostItem>("SELECT id Id,cost_type_id TypeId,cost_name Name,amount_cny AmountCny,sort_order SortOrder FROM procurement_plan_cost_items WHERE plan_id=@PlanId AND cost_scope='shared' ORDER BY sort_order,id",new{PlanId=planId},cancellationToken:transactionToken);
            var productTotal=products.Sum(x=>x.AmountCny);var sharedTotal=shared.Sum(x=>x.AmountCny);var total=productTotal+sharedTotal;if(total<=0)throw new BusinessRuleException("总成本必须大于零。","procurement.cost.empty");
            var usd=decimal.Round(total/plan.CnyPerUsd,2,MidpointRounding.AwayFromZero);var quote=decimal.Round(usd/(1-plan.ProfitRate),2,MidpointRounding.AwayFromZero);var revision=(await db.QueryAsync<uint>("SELECT COALESCE(MAX(revision_no),0)+1 FROM procurement_cost_snapshots WHERE plan_id=@PlanId",new{PlanId=planId},cancellationToken:transactionToken)).Single();var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("INSERT procurement_cost_snapshots(plan_id,revision_no,cny_per_usd,profit_rate,product_cost_cny,shared_cost_cny,total_cost_cny,total_cost_usd,suggested_quote_usd,status,submitted_by,submitted_at) VALUES(@PlanId,@Revision,@Rate,@Profit,@Product,@Shared,@Total,@Usd,@Quote,'submitted',@StaffId,@Now)",new{PlanId=planId,Revision=revision,Rate=plan.CnyPerUsd,Profit=plan.ProfitRate,Product=productTotal,Shared=sharedTotal,Total=total,Usd=usd,Quote=quote,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            var snapshotId=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();var sort=0;
            foreach(var product in products)await db.ExecuteAsync("INSERT procurement_cost_snapshot_items(snapshot_id,plan_item_id,inquiry_id,cost_name,source_currency,quantity,unit_amount,amount_cny,sort_order) VALUES(@SnapshotId,@PlanItemId,@InquiryId,@Name,@Currency,@Quantity,@UnitPrice,@Amount,@Sort)",new{SnapshotId=snapshotId,product.PlanItemId,product.InquiryId,Name=product.ProductName,product.Currency,product.Quantity,product.UnitPrice,Amount=product.AmountCny,Sort=++sort},cancellationToken:transactionToken);
            foreach(var item in shared)await db.ExecuteAsync("INSERT procurement_cost_snapshot_items(snapshot_id,cost_type_id,cost_name,amount_cny,sort_order) VALUES(@SnapshotId,@TypeId,@Name,@Amount,@Sort)",new{SnapshotId=snapshotId,item.TypeId,item.Name,Amount=item.AmountCny,Sort=++sort},cancellationToken:transactionToken);
            var changed=await db.ExecuteAsync("UPDATE purchase_plans SET status=3,product_cost_cny=@Product,other_cost_cny=@Shared,total_cost_cny=@Total,total_cost_usd=@Usd,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=2",new{PlanId=planId,Product=productTotal,Shared=sharedTotal,Total=total,Usd=usd,StaffId=staffId,Now=now},cancellationToken:transactionToken);if(changed!=1)throw new BusinessRuleException("计划已被其他操作处理，请刷新后重试。","procurement.cost.concurrent-change");
            await AddWorkflowEventAsync(db,planId,"cost.submitted",2,3,"cost_snapshot",snapshotId,$"成本快照 V{revision}",staffId,now,transactionToken);return new(snapshotId,revision,"PendingDepartmentReview");
        },cancellationToken:token);
    }

    public async Task<QuoteDecisionResult> ReviewQuoteAsync(DepartmentQuoteReviewCommand command,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,command.PlanId,token);
        return await db.ExecuteInTransactionAsync<QuoteDecisionResult>(async transactionToken=>
        {
            var status=(await db.QueryAsync<byte>("SELECT status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{command.PlanId},cancellationToken:transactionToken)).Single();if(status!=ProcurementWorkflowStatus.PendingDepartmentReview)throw new BusinessRuleException("当前状态不能进行部门报价审核。","procurement.quote.review-state");
            var snapshot=(await db.QueryAsync<SnapshotIdentity>("SELECT id Id,revision_no RevisionNo,submitted_by SubmittedBy,total_cost_usd TotalCostUsd,suggested_quote_usd SuggestedQuoteUsd FROM procurement_cost_snapshots WHERE plan_id=@PlanId AND status='submitted' ORDER BY revision_no DESC LIMIT 1 FOR UPDATE",new{command.PlanId},cancellationToken:transactionToken)).SingleOrDefault()??throw new BusinessRuleException("未找到待审核成本快照。","procurement.cost.snapshot-missing");
            if(snapshot.SubmittedBy==(ulong)command.StaffId)throw new BusinessRuleException("成本提交人不能审核自己的报价。","procurement.review.separation-of-duties");ProcurementPricing.ValidateFinalQuote(command.ProposedQuoteUsd,snapshot.TotalCostUsd);
            var margin=(command.ProposedQuoteUsd-snapshot.TotalCostUsd)/command.ProposedQuoteUsd;var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();await db.ExecuteAsync("INSERT procurement_quote_reviews(plan_id,cost_snapshot_id,stage,decision,proposed_quote_usd,actual_profit_rate,note,acted_by,acted_at) VALUES(@PlanId,@SnapshotId,'department','recommended',@Quote,@Margin,@Note,@StaffId,@Now)",new{command.PlanId,SnapshotId=snapshot.Id,Quote=command.ProposedQuoteUsd,Margin=margin,command.Note,command.StaffId,Now=now},cancellationToken:transactionToken);var reviewId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            var changed=await db.ExecuteAsync("UPDATE purchase_plans SET status=6,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=3",new{command.PlanId,command.StaffId,Now=now},cancellationToken:transactionToken);if(changed!=1)throw new BusinessRuleException("计划已被其他操作处理，请刷新后重试。","procurement.quote.concurrent-change");await AddWorkflowEventAsync(db,command.PlanId,"quote.department-recommended",3,6,"quote_review",(ulong)reviewId,command.Note,command.StaffId,now,transactionToken);return new(reviewId,null,null,null,"PendingFinalApproval");
        },cancellationToken:token);
    }

    public async Task<QuoteDecisionResult> RejectPhaseOneQuoteAsync(QuoteDecisionCommand command,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(command.Note))throw new BusinessRuleException("退回时必须填写原因。","procurement.quote.reject-note-required");await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,command.PlanId,token);
        return await db.ExecuteInTransactionAsync<QuoteDecisionResult>(async transactionToken=>
        {
            var status=(await db.QueryAsync<byte>("SELECT status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{command.PlanId},cancellationToken:transactionToken)).Single();if(status is not (ProcurementWorkflowStatus.PendingDepartmentReview or ProcurementWorkflowStatus.PendingFinalApproval))throw new BusinessRuleException("当前状态不能退回报价。","procurement.quote.reject-state");
            var snapshot=(await db.QueryAsync<SnapshotIdentity>("SELECT id Id,revision_no RevisionNo,submitted_by SubmittedBy,total_cost_usd TotalCostUsd,suggested_quote_usd SuggestedQuoteUsd FROM procurement_cost_snapshots WHERE plan_id=@PlanId ORDER BY revision_no DESC LIMIT 1 FOR UPDATE",new{command.PlanId},cancellationToken:transactionToken)).Single();var stage=status==ProcurementWorkflowStatus.PendingDepartmentReview?"department":"final";var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("INSERT procurement_quote_reviews(plan_id,cost_snapshot_id,stage,decision,proposed_quote_usd,note,acted_by,acted_at) VALUES(@PlanId,@SnapshotId,@Stage,'rejected',@Quote,@Note,@StaffId,@Now)",new{command.PlanId,SnapshotId=snapshot.Id,Stage=stage,Quote=command.QuoteUsd,command.Note,command.StaffId,Now=now},cancellationToken:transactionToken);var reviewId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();await db.ExecuteAsync("UPDATE procurement_cost_snapshots SET status='rejected' WHERE id=@SnapshotId",new{SnapshotId=snapshot.Id},cancellationToken:transactionToken);await db.ExecuteAsync("UPDATE purchase_plans SET status=2,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=@Status",new{command.PlanId,command.StaffId,Now=now,Status=status},cancellationToken:transactionToken);await AddWorkflowEventAsync(db,command.PlanId,"quote.rejected",status,2,"quote_review",(ulong)reviewId,command.Note,command.StaffId,now,transactionToken);return new(reviewId,null,null,null,"Rejected");
        },cancellationToken:token);
    }

    public async Task<QuoteDecisionResult> FinalApproveQuoteAsync(QuoteDecisionCommand command,string recipient,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(recipient))throw new BusinessRuleException("最终批准时必须填写收件人。","procurement.quote.recipient-required");await using var db=await databases.OpenMySqlAsync(DatabaseName,token);await scopePolicy.EnsurePlanAsync(db,command.PlanId,token);
        return await db.ExecuteInTransactionAsync<QuoteDecisionResult>(async transactionToken=>
        {
            var plan=(await db.QueryAsync<PlanForApproval>("SELECT id Id,request_id RequestId,request_version_id RequestVersionId,total_cost_usd TotalCostUsd,profit_rate ProfitRate,status Status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{command.PlanId},cancellationToken:transactionToken)).SingleOrDefault()??throw new BusinessRuleException("采购计划不存在。","procurement.plan.missing");if(plan.Status!=ProcurementWorkflowStatus.PendingFinalApproval)throw new BusinessRuleException("只有部门审核通过的报价才能最终批准。","procurement.quote.final-state");
            var snapshot=(await db.QueryAsync<SnapshotIdentity>("SELECT id Id,revision_no RevisionNo,submitted_by SubmittedBy,total_cost_usd TotalCostUsd,suggested_quote_usd SuggestedQuoteUsd FROM procurement_cost_snapshots WHERE plan_id=@PlanId AND status='submitted' ORDER BY revision_no DESC LIMIT 1 FOR UPDATE",new{command.PlanId},cancellationToken:transactionToken)).Single();var review=(await db.QueryAsync<ReviewIdentity>("SELECT acted_by ActedBy,proposed_quote_usd ProposedQuoteUsd FROM procurement_quote_reviews WHERE plan_id=@PlanId AND cost_snapshot_id=@SnapshotId AND stage='department' AND decision='recommended' ORDER BY id DESC LIMIT 1",new{command.PlanId,SnapshotId=snapshot.Id},cancellationToken:transactionToken)).SingleOrDefault()??throw new BusinessRuleException("缺少部门审核意见。","procurement.quote.department-review-missing");if(snapshot.SubmittedBy==(ulong)command.StaffId||review.ActedBy==(ulong)command.StaffId)throw new BusinessRuleException("成本提交人或部门审核人不能执行最终批准。","procurement.final.separation-of-duties");
            ProcurementPricing.ValidateFinalQuote(command.QuoteUsd,snapshot.TotalCostUsd);var margin=(command.QuoteUsd-snapshot.TotalCostUsd)/command.QuoteUsd;var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();await db.ExecuteAsync("INSERT procurement_quote_reviews(plan_id,cost_snapshot_id,stage,decision,proposed_quote_usd,actual_profit_rate,note,acted_by,acted_at) VALUES(@PlanId,@SnapshotId,'final','approved',@Quote,@Margin,@Note,@StaffId,@Now)",new{command.PlanId,SnapshotId=snapshot.Id,Quote=command.QuoteUsd,Margin=margin,command.Note,command.StaffId,Now=now},cancellationToken:transactionToken);await db.ExecuteAsync("INSERT procurement_quote_approvals(plan_id,status,quote_usd,profit_rate,submitted_by,decided_by,decision_note,submitted_at,decided_at) VALUES(@PlanId,'approved',@Quote,@Margin,@StaffId,@StaffId,@Note,@Now,@Now)",new{command.PlanId,Quote=command.QuoteUsd,Margin=margin,command.StaffId,command.Note,Now=now},cancellationToken:transactionToken);var approvalId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            await db.ExecuteAsync("UPDATE purchase_plans SET status=4,approved_quote_amount_usd=@Quote,approved_by=@StaffId,approved_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=6",new{command.PlanId,Quote=command.QuoteUsd,command.StaffId,Now=now},cancellationToken:transactionToken);await db.ExecuteAsync("UPDATE procurement_cost_snapshots SET status='approved' WHERE id=@SnapshotId",new{SnapshotId=snapshot.Id},cancellationToken:transactionToken);var invoice=await CreateApprovedInvoiceAsync(db,plan,command.QuoteUsd,command.Note,command.StaffId,now,transactionToken);await db.ExecuteAsync("INSERT procurement_mail_tasks(plan_id,approval_id,template_code,recipient,status,payload_json,created_at) VALUES(@PlanId,@ApprovalId,'wholesale.final-quote',@Recipient,'pending',@Payload,@Now)",new{command.PlanId,ApprovalId=approvalId,Recipient=recipient.Trim(),Payload=System.Text.Json.JsonSerializer.Serialize(new{command.PlanId,approvalId,command.QuoteUsd,proformaInvoiceId=invoice.Id,proformaInvoiceNumber=invoice.Number,costSnapshotId=snapshot.Id}),Now=now},cancellationToken:transactionToken);var taskId=(await db.QueryAsync<long>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();await AddWorkflowEventAsync(db,command.PlanId,"quote.final-approved",6,4,"approval",(ulong)approvalId,command.Note,command.StaffId,now,transactionToken);return new(approvalId,taskId,invoice.Id,invoice.Number,"Approved");
        },cancellationToken:token);
    }

    private static void ValidateCostInput(SaveProcurementCostsCommand command){if(command.CnyPerUsd<=0)throw new ArgumentOutOfRangeException(nameof(command.CnyPerUsd),"汇率必须大于 0。");if(command.ProfitRate is <0.10m or >0.20m)throw new ArgumentOutOfRangeException(nameof(command.ProfitRate),"利润率必须在 10% 到 20% 之间。");}
    private static ProcurementCostItemInput[] NormalizeSharedCosts(SaveProcurementCostsCommand command,IReadOnlyDictionary<uint,ProcurementManagedOption> typeMap)=>(command.Items??[]).Where(x=>x.TypeId.HasValue||!string.IsNullOrWhiteSpace(x.Name)).Select(x=>{if(x.TypeId.HasValue&&!typeMap.ContainsKey(x.TypeId.Value))throw new InvalidOperationException("成本类型不存在、已停用或不能作为公共费用。");var name=x.TypeId.HasValue?typeMap[x.TypeId.Value].Name:x.Name?.Trim();if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("自定义成本必须填写名称。");if(x.AmountCny<0)throw new InvalidOperationException("成本金额不能为负数。");return new ProcurementCostItemInput(x.TypeId,name,x.AmountCny);}).ToArray();
    private static async Task<IReadOnlyList<ProcurementProductCost>> GetProductCostsAsync(DatabaseSession db,uint planId,decimal rate,bool requireAll,CancellationToken token)
    {
        var rows=await db.QueryAsync<ProductCostRow>("SELECT i.id PlanItemId,i.product_name ProductName,i.quantity Quantity,i.quantity_unit Unit,x.inquiry_id InquiryId,s.supplier_name SupplierName,q.currency Currency,q.unit_price_cny UnitPrice,COALESCE(q.is_current,0) IsCurrent,q.status InquiryStatus FROM purchase_plan_items i LEFT JOIN procurement_selected_inquiries x ON x.plan_item_id=i.id LEFT JOIN procurement_inquiries q ON q.id=x.inquiry_id LEFT JOIN procurement_suppliers s ON s.id=q.supplier_id WHERE i.plan_id=@PlanId ORDER BY i.sort_order,i.id",new{PlanId=planId},cancellationToken:token);
        if(requireAll&&rows.Any(x=>!x.InquiryId.HasValue||!x.IsCurrent||x.InquiryStatus!="quoted"||!x.UnitPrice.HasValue||x.UnitPrice<=0))throw new BusinessRuleException("每个计划产品都必须选择一条有效的最新供应商报价后才能核算或提交。","procurement.cost.inquiry-selection-incomplete");
        return rows.Where(x=>x.InquiryId.HasValue&&x.IsCurrent&&x.UnitPrice.HasValue&&x.UnitPrice>0).Select(x=>{var cnyUnit=x.Currency=="USD"?x.UnitPrice!.Value*rate:x.UnitPrice!.Value;return new ProcurementProductCost(x.PlanItemId,x.ProductName,x.Quantity,x.Unit,x.InquiryId!.Value,x.SupplierName??"—",x.Currency??"CNY",x.UnitPrice.Value,decimal.Round(cnyUnit*x.Quantity,2,MidpointRounding.AwayFromZero));}).ToArray();
    }
    private static Task AddWorkflowEventAsync(DatabaseSession db,uint planId,string eventCode,byte? fromStatus,byte? toStatus,string? entityType,ulong? entityId,string? note,long actorId,long now,CancellationToken token)=>db.ExecuteAsync("INSERT procurement_workflow_events(plan_id,event_code,from_status,to_status,entity_type,entity_id,note,actor_id,created_at) VALUES(@PlanId,@EventCode,@FromStatus,@ToStatus,@EntityType,@EntityId,@Note,@ActorId,@Now)",new{PlanId=planId,EventCode=eventCode,FromStatus=fromStatus,ToStatus=toStatus,EntityType=entityType,EntityId=entityId,Note=Clean(note),ActorId=actorId,Now=now},cancellationToken:token);
}
