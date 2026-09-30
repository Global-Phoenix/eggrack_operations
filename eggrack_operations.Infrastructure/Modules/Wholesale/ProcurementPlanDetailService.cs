using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Common.Models;
using Eggrack.Operations.Infrastructure.Database;
using Eggrack.Operations.Infrastructure.Modules.Files;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService
{
    private sealed class PlanSupplierLink
    {
        public uint SupplierId { get; set; }
    }

    private sealed record PlanStatusRow(byte Status);
    private sealed record ProductCostTotal(decimal ProductCostCny);
    private sealed record PlanFileAccess(uint Id,string OriginalName,string StoragePath,string MimeType,uint FileSize,string VisibilityCode);
    private sealed record PlanCompletionRow(byte Status,uint RequestId);

    public async Task<PurchasePlanDetail> GetPurchasePlanDetailAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.QueryMultipleAsync("""
            SELECT p.id Id,p.plan_number PlanNumber,r.request_number PlanTitle,
              p.request_id RequestId,p.request_version_id RequestVersionId,r.request_number RequestNumber,
              v.version_number RequestVersion,COALESCE(NULLIF(v.company_name,''),v.contact_name) CustomerName,
              v.email CustomerEmail,p.status Status,COALESCE(p.priority_code,'normal') Priority,
              p.assigned_buyer_id AssignedBuyerId,b.staff_name AssignedBuyerName,p.internal_note InternalNote,
              COALESCE(p.product_cost_cny,0) ProductCostCny,COALESCE(p.packaging_cost_cny,0) PackagingCostCny,
              COALESCE(p.sample_cost_cny,0) SampleCostCny,COALESCE(p.domestic_shipping_cny,0) DomesticShippingCny,
              COALESCE(p.international_shipping_cny,0) InternationalShippingCny,COALESCE(p.other_cost_cny,0) OtherCostCny,
              COALESCE(p.total_cost_cny,0) TotalCostCny,COALESCE(p.cny_per_usd,0) CnyPerUsd,
              COALESCE(p.total_cost_usd,0) TotalCostUsd,COALESCE(p.profit_method,2) ProfitMethod,
              COALESCE(p.profit_rate,0) ProfitRate,COALESCE(p.approved_quote_amount_usd,0) ApprovedQuoteUsd,
              FROM_UNIXTIME(p.updated_at) UpdatedAtUtc,
              (SELECT COUNT(*) FROM procurement_inquiries q JOIN purchase_plan_items qi ON qi.id=q.plan_item_id WHERE qi.plan_id=p.id AND q.is_current=1) InquiryCount,
              (SELECT COUNT(*) FROM procurement_suppliers s WHERE
                EXISTS(SELECT 1 FROM procurement_inquiries q JOIN purchase_plan_items qi ON qi.id=q.plan_item_id WHERE qi.plan_id=p.id AND q.supplier_id=s.id AND q.is_current=1)
                OR EXISTS(SELECT 1 FROM procurement_candidate_products c JOIN purchase_plan_items ci ON ci.id=c.plan_item_id WHERE ci.plan_id=p.id AND c.supplier_id=s.id)
                OR EXISTS(SELECT 1 FROM procurement_samples x JOIN purchase_plan_items xi ON xi.id=x.plan_item_id WHERE xi.plan_id=p.id AND x.supplier_id=s.id)
                OR EXISTS(SELECT 1 FROM procurement_workflow_events e WHERE e.plan_id=p.id AND e.event_code='supplier.created-from-plan' AND e.entity_type='supplier' AND e.entity_id=s.id)) SupplierCount,
              (SELECT COUNT(DISTINCT q.plan_item_id) FROM procurement_inquiries q JOIN purchase_plan_items qi ON qi.id=q.plan_item_id WHERE qi.plan_id=p.id AND q.is_current=1 AND q.unit_price_cny IS NOT NULL) QuotedProductCount,
              (SELECT COUNT(*) FROM procurement_selected_inquiries x JOIN purchase_plan_items xi ON xi.id=x.plan_item_id WHERE xi.plan_id=p.id) SelectedQuoteCount,
              (SELECT COUNT(*) FROM purchase_plan_files f WHERE f.plan_id=p.id AND f.status=1) FileCount,
              (SELECT COUNT(*) FROM procurement_workflow_events e WHERE e.plan_id=p.id) EventCount,
              (SELECT pi.status FROM proforma_invoices pi WHERE pi.purchase_plan_id=p.id ORDER BY pi.id DESC LIMIT 1) LatestInvoiceStatus
            FROM purchase_plans p
            JOIN purchase_requests r ON r.id=p.request_id
            JOIN purchase_request_versions v ON v.id=p.request_version_id AND v.request_id=p.request_id
            LEFT JOIN eggrack_auth_staff b ON b.id=p.assigned_buyer_id
            WHERE p.id=@PlanId
            ;
            SELECT i.id Id,i.request_item_id RequestItemId,i.product_name ProductName,i.quantity Quantity,
              i.quantity_unit Unit,i.sku Sku,i.brand Brand,i.specifications Specifications,i.color Color,i.size Size,
              i.packaging_requirements PackagingRequirements,i.customization_requirements CustomizationRequirements,
              COALESCE(r.customer_note,i.customer_note) CustomerNote,i.buyer_id BuyerId,b.staff_name BuyerName,
              i.purchase_unit_price_cny PurchaseUnitPriceCny,i.internal_note InternalNote,
              COALESCE(qs.quote_count,0) QuoteCount,COALESCE(fs.file_count,0) FileCount,
              ss.supplier_name SelectedSupplierName,sq.offered_product_name SelectedSupplierProductName,
              sq.currency SelectedSupplierCurrency,sq.unit_price_cny SelectedSupplierUnitPrice
            FROM purchase_plan_items i
            LEFT JOIN purchase_request_version_items r ON r.id=i.request_item_id
            LEFT JOIN eggrack_auth_staff b ON b.id=i.buyer_id
            LEFT JOIN (SELECT plan_item_id,COUNT(*) quote_count FROM procurement_inquiries WHERE is_current=1 GROUP BY plan_item_id) qs ON qs.plan_item_id=i.id
            LEFT JOIN (SELECT plan_item_id,COUNT(*) file_count FROM purchase_plan_files WHERE status=1 GROUP BY plan_item_id) fs ON fs.plan_item_id=i.id
            LEFT JOIN procurement_selected_inquiries sx ON sx.plan_item_id=i.id
            LEFT JOIN procurement_inquiries sq ON sq.id=sx.inquiry_id
            LEFT JOIN procurement_suppliers ss ON ss.id=sq.supplier_id
            WHERE i.plan_id=@PlanId ORDER BY i.sort_order,i.id
            ;
            SELECT id Id,revision_no RevisionNo,status Status,product_cost_cny ProductCostCny,
              shared_cost_cny SharedCostCny,total_cost_cny TotalCostCny,total_cost_usd TotalCostUsd,
              suggested_quote_usd SuggestedQuoteUsd,submitted_by SubmittedBy,
              FROM_UNIXTIME(submitted_at) SubmittedAtUtc
            FROM procurement_cost_snapshots WHERE plan_id=@PlanId ORDER BY revision_no DESC LIMIT 1
            ;
            SELECT id Id,stage Stage,decision Decision,proposed_quote_usd ProposedQuoteUsd,
              actual_profit_rate ActualProfitRate,note Note,acted_by ActedBy,
              FROM_UNIXTIME(acted_at) ActedAtUtc
            FROM procurement_quote_reviews WHERE plan_id=@PlanId ORDER BY acted_at DESC,id DESC
            ;
            SELECT id Id,recipient Recipient,status Status,template_code TemplateCode,attempts Attempts,
              last_error LastError,FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(sent_at) SentAtUtc
            FROM procurement_mail_tasks WHERE plan_id=@PlanId ORDER BY created_at DESC,id DESC
            """,async rows=>
        {
            var plan=(await rows.ReadAsync<PurchasePlanDetail>()).SingleOrDefault()
                ??throw new BusinessRuleException("采购计划不存在。","procurement.plan.missing");
            plan.Items=(await rows.ReadAsync<PurchasePlanItemDetail>()).ToList();
            plan.LatestCostSnapshot=(await rows.ReadAsync<ProcurementCostSnapshotSummary>()).SingleOrDefault();
            plan.QuoteReviews=(await rows.ReadAsync<ProcurementQuoteReviewItem>()).ToList();
            plan.MailTasks=(await rows.ReadAsync<MailTaskItem>()).ToList();
            return plan;
        },new{PlanId=planId},cancellationToken:token);
    }

    public async Task<ProformaInvoiceDetail?> GetLatestPlanInvoiceAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.QueryMultipleAsync("""
            SELECT id Id,pi_number Number,supersedes_pi_id SupersedesPiId,status Status,company_name CompanyName,
              contact_name ContactName,email Email,seller_name SellerName,currency Currency,
              product_amount ProductAmount,packaging_fee PackagingFee,shipping_fee ShippingFee,
              other_fee OtherFee,discount_amount DiscountAmount,total_amount TotalAmount,
              payment_terms PaymentTerms,payment_instructions PaymentInstructions,trade_terms TradeTerms,
              delivery_terms DeliveryTerms,lead_time LeadTime,FROM_UNIXTIME(valid_until) ValidUntil,
              FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(issued_at) IssuedAtUtc
            FROM proforma_invoices WHERE purchase_plan_id=@PlanId ORDER BY id DESC LIMIT 1
            ;
            SELECT x.id Id,x.product_name ProductName,x.quantity Quantity,x.quantity_unit Unit,
              x.unit_price UnitPrice,x.line_amount LineAmount
            FROM proforma_invoice_items x
            JOIN (SELECT id FROM proforma_invoices WHERE purchase_plan_id=@PlanId ORDER BY id DESC LIMIT 1) latest
              ON latest.id=x.pi_id
            ORDER BY x.sort_order,x.id
            """,async rows=>
        {
            var invoice=(await rows.ReadAsync<ProformaInvoiceDetail>()).SingleOrDefault();
            var items=(await rows.ReadAsync<ProformaInvoiceItemDetail>()).ToList();
            if(invoice is not null)invoice.Items=items;
            return invoice;
        },new{PlanId=planId},cancellationToken:token);
    }

    public async Task<PurchasePlanSourcingData> GetPurchasePlanSourcingDataAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.QueryMultipleAsync("""
            SELECT id Id,supplier_name Name,supplier_code Code,address Address,legal_representative LegalRepresentative,
              contact_name ContactName,contact_phone ContactPhone,website Website,contact_json ContactJson,status Status,
              FROM_UNIXTIME(updated_at) UpdatedAtUtc
            FROM procurement_suppliers ORDER BY supplier_name;
            SELECT c.id Id,c.plan_item_id PlanItemId,c.supplier_id SupplierId,c.product_name ProductName,
              s.supplier_name SupplierName,c.reference_url ReferenceUrl,c.specification_json SpecificationJson,c.status Status
            FROM procurement_candidate_products c
            JOIN purchase_plan_items i ON i.id=c.plan_item_id
            LEFT JOIN procurement_suppliers s ON s.id=c.supplier_id
            WHERE i.plan_id=@PlanId ORDER BY i.sort_order,c.updated_at DESC,c.id DESC;
            SELECT q.id Id,q.plan_item_id PlanItemId,q.supplier_id SupplierId,i.product_name ProductName,
              s.supplier_name SupplierName,q.offered_product_name OfferedProductName,q.length_cm LengthCm,
              q.width_cm WidthCm,q.height_cm HeightCm,q.weight_kg WeightKg,q.color Color,q.size_details SizeDetails,
              q.parameter_details ParameterDetails,q.currency Currency,q.unit_price_cny UnitPrice,q.moq Moq,
              q.lead_days LeadDays,q.valid_until ValidUntil,q.terms Terms,q.status Status,q.notes Notes,
              q.revision_no RevisionNo,EXISTS(SELECT 1 FROM procurement_selected_inquiries x WHERE x.inquiry_id=q.id) IsSelected
            FROM procurement_inquiries q
            JOIN purchase_plan_items i ON i.id=q.plan_item_id
            JOIN procurement_suppliers s ON s.id=q.supplier_id
            WHERE i.plan_id=@PlanId AND q.is_current=1 ORDER BY i.sort_order,q.updated_at DESC,q.id DESC;
            SELECT x.id Id,x.plan_item_id PlanItemId,x.supplier_id SupplierId,i.product_name ProductName,
              s.supplier_name SupplierName,x.quantity Quantity,x.status Status,x.cost_cny CostCny,
              x.tracking_number TrackingNumber,x.notes Notes
            FROM procurement_samples x
            JOIN purchase_plan_items i ON i.id=x.plan_item_id
            LEFT JOIN procurement_suppliers s ON s.id=x.supplier_id
            WHERE i.plan_id=@PlanId ORDER BY x.updated_at DESC,x.id DESC;
            SELECT DISTINCT s.id SupplierId
            FROM procurement_workflow_events e
            JOIN procurement_suppliers s ON s.id=e.entity_id AND s.status='active'
            WHERE e.plan_id=@PlanId AND e.event_code='supplier.created-from-plan' AND e.entity_type='supplier'
            """,async rows=>new PurchasePlanSourcingData(
                (await rows.ReadAsync<SupplierListItem>()).ToList(),
                (await rows.ReadAsync<CandidateProductItem>()).ToList(),
                (await rows.ReadAsync<InquiryItem>()).ToList(),
                (await rows.ReadAsync<SampleItem>()).ToList(),
                (await rows.ReadAsync<PlanSupplierLink>()).Select(x=>x.SupplierId).ToList()),
            new{PlanId=planId},cancellationToken:token);
    }

    public async Task<PurchasePlanFilesData> GetPurchasePlanFilesDataAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.QueryMultipleAsync("""
            SELECT f.id Id,f.plan_item_id PlanItemId,i.product_name ProductName,f.file_type FileType,
              f.title Title,f.description Description,f.original_name OriginalName,f.mime_type MimeType,
              f.file_size FileSize,f.is_customer_visible=1 IsCustomerVisible,FROM_UNIXTIME(f.uploaded_at) UploadedAtUtc
            FROM purchase_plan_files f LEFT JOIN purchase_plan_items i ON i.id=f.plan_item_id
            WHERE f.plan_id=@PlanId AND f.status=1 ORDER BY f.sort_order,f.id;
            SELECT DISTINCT s.id Id,s.supplier_name Name,s.supplier_code Code,s.address Address,
              s.legal_representative LegalRepresentative,s.contact_name ContactName,s.contact_phone ContactPhone,
              s.website Website,s.contact_json ContactJson,s.status Status,FROM_UNIXTIME(s.updated_at) UpdatedAtUtc
            FROM procurement_suppliers s
            LEFT JOIN procurement_inquiries q ON q.supplier_id=s.id AND q.is_current=1
            LEFT JOIN procurement_samples x ON x.supplier_id=s.id
            LEFT JOIN purchase_plan_items qi ON qi.id=q.plan_item_id
            LEFT JOIN purchase_plan_items xi ON xi.id=x.plan_item_id
            WHERE qi.plan_id=@PlanId OR xi.plan_id=@PlanId ORDER BY s.supplier_name;
            SELECT q.id Id,q.plan_item_id PlanItemId,q.supplier_id SupplierId,i.product_name ProductName,
              s.supplier_name SupplierName,q.offered_product_name OfferedProductName,q.length_cm LengthCm,
              q.width_cm WidthCm,q.height_cm HeightCm,q.weight_kg WeightKg,q.color Color,q.size_details SizeDetails,
              q.parameter_details ParameterDetails,q.currency Currency,q.unit_price_cny UnitPrice,q.moq Moq,
              q.lead_days LeadDays,q.valid_until ValidUntil,q.terms Terms,q.status Status,q.notes Notes,
              q.revision_no RevisionNo,EXISTS(SELECT 1 FROM procurement_selected_inquiries z WHERE z.inquiry_id=q.id) IsSelected
            FROM procurement_inquiries q JOIN purchase_plan_items i ON i.id=q.plan_item_id
            JOIN procurement_suppliers s ON s.id=q.supplier_id
            WHERE i.plan_id=@PlanId AND q.is_current=1 ORDER BY i.sort_order,q.updated_at DESC,q.id DESC;
            SELECT x.id Id,x.plan_item_id PlanItemId,x.supplier_id SupplierId,i.product_name ProductName,
              s.supplier_name SupplierName,x.quantity Quantity,x.status Status,x.cost_cny CostCny,
              x.tracking_number TrackingNumber,x.notes Notes
            FROM procurement_samples x JOIN purchase_plan_items i ON i.id=x.plan_item_id
            LEFT JOIN procurement_suppliers s ON s.id=x.supplier_id
            WHERE i.plan_id=@PlanId ORDER BY x.updated_at DESC,x.id DESC;
            """,async rows=>new PurchasePlanFilesData(
                (await rows.ReadAsync<PurchasePlanFileDetail>()).ToList(),
                (await rows.ReadAsync<SupplierListItem>()).ToList(),
                (await rows.ReadAsync<InquiryItem>()).ToList(),
                (await rows.ReadAsync<SampleItem>()).ToList()),
            new{PlanId=planId},cancellationToken:token);
    }

    public async Task<IReadOnlyList<ProcurementWorkflowEventItem>> GetPurchasePlanEventsAsync(uint planId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.QueryAsync<ProcurementWorkflowEventItem>("""
            SELECT id Id,event_code EventCode,from_status FromStatus,to_status ToStatus,note Note,
              actor_id ActorId,FROM_UNIXTIME(created_at) CreatedAtUtc
            FROM procurement_workflow_events WHERE plan_id=@PlanId ORDER BY created_at DESC,id DESC LIMIT 100
            """,new{PlanId=planId},cancellationToken:token);
    }

    public async Task<ProformaInvoiceDetail?> GetProformaInvoiceDetailAsync(uint invoiceId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        var invoice=(await db.QueryAsync<ProformaInvoiceDetail>("""
            SELECT i.id Id,i.pi_number Number,i.purchase_plan_id PurchasePlanId,p.plan_number PlanNumber,
              i.request_id RequestId,r.request_number RequestNumber,i.supersedes_pi_id SupersedesPiId,
              i.status Status,i.company_name CompanyName,i.contact_name ContactName,i.email Email,
              i.phone Phone,i.country Country,i.billing_address BillingAddress,i.delivery_address DeliveryAddress,
              i.seller_name SellerName,i.seller_address SellerAddress,i.seller_email SellerEmail,
              i.seller_phone SellerPhone,i.currency Currency,i.product_amount ProductAmount,
              i.packaging_fee PackagingFee,i.shipping_fee ShippingFee,i.other_fee OtherFee,
              i.discount_amount DiscountAmount,i.total_amount TotalAmount,i.payment_terms PaymentTerms,
              i.payment_instructions PaymentInstructions,i.trade_terms TradeTerms,i.delivery_terms DeliveryTerms,
              i.lead_time LeadTime,FROM_UNIXTIME(i.valid_until) ValidUntil,
              FROM_UNIXTIME(i.created_at) CreatedAtUtc,FROM_UNIXTIME(i.issued_at) IssuedAtUtc
            FROM proforma_invoices i
            JOIN purchase_plans p ON p.id=i.purchase_plan_id
            JOIN purchase_requests r ON r.id=i.request_id
            WHERE i.id=@InvoiceId
            """,new{InvoiceId=invoiceId},cancellationToken:token)).SingleOrDefault();
        if(invoice is null)return null;
        await scopePolicy.EnsurePlanAsync(db,invoice.PurchasePlanId,token);
        invoice.Items=await db.QueryAsync<ProformaInvoiceItemDetail>(
            "SELECT id Id,product_name ProductName,quantity Quantity,quantity_unit Unit,unit_price UnitPrice,line_amount LineAmount FROM proforma_invoice_items WHERE pi_id=@InvoiceId ORDER BY sort_order,id",
            new{InvoiceId=invoice.Id},cancellationToken:token);
        return invoice;
    }

    public async Task<uint> CreateReplacementProformaInvoiceAsync(uint planId,uint issuedInvoiceId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.ExecuteInTransactionAsync<uint>(async transactionToken=>
        {
            var status=(await db.QueryAsync<byte>(
                "SELECT status FROM proforma_invoices WHERE id=@InvoiceId AND purchase_plan_id=@PlanId FOR UPDATE",
                new{InvoiceId=issuedInvoiceId,PlanId=planId},cancellationToken:transactionToken)).SingleOrDefault();
            if(status!=3)throw new BusinessRuleException("只有已签发 PI 可以创建修订版本。","procurement.pi.revision-source");
            var active=await db.QueryAsync<uint>(
                "SELECT id FROM proforma_invoices WHERE purchase_plan_id=@PlanId AND status IN(1,2) FOR UPDATE",
                new{PlanId=planId},cancellationToken:transactionToken);
            if(active.Count>0)throw new BusinessRuleException("当前计划已有待处理 PI，请先完成或取消后再创建修订版本。","procurement.pi.revision-active");
            var number="PI-"+DateTime.UtcNow.ToString("yyMMdd-HHmmssfff");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("""
                INSERT proforma_invoices(pi_number,request_id,request_version_id,purchase_plan_id,supersedes_pi_id,
                  status,customer_user_id,company_name,contact_name,email,phone,whatsapp,country,billing_address,
                  delivery_address,seller_name,seller_address,seller_email,seller_phone,currency,product_amount,
                  packaging_fee,shipping_fee,other_fee,discount_amount,subtotal,total_amount,payment_terms,
                  payment_instructions,trade_terms,delivery_terms,lead_time,valid_until,customer_note,internal_note,
                  created_by,updated_by,created_at,updated_at)
                SELECT @Number,request_id,request_version_id,purchase_plan_id,id,1,customer_user_id,company_name,
                  contact_name,email,phone,whatsapp,country,billing_address,delivery_address,seller_name,seller_address,
                  seller_email,seller_phone,currency,product_amount,packaging_fee,shipping_fee,other_fee,discount_amount,
                  subtotal,total_amount,payment_terms,payment_instructions,trade_terms,delivery_terms,lead_time,
                  valid_until,customer_note,internal_note,@StaffId,@StaffId,@Now,@Now
                FROM proforma_invoices WHERE id=@InvoiceId AND purchase_plan_id=@PlanId AND status=3
                """,new{Number=number,InvoiceId=issuedInvoiceId,PlanId=planId,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            var replacementId=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:transactionToken)).Single();
            await db.ExecuteAsync("""
                INSERT proforma_invoice_items(pi_id,purchase_plan_item_id,request_item_id,product_key,sort_order,
                  product_name,sku,brand,description,quantity,quantity_unit,specifications,color,size,
                  packaging_requirements,customization_requirements,customer_note,unit_price,line_amount,
                  created_by,updated_by,created_at,updated_at)
                SELECT @ReplacementId,purchase_plan_item_id,request_item_id,product_key,sort_order,product_name,sku,
                  brand,description,quantity,quantity_unit,specifications,color,size,packaging_requirements,
                  customization_requirements,customer_note,unit_price,line_amount,@StaffId,@StaffId,@Now,@Now
                FROM proforma_invoice_items WHERE pi_id=@InvoiceId ORDER BY sort_order,id
                """,new{ReplacementId=replacementId,InvoiceId=issuedInvoiceId,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            await AddWorkflowEventAsync(db,planId,"pi.revision-created",4,4,"proforma_invoice",replacementId,
                $"{number} supersedes PI #{issuedInvoiceId}",staffId,now,transactionToken);
            return replacementId;
        },cancellationToken:token);
    }

    public async Task CancelProformaInvoiceAsync(uint planId,uint invoiceId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        await db.ExecuteInTransactionAsync(async transactionToken=>
        {
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var changed=await db.ExecuteAsync("""
                UPDATE proforma_invoices SET status=4,cancelled_by=@StaffId,cancelled_at=@Now,
                  updated_by=@StaffId,updated_at=@Now
                WHERE id=@InvoiceId AND purchase_plan_id=@PlanId AND status IN(1,2)
                """,new{InvoiceId=invoiceId,PlanId=planId,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            if(changed!=1)throw new BusinessRuleException("只有草稿或待签发 PI 可以取消；已签发 PI 必须创建修订版本。","procurement.pi.cancel-state");
            await AddWorkflowEventAsync(db,planId,"pi.cancelled",4,4,"proforma_invoice",invoiceId,null,staffId,now,transactionToken);
        },cancellationToken:token);
    }

    public async Task CompletePurchasePlanAsync(uint planId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        await db.ExecuteInTransactionAsync(async transactionToken=>
        {
            var plans=await db.QueryAsync<PlanCompletionRow>(
                "SELECT status Status,request_id RequestId FROM purchase_plans WHERE id=@PlanId FOR UPDATE",
                new{PlanId=planId},cancellationToken:transactionToken);
            var plan=plans.SingleOrDefault();
            if(plan is null||plan.RequestId==0||plan.Status is not(4 or 8))
                throw new BusinessRuleException("只有 PI 已签发且邮件待处理的采购计划可以完成。","procurement.plan.complete-state");
            var issued=(await db.QueryAsync<long>(
                "SELECT COUNT(*) FROM proforma_invoices WHERE purchase_plan_id=@PlanId AND status=3",
                new{PlanId=planId},cancellationToken:transactionToken)).Single();
            if(issued==0)throw new BusinessRuleException("请先签发 PI，再完成采购计划。","procurement.plan.pi-not-issued");
            var sent=(await db.QueryAsync<long>("""
                SELECT COUNT(*) FROM procurement_mail_tasks m
                JOIN (SELECT id FROM proforma_invoices WHERE purchase_plan_id=@PlanId AND status=3 ORDER BY id DESC LIMIT 1) i
                  ON i.id=CAST(JSON_UNQUOTE(JSON_EXTRACT(m.payload_json,'$.proformaInvoiceId')) AS UNSIGNED)
                WHERE m.plan_id=@PlanId AND m.status='sent'
                """,
                new{PlanId=planId},cancellationToken:transactionToken)).Single();
            if(sent==0)throw new BusinessRuleException("请先将客户报价邮件发送成功，再完成采购计划。","procurement.plan.mail-not-sent");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var changed=await db.ExecuteAsync(
                "UPDATE purchase_plans SET status=5,completed_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status IN(4,8)",
                new{PlanId=planId,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            if(changed!=1)throw new BusinessRuleException("采购计划已被其他操作处理，请刷新。","procurement.plan.concurrent-change");
            await db.ExecuteAsync(
                "UPDATE purchase_requests SET status=4,updated_at=@Now WHERE id=@RequestId AND status<4",
                new{plan.RequestId,Now=now},cancellationToken:transactionToken);
            await AddWorkflowEventAsync(db,planId,"plan.completed",4,5,null,null,"采购计划已完成",staffId,now,transactionToken);
        },cancellationToken:token);
    }

    public async Task SavePurchasePlanItemProcurementAsync(
        uint planId,SavePurchasePlanItemProcurementCommand command,ulong staffId,CancellationToken token=default)
    {
        if(command.BuyerId==0)throw new BusinessRuleException("请选择采购人员。","procurement.item.buyer-required");
        if(command.PurchaseUnitPriceCny<0)throw new BusinessRuleException("产品采购单价不能为负数。","procurement.item.price-invalid");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        await scopePolicy.ResolveBuyerDepartmentAsync(db,command.BuyerId,token);
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var changed=await db.ExecuteAsync("""
            UPDATE purchase_plan_items i JOIN purchase_plans p ON p.id=i.plan_id
            SET i.buyer_id=@BuyerId,i.assigned_by=@StaffId,i.assigned_at=@Now,
              i.purchase_unit_price_cny=@Price,i.internal_note=@InternalNote,
              i.updated_by=@StaffId,i.updated_at=@Now
            WHERE i.id=@ItemId AND i.plan_id=@PlanId AND p.status IN(1,2,7)
            """,new{PlanId=planId,command.ItemId,command.BuyerId,Price=command.PurchaseUnitPriceCny,
                InternalNote=Clean(command.InternalNote),StaffId=staffId,Now=now},cancellationToken:token);
        if(changed!=1)throw new BusinessRuleException("计划产品不存在或当前状态不能修改。","procurement.item.concurrent-change");
    }

    public async Task<PurchasePlanQuoteCalculation> SavePurchasePlanCostQuoteAsync(
        uint planId,SavePurchasePlanCostQuoteCommand command,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.ExecuteInTransactionAsync<PurchasePlanQuoteCalculation>(async transactionToken=>
        {
            var status=(await db.QueryAsync<PlanStatusRow>(
                "SELECT status Status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",
                new{PlanId=planId},cancellationToken:transactionToken)).SingleOrDefault()
                ??throw new BusinessRuleException("采购计划不存在。","procurement.plan.missing");
            if(status.Status!=ProcurementWorkflowStatus.Sourcing)
                throw new BusinessRuleException("采购计划当前状态不能修改成本。","procurement.cost.state");
            var missing=(await db.QueryAsync<long>(
                "SELECT COUNT(*) FROM purchase_plan_items WHERE plan_id=@PlanId AND purchase_unit_price_cny IS NULL",
                new{PlanId=planId},cancellationToken:transactionToken)).Single();
            if(missing>0)throw new BusinessRuleException("请先完成所有产品的采购单价。","procurement.cost.product-price-missing");
            var product=(await db.QueryAsync<ProductCostTotal>(
                "SELECT COALESCE(SUM(quantity*purchase_unit_price_cny),0) ProductCostCny FROM purchase_plan_items WHERE plan_id=@PlanId",
                new{PlanId=planId},cancellationToken:transactionToken)).Single().ProductCostCny;
            var calculated=PurchasePlanQuoteCalculator.Calculate(product,command);
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.ExecuteAsync("""
                UPDATE purchase_plans SET status=2,product_cost_cny=@ProductCostCny,
                  packaging_cost_cny=@PackagingCostCny,sample_cost_cny=@SampleCostCny,
                  domestic_shipping_cny=@DomesticShippingCny,international_shipping_cny=@InternationalShippingCny,
                  other_cost_cny=@OtherCostCny,total_cost_cny=@TotalCostCny,cny_per_usd=@CnyPerUsd,
                  total_cost_usd=@TotalCostUsd,profit_method=@ProfitMethod,profit_rate=@ProfitRate,
                  approved_quote_amount_usd=@QuoteUsd,cost_updated_by=@StaffId,cost_updated_at=@Now,
                  updated_by=@StaffId,updated_at=@Now
                WHERE id=@PlanId
                """,new{PlanId=planId,calculated.ProductCostCny,command.PackagingCostCny,command.SampleCostCny,
                    command.DomesticShippingCny,command.InternationalShippingCny,command.OtherCostCny,
                    calculated.TotalCostCny,command.CnyPerUsd,calculated.TotalCostUsd,command.ProfitMethod,
                    command.ProfitRate,calculated.QuoteUsd,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            await AddWorkflowEventAsync(db,planId,"cost.quote-saved",status.Status,2,null,null,
                $"建议报价 USD {calculated.QuoteUsd:N2}",staffId,now,transactionToken);
            return calculated;
        },cancellationToken:token);
    }

    public async Task SubmitPurchasePlanQuoteAsync(uint planId,long staffId,CancellationToken token=default)
    {
        await SubmitCostReviewAsync(planId,staffId,token);
    }

    public async Task ApprovePurchasePlanQuoteAsync(uint planId,decimal quoteUsd,string? note,long staffId,CancellationToken token=default)
    {
        var plan=await GetPurchasePlanDetailAsync(planId,token);
        await FinalApproveQuoteAsync(new(planId,quoteUsd,note,staffId),plan.CustomerEmail,token);
    }

    public async Task<ProcurementLifecycleResult> GenerateProformaInvoiceAsync(uint planId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.ExecuteInTransactionAsync<ProcurementLifecycleResult>(async transactionToken=>
        {
            var plans=await db.QueryAsync<PlanForApproval>(
                "SELECT id Id,request_id RequestId,request_version_id RequestVersionId,total_cost_usd TotalCostUsd,profit_rate ProfitRate,status Status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",
                new{PlanId=planId},cancellationToken:transactionToken);
            var plan=plans.SingleOrDefault()??throw new BusinessRuleException("采购计划不存在。","procurement.plan.missing");
            if(plan.Status!=4)throw new BusinessRuleException("只有最终报价审核通过的采购计划可以生成 PI。","procurement.pi.plan-state");
            var quote=(await db.QueryAsync<decimal>(
                "SELECT approved_quote_amount_usd FROM purchase_plans WHERE id=@PlanId",
                new{PlanId=planId},cancellationToken:transactionToken)).Single();
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var invoice=await CreateApprovedInvoiceAsync(db,plan,quote,null,staffId,now,transactionToken);
            await AddWorkflowEventAsync(db,planId,"pi.generated",4,4,"proforma_invoice",invoice.Id,invoice.Number,staffId,now,transactionToken);
            return new(planId,"Approved",invoice.Id,invoice.Number,"Draft");
        },cancellationToken:token);
    }

    public async Task SaveProformaInvoiceAsync(uint planId,SaveProformaInvoiceCommand command,long staffId,CancellationToken token=default)
    {
        if(command.Items.Count==0)throw new BusinessRuleException("PI 至少需要一个产品。","procurement.pi.items-empty");
        if(command.Items.Any(item=>item.UnitPrice<0)||command.PackagingFee<0||command.ShippingFee<0||command.OtherFee<0||command.DiscountAmount<0)
            throw new BusinessRuleException("PI 价格、费用和折扣不能为负数。","procurement.pi.pricing-invalid");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        await db.ExecuteInTransactionAsync(async transactionToken=>
        {
            var headers=await db.QueryAsync<InvoicePricingHeader>(
                "SELECT i.id Id,i.status Status,p.approved_quote_amount_usd ApprovedQuoteUsd,i.packaging_fee PackagingFee,i.shipping_fee ShippingFee,i.other_fee OtherFee,i.discount_amount DiscountAmount FROM proforma_invoices i JOIN purchase_plans p ON p.id=i.purchase_plan_id WHERE i.id=@InvoiceId AND i.purchase_plan_id=@PlanId FOR UPDATE",
                new{command.InvoiceId,PlanId=planId},cancellationToken:transactionToken);
            var header=headers.SingleOrDefault()??throw new BusinessRuleException("PI 不存在。","procurement.pi.missing");
            if(header.Status is not(1 or 2))throw new BusinessRuleException("已签发或取消的 PI 不能修改。","procurement.pi.immutable");
            var existing=await db.QueryAsync<ProformaInvoicePriceItem>(
                "SELECT id Id,product_name ProductName,quantity Quantity,quantity_unit Unit,unit_price UnitPrice,line_amount LineAmount FROM proforma_invoice_items WHERE pi_id=@InvoiceId ORDER BY sort_order,id FOR UPDATE",
                new{command.InvoiceId},cancellationToken:transactionToken);
            if(existing.Count!=command.Items.Count||existing.Select(item=>item.Id).Except(command.Items.Select(item=>item.Id)).Any())
                throw new BusinessRuleException("PI 产品明细不一致，请刷新。","procurement.pi.items-mismatch");
            var prices=command.Items.ToDictionary(item=>item.Id,item=>item.UnitPrice);
            var lineAmounts=new List<decimal>();
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach(var item in existing)
            {
                var price=Math.Round(prices[item.Id],4,MidpointRounding.AwayFromZero);
                var amount=ProformaInvoicePricingCalculator.CalculateLineAmount(item.Quantity,price);
                lineAmounts.Add(amount);
                await db.ExecuteAsync(
                    "UPDATE proforma_invoice_items SET unit_price=@Price,line_amount=@Amount,updated_by=@StaffId,updated_at=@Now WHERE id=@Id AND pi_id=@InvoiceId",
                    new{item.Id,command.InvoiceId,Price=price,Amount=amount,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            }
            var totals=ProformaInvoicePricingCalculator.CalculateTotals(
                lineAmounts,command.PackagingFee,command.ShippingFee,command.OtherFee,command.DiscountAmount);
            if(command.DiscountAmount>lineAmounts.Sum()+command.PackagingFee+command.ShippingFee+command.OtherFee)
                throw new BusinessRuleException("折扣不能大于 PI 小计。","procurement.pi.discount-invalid");
            if(Math.Abs(totals.TotalAmount-header.ApprovedQuoteUsd)>.01m)
                throw new BusinessRuleException($"PI 总额必须等于最终审核确认报价 USD {header.ApprovedQuoteUsd:N2}。","procurement.pi.total-mismatch");
            long? validUntil=command.ValidUntil.HasValue
                ?new DateTimeOffset(DateTime.SpecifyKind(command.ValidUntil.Value.Date,DateTimeKind.Utc)).ToUnixTimeSeconds():null;
            await db.ExecuteAsync("""
                UPDATE proforma_invoices SET status=2,product_amount=@ProductAmount,
                  packaging_fee=@PackagingFee,shipping_fee=@ShippingFee,other_fee=@OtherFee,
                  discount_amount=@DiscountAmount,subtotal=@Subtotal,total_amount=@Total,
                  payment_terms=@PaymentTerms,payment_instructions=@PaymentInstructions,
                  trade_terms=@TradeTerms,delivery_terms=@DeliveryTerms,lead_time=@LeadTime,
                  valid_until=@ValidUntil,updated_by=@StaffId,updated_at=@Now
                WHERE id=@InvoiceId AND purchase_plan_id=@PlanId AND status IN(1,2)
                """,new{command.InvoiceId,PlanId=planId,totals.ProductAmount,command.PackagingFee,
                    command.ShippingFee,command.OtherFee,command.DiscountAmount,totals.Subtotal,
                    Total=totals.TotalAmount,PaymentTerms=Clean(command.PaymentTerms),
                    PaymentInstructions=Clean(command.PaymentInstructions),TradeTerms=Clean(command.TradeTerms),
                    DeliveryTerms=Clean(command.DeliveryTerms),LeadTime=Clean(command.LeadTime),
                    ValidUntil=validUntil,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            await AddWorkflowEventAsync(db,planId,"pi.approved",4,4,"proforma_invoice",command.InvoiceId,null,staffId,now,transactionToken);
        },cancellationToken:token);
    }

    public async Task<uint> AddPurchasePlanFileAsync(
        uint planId,uint? planItemId,string fileType,string? title,string? description,
        string originalName,string storagePath,string checksum,string mimeType,uint fileSize,ulong staffId,
        CancellationToken token=default)
    {
        var allowed=new[]{"image","pdf","video","specification","design","document","other"};
        fileType=(fileType??string.Empty).Trim().ToLowerInvariant();
        if(!allowed.Contains(fileType))throw new BusinessRuleException("文件类型无效。","procurement.file.type-invalid");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        if(planItemId.HasValue&&(await db.QueryAsync<long>(
            "SELECT COUNT(*) FROM purchase_plan_items WHERE id=@ItemId AND plan_id=@PlanId",
            new{ItemId=planItemId.Value,PlanId=planId},cancellationToken:token)).Single()!=1)
            throw new BusinessRuleException("关联的计划产品不存在。","procurement.file.item-invalid");
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.ExecuteAsync("""
            INSERT purchase_plan_files(plan_id,plan_item_id,file_type,is_customer_visible,title,description,
              original_name,storage_path,mime_type,file_size,status,uploaded_by,customer_visible_by,
              updated_by,uploaded_at,customer_visible_at,updated_at,visibility_code,checksum_sha256)
            VALUES(@PlanId,@PlanItemId,@FileType,0,@Title,@Description,@OriginalName,@StoragePath,@MimeType,
              @FileSize,1,@StaffId,NULL,@StaffId,@Now,NULL,@Now,'internal',@Checksum)
            """,new{PlanId=planId,PlanItemId=planItemId,FileType=fileType,Title=Clean(title),
                Description=Clean(description),OriginalName=originalName,StoragePath=storagePath,MimeType=mimeType,
                FileSize=fileSize,StaffId=staffId,Now=now,Checksum=checksum},cancellationToken:token);
        return(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
    }

    public async Task SetPurchasePlanFileVisibilityAsync(
        uint planId,uint fileId,bool customerVisible,ulong staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var changed=await db.ExecuteAsync("""
            UPDATE purchase_plan_files SET is_customer_visible=@Visible,
              customer_visible_by=CASE WHEN @Visible=1 THEN @StaffId ELSE NULL END,
              customer_visible_at=CASE WHEN @Visible=1 THEN @Now ELSE NULL END,
              visibility_code=CASE WHEN @Visible=1 THEN 'customer' ELSE 'internal' END,
              updated_by=@StaffId,updated_at=@Now
            WHERE id=@FileId AND plan_id=@PlanId AND status=1
            """,new{PlanId=planId,FileId=fileId,Visible=customerVisible?1:0,StaffId=staffId,Now=now},cancellationToken:token);
        if(changed!=1)throw new BusinessRuleException("文件不存在或已失效。","procurement.file.missing");
    }

    public async Task<FileCenterStoredFile?> GetPurchasePlanFileAsync(uint planId,uint fileId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        var file=(await db.QueryAsync<PlanFileAccess>(
            "SELECT id Id,original_name OriginalName,storage_path StoragePath,mime_type MimeType,file_size FileSize,visibility_code VisibilityCode FROM purchase_plan_files WHERE id=@FileId AND plan_id=@PlanId AND status=1",
            new{FileId=fileId,PlanId=planId},cancellationToken:token)).SingleOrDefault();
        return file is null?null:new FileCenterStoredFile("plan",file.Id,file.OriginalName,file.StoragePath,file.MimeType,file.FileSize,file.VisibilityCode);
    }
}
