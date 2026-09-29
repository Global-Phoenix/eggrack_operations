using Eggrack.Operations.Application.Modules.Wholesale;
using Eggrack.Operations.Common.Models;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService
{
    private sealed record InvoiceIdentity(uint Id,string Number);
    private sealed record CustomerSnapshot(int? UserId,string? CompanyName,string ContactName,string Email,string? Phone,string? Whatsapp,string? Country,string? DeliveryAddress,string? TradeTerms,string? CustomerNote);
    private sealed record InvoiceItemSource(uint Id,uint? RequestItemId,string ProductKey,uint SortOrder,string ProductName,string? Sku,string? Brand,string? Description,decimal Quantity,string QuantityUnit,string? Specifications,string? Color,string? Size,string? PackagingRequirements,string? CustomizationRequirements,string? CustomerNote);

    private async Task<InvoiceIdentity> CreateApprovedInvoiceAsync(DatabaseSession db,PlanForApproval plan,decimal totalAmount,string? internalNote,long staffId,long now,CancellationToken token)
    {
        var existing=await db.QueryAsync<uint>("SELECT id FROM proforma_invoices WHERE purchase_plan_id=@PlanId AND status IN (1,2,3) FOR UPDATE",new{PlanId=plan.Id},cancellationToken:token);
        if(existing.Count>0) throw new BusinessRuleException("该采购计划已经存在有效 PI，不能重复生成。","procurement.pi.duplicate");
        var customers=await db.QueryAsync<CustomerSnapshot>("""
        SELECT r.user_id UserId,v.company_name CompanyName,v.contact_name ContactName,v.email Email,
          v.phone Phone,v.whatsapp Whatsapp,v.country Country,v.delivery_address DeliveryAddress,
          v.pickup_trade_info TradeTerms,v.customer_message CustomerNote
        FROM purchase_requests r JOIN purchase_request_versions v ON v.request_id=r.id AND v.id=@VersionId
        WHERE r.id=@RequestId
        """,new{RequestId=plan.RequestId,VersionId=plan.RequestVersionId},cancellationToken:token);
        var customer=customers.SingleOrDefault()??throw new BusinessRuleException("采购申请客户快照不存在。","procurement.customer-snapshot.missing");
        var items=await db.QueryAsync<InvoiceItemSource>("""
        SELECT p.id Id,p.request_item_id RequestItemId,p.product_key ProductKey,p.sort_order SortOrder,
          p.product_name ProductName,r.sku Sku,r.brand Brand,r.description Description,p.quantity Quantity,
          p.quantity_unit QuantityUnit,r.specifications Specifications,r.color Color,r.size Size,
          r.packaging_requirements PackagingRequirements,r.customization_requirements CustomizationRequirements,r.customer_note CustomerNote
        FROM purchase_plan_items p LEFT JOIN purchase_request_version_items r ON r.id=p.request_item_id
        WHERE p.plan_id=@PlanId ORDER BY p.sort_order,p.id
        """,new{PlanId=plan.Id},cancellationToken:token);
        if(items.Count==0) throw new BusinessRuleException("采购计划没有可生成 PI 的产品。","procurement.pi.items-empty");
        if(items.Any(item=>item.Quantity<=0)) throw new BusinessRuleException("采购计划存在无效产品数量，不能生成 PI。","procurement.pi.quantity-invalid");
        var number="PI-"+DateTime.UtcNow.ToString("yyMMdd-HHmmssfff");
        await db.ExecuteAsync("""
        INSERT proforma_invoices(pi_number,request_id,request_version_id,purchase_plan_id,status,customer_user_id,
          company_name,contact_name,email,phone,whatsapp,country,delivery_address,seller_name,currency,
          product_amount,subtotal,total_amount,trade_terms,customer_note,internal_note,valid_until,
          created_by,updated_by,approved_by,created_at,updated_at,approved_at)
        VALUES(@Number,@RequestId,@VersionId,@PlanId,2,@UserId,@CompanyName,@ContactName,@Email,@Phone,@Whatsapp,
          @Country,@DeliveryAddress,'EGGRACKS','USD',@Total,@Total,@Total,@TradeTerms,@CustomerNote,@InternalNote,
          @ValidUntil,@StaffId,@StaffId,@StaffId,@Now,@Now,@Now)
        """,new{Number=number,RequestId=plan.RequestId,VersionId=plan.RequestVersionId,PlanId=plan.Id,customer.UserId,
            customer.CompanyName,customer.ContactName,customer.Email,customer.Phone,customer.Whatsapp,customer.Country,
            customer.DeliveryAddress,customer.TradeTerms,customer.CustomerNote,InternalNote=internalNote,Total=totalAmount,
            ValidUntil=now+14*24*60*60,StaffId=staffId,Now=now},cancellationToken:token);
        var invoiceId=(await db.QueryAsync<uint>("SELECT LAST_INSERT_ID()",cancellationToken:token)).Single();
        var remaining=totalAmount;
        for(var index=0;index<items.Count;index++)
        {
            var item=items[index];
            var lineAmount=index==items.Count-1?remaining:Math.Round(totalAmount/items.Count,2,MidpointRounding.AwayFromZero);
            remaining-=lineAmount;
            var unitPrice=Math.Round(lineAmount/item.Quantity,4,MidpointRounding.AwayFromZero);
            await db.ExecuteAsync("""
            INSERT proforma_invoice_items(pi_id,purchase_plan_item_id,request_item_id,product_key,sort_order,
              product_name,sku,brand,description,quantity,quantity_unit,specifications,color,size,
              packaging_requirements,customization_requirements,customer_note,unit_price,line_amount,
              created_by,updated_by,created_at,updated_at)
            VALUES(@InvoiceId,@Id,@RequestItemId,@ProductKey,@SortOrder,@ProductName,@Sku,@Brand,@Description,
              @Quantity,@QuantityUnit,@Specifications,@Color,@Size,@PackagingRequirements,@CustomizationRequirements,
              @CustomerNote,@UnitPrice,@LineAmount,@StaffId,@StaffId,@Now,@Now)
            """,new{InvoiceId=invoiceId,item.Id,item.RequestItemId,item.ProductKey,item.SortOrder,item.ProductName,
                item.Sku,item.Brand,item.Description,item.Quantity,item.QuantityUnit,item.Specifications,item.Color,item.Size,
                item.PackagingRequirements,item.CustomizationRequirements,item.CustomerNote,UnitPrice=unitPrice,LineAmount=lineAmount,
                StaffId=staffId,Now=now},cancellationToken:token);
        }
        return new(invoiceId,number);
    }

    public async Task<ProcurementLifecycleResult> IssueProformaInvoiceAsync(uint planId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.ExecuteInTransactionAsync<ProcurementLifecycleResult>(async transactionToken=>
        {
            var plans=await db.QueryAsync<byte>("SELECT status FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken);
            if(plans.Count==0) throw new BusinessRuleException("采购计划不存在。","procurement.plan.missing");
            if(plans.Single()!=4) throw new BusinessRuleException("只有已批准报价的采购计划才能签发 PI。","procurement.pi.issue-state");
            var invoices=await db.QueryAsync<ProformaInvoiceSummary>("SELECT id Id,pi_number Number,CASE status WHEN 2 THEN 'Approved' WHEN 3 THEN 'Issued' ELSE 'Invalid' END Status,total_amount TotalAmount,currency Currency,FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(issued_at) IssuedAtUtc FROM proforma_invoices WHERE purchase_plan_id=@PlanId ORDER BY id DESC LIMIT 1 FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken);
            var invoice=invoices.SingleOrDefault()??throw new BusinessRuleException("采购计划尚未生成 PI。","procurement.pi.missing");
            if(invoice.Status!="Approved") throw new BusinessRuleException("当前 PI 状态不能签发。","procurement.pi.issue-state");
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var changed=await db.ExecuteAsync("UPDATE proforma_invoices SET status=3,issued_by=@StaffId,issued_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@Id AND status=2",new{invoice.Id,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            if(changed!=1) throw new BusinessRuleException("PI 已被其他操作处理，请刷新后重试。","procurement.pi.concurrent-change");
            await db.ExecuteAsync("UPDATE purchase_plans SET quoted_by=@StaffId,quoted_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId",new{PlanId=planId,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            return new(planId,"Approved",invoice.Id,invoice.Number,"Issued");
        },cancellationToken:token);
    }

    public async Task<ProcurementLifecycleResult> CompletePlanAsync(uint planId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        return await db.ExecuteInTransactionAsync<ProcurementLifecycleResult>(async transactionToken=>
        {
            var invoices=await db.QueryAsync<ProformaInvoiceSummary>("SELECT i.id Id,i.pi_number Number,CASE i.status WHEN 3 THEN 'Issued' ELSE 'Invalid' END Status,i.total_amount TotalAmount,i.currency Currency,FROM_UNIXTIME(i.created_at) CreatedAtUtc,FROM_UNIXTIME(i.issued_at) IssuedAtUtc FROM purchase_plans p JOIN proforma_invoices i ON i.purchase_plan_id=p.id AND i.status=3 WHERE p.id=@PlanId AND p.status=4 ORDER BY i.id DESC LIMIT 1 FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken);
            var invoice=invoices.SingleOrDefault()??throw new BusinessRuleException("只有已签发 PI 的采购计划才能完成。","procurement.plan.complete-state");
            var changed=await db.ExecuteAsync("UPDATE purchase_plans SET status=5,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=4",new{PlanId=planId,StaffId=staffId,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:transactionToken);
            if(changed!=1) throw new BusinessRuleException("采购计划已被其他操作处理，请刷新后重试。","procurement.plan.concurrent-change");
            return new(planId,"Completed",invoice.Id,invoice.Number,"Issued");
        },cancellationToken:token);
    }
}
