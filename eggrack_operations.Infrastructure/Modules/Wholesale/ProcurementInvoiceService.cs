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
        VALUES(@Number,@RequestId,@VersionId,@PlanId,1,@UserId,@CompanyName,@ContactName,@Email,@Phone,@Whatsapp,
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

    private sealed record InvoicePricingHeader(uint Id,byte Status,decimal ApprovedQuoteUsd,decimal PackagingFee,decimal ShippingFee,decimal OtherFee,decimal DiscountAmount);
    private sealed record InvoiceValidation(byte Status,decimal ApprovedQuoteUsd,decimal ProductAmount,decimal PackagingFee,decimal ShippingFee,decimal OtherFee,decimal DiscountAmount,decimal TotalAmount,decimal ItemAmount);

    private sealed record InvoicePricingViewHeader(uint InvoiceId,decimal ProductAmount,decimal PackagingFee,decimal ShippingFee,decimal OtherFee,decimal DiscountAmount,decimal TotalAmount);
    private sealed record InvoiceIssuePlan(byte Status,decimal ApprovedQuoteUsd);
    public async Task<ProformaInvoicePricing> UpdateProformaInvoicePricingAsync(uint planId,UpdateProformaInvoicePricingCommand command,long staffId,CancellationToken token=default)
    {
        if(command.Items is null||command.Items.Count==0) throw new BusinessRuleException("PI 至少需要一个产品明细。","procurement.pi.items-empty");
        if(command.PackagingFee<0||command.ShippingFee<0||command.OtherFee<0||command.DiscountAmount<0) throw new BusinessRuleException("PI 费用和折扣不能为负数。","procurement.pi.fee-invalid");
        if(command.Items.Any(item=>item.UnitPrice<=0)) throw new BusinessRuleException("PI 产品单价必须大于零。","procurement.pi.unit-price-invalid");
        if(command.Items.Select(item=>item.Id).Distinct().Count()!=command.Items.Count) throw new BusinessRuleException("PI 产品明细不能重复。","procurement.pi.item-duplicate");
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.ExecuteInTransactionAsync<ProformaInvoicePricing>(async transactionToken=>
        {
            var headers=await db.QueryAsync<InvoicePricingHeader>("SELECT i.id Id,i.status Status,p.approved_quote_amount_usd ApprovedQuoteUsd,i.packaging_fee PackagingFee,i.shipping_fee ShippingFee,i.other_fee OtherFee,i.discount_amount DiscountAmount FROM proforma_invoices i JOIN purchase_plans p ON p.id=i.purchase_plan_id WHERE i.id=@InvoiceId AND i.purchase_plan_id=@PlanId FOR UPDATE",new{command.InvoiceId,PlanId=planId},cancellationToken:transactionToken);
            var header=headers.SingleOrDefault()??throw new BusinessRuleException("PI 不存在或不属于当前采购计划。","procurement.pi.missing");
            if(header.Status is not (1 or 2)) throw new BusinessRuleException("只有草稿或待签发 PI 可以修改定价。","procurement.pi.pricing-state");
            var existing=await db.QueryAsync<ProformaInvoicePriceItem>("SELECT id Id,product_name ProductName,quantity Quantity,quantity_unit Unit,unit_price UnitPrice,line_amount LineAmount FROM proforma_invoice_items WHERE pi_id=@InvoiceId ORDER BY sort_order,id FOR UPDATE",new{command.InvoiceId},cancellationToken:transactionToken);
            if(existing.Count!=command.Items.Count||existing.Select(item=>item.Id).Except(command.Items.Select(item=>item.Id)).Any()) throw new BusinessRuleException("提交的 PI 产品明细与数据库不一致，请刷新后重试。","procurement.pi.items-mismatch");
            var prices=command.Items.ToDictionary(item=>item.Id,item=>item.UnitPrice);
            var lineAmounts=new List<decimal>();
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach(var item in existing)
            {
                var unitPrice=Math.Round(prices[item.Id],4,MidpointRounding.AwayFromZero);
                var lineAmount=ProformaInvoicePricingCalculator.CalculateLineAmount(item.Quantity,unitPrice);
                lineAmounts.Add(lineAmount);
                await db.ExecuteAsync("UPDATE proforma_invoice_items SET unit_price=@UnitPrice,line_amount=@LineAmount,updated_by=@StaffId,updated_at=@Now WHERE id=@Id AND pi_id=@InvoiceId",new{item.Id,command.InvoiceId,UnitPrice=unitPrice,LineAmount=lineAmount,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            }
            var totals=ProformaInvoicePricingCalculator.CalculateTotals(lineAmounts,command.PackagingFee,command.ShippingFee,command.OtherFee,command.DiscountAmount);
            var rawSubtotal=lineAmounts.Sum()+command.PackagingFee+command.ShippingFee+command.OtherFee;
            if(command.DiscountAmount>rawSubtotal) throw new BusinessRuleException("折扣不能大于 PI 小计。","procurement.pi.discount-invalid");
            if(Math.Abs(totals.TotalAmount-header.ApprovedQuoteUsd)>0.01m) throw new BusinessRuleException($"PI 总额必须等于已批准报价 {header.ApprovedQuoteUsd:N2} USD，当前为 {totals.TotalAmount:N2} USD。","procurement.pi.total-mismatch");
            await db.ExecuteAsync("UPDATE proforma_invoices SET status=2,product_amount=@ProductAmount,packaging_fee=@PackagingFee,shipping_fee=@ShippingFee,other_fee=@OtherFee,discount_amount=@DiscountAmount,subtotal=@Subtotal,total_amount=@Total,updated_by=@StaffId,updated_at=@Now WHERE id=@InvoiceId AND status IN (1,2)",new{command.InvoiceId,totals.ProductAmount,command.PackagingFee,command.ShippingFee,command.OtherFee,command.DiscountAmount,totals.Subtotal,Total=totals.TotalAmount,StaffId=staffId,Now=now},cancellationToken:transactionToken);
            var updated=existing.Select(item=>item with{UnitPrice=Math.Round(prices[item.Id],4,MidpointRounding.AwayFromZero),LineAmount=Math.Round(item.Quantity*prices[item.Id],2,MidpointRounding.AwayFromZero)}).ToArray();
            return new(command.InvoiceId,totals.ProductAmount,command.PackagingFee,command.ShippingFee,command.OtherFee,command.DiscountAmount,totals.TotalAmount,updated);
        },cancellationToken:token);
    }

    private static async Task<ProformaInvoicePricing?> GetProformaInvoicePricingAsync(DatabaseSession db,uint invoiceId,CancellationToken token)
    {
        var headers=await db.QueryAsync<InvoicePricingViewHeader>("SELECT id InvoiceId,product_amount ProductAmount,packaging_fee PackagingFee,shipping_fee ShippingFee,other_fee OtherFee,discount_amount DiscountAmount,total_amount TotalAmount FROM proforma_invoices WHERE id=@InvoiceId",new{InvoiceId=invoiceId},cancellationToken:token);
        var header=headers.SingleOrDefault();
        if(header is null)return null;
        var items=await db.QueryAsync<ProformaInvoicePriceItem>("SELECT id Id,product_name ProductName,quantity Quantity,quantity_unit Unit,unit_price UnitPrice,line_amount LineAmount FROM proforma_invoice_items WHERE pi_id=@InvoiceId ORDER BY sort_order,id",new{InvoiceId=invoiceId},cancellationToken:token);
        return new(header.InvoiceId,header.ProductAmount,header.PackagingFee,header.ShippingFee,header.OtherFee,header.DiscountAmount,header.TotalAmount,items);
    }
    public async Task<ProcurementLifecycleResult> IssueProformaInvoiceAsync(uint planId,long staffId,CancellationToken token=default)
    {
        await using var db=await databases.OpenMySqlAsync(DatabaseName,token);
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.ExecuteInTransactionAsync<ProcurementLifecycleResult>(async transactionToken=>
        {
            var plans=await db.QueryAsync<InvoiceIssuePlan>("SELECT status Status,approved_quote_amount_usd ApprovedQuoteUsd FROM purchase_plans WHERE id=@PlanId FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken);
            if(plans.Count==0) throw new BusinessRuleException("采购计划不存在。","procurement.plan.missing");
            if(plans.Single().Status!=4) throw new BusinessRuleException("只有已批准报价的采购计划才能签发 PI。","procurement.pi.issue-state");
            var invoices=await db.QueryAsync<ProformaInvoiceSummary>("SELECT id Id,pi_number Number,CASE status WHEN 2 THEN 'Approved' WHEN 3 THEN 'Issued' ELSE 'Invalid' END Status,total_amount TotalAmount,currency Currency,FROM_UNIXTIME(created_at) CreatedAtUtc,FROM_UNIXTIME(issued_at) IssuedAtUtc FROM proforma_invoices WHERE purchase_plan_id=@PlanId ORDER BY id DESC LIMIT 1 FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken);
            var invoice=invoices.SingleOrDefault()??throw new BusinessRuleException("采购计划尚未生成 PI。","procurement.pi.missing");
            if(invoice.Status!="Approved") throw new BusinessRuleException("当前 PI 状态不能签发。","procurement.pi.issue-state");
            var validations=await db.QueryAsync<InvoiceValidation>("SELECT i.status Status,p.approved_quote_amount_usd ApprovedQuoteUsd,i.product_amount ProductAmount,i.packaging_fee PackagingFee,i.shipping_fee ShippingFee,i.other_fee OtherFee,i.discount_amount DiscountAmount,i.total_amount TotalAmount,COALESCE((SELECT SUM(x.line_amount) FROM proforma_invoice_items x WHERE x.pi_id=i.id),0) ItemAmount FROM proforma_invoices i JOIN purchase_plans p ON p.id=i.purchase_plan_id WHERE i.id=@Id",new{invoice.Id},cancellationToken:transactionToken);
            var validation=validations.Single();
            var calculated=validation.ProductAmount+validation.PackagingFee+validation.ShippingFee+validation.OtherFee-validation.DiscountAmount;
            if(Math.Abs(validation.ItemAmount-validation.ProductAmount)>0.01m||Math.Abs(calculated-validation.TotalAmount)>0.01m||Math.Abs(validation.TotalAmount-validation.ApprovedQuoteUsd)>0.01m) throw new BusinessRuleException("PI 明细、费用或总额与已批准报价不一致，请先保存正确的 PI 定价。","procurement.pi.pricing-invalid");
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
        await scopePolicy.EnsurePlanAsync(db,planId,token);
        return await db.ExecuteInTransactionAsync<ProcurementLifecycleResult>(async transactionToken=>
        {
            var invoices=await db.QueryAsync<ProformaInvoiceSummary>("SELECT i.id Id,i.pi_number Number,CASE i.status WHEN 3 THEN 'Issued' ELSE 'Invalid' END Status,i.total_amount TotalAmount,i.currency Currency,FROM_UNIXTIME(i.created_at) CreatedAtUtc,FROM_UNIXTIME(i.issued_at) IssuedAtUtc FROM purchase_plans p JOIN proforma_invoices i ON i.purchase_plan_id=p.id AND i.status=3 WHERE p.id=@PlanId AND p.status=4 AND EXISTS(SELECT 1 FROM procurement_mail_tasks m WHERE m.plan_id=p.id AND m.status='sent') ORDER BY i.id DESC LIMIT 1 FOR UPDATE",new{PlanId=planId},cancellationToken:transactionToken);
            var invoice=invoices.SingleOrDefault()??throw new BusinessRuleException("只有 PI 已签发且客户邮件发送成功后才能完成采购计划。","procurement.plan.complete-state");
            var changed=await db.ExecuteAsync("UPDATE purchase_plans SET status=5,completed_at=@Now,updated_by=@StaffId,updated_at=@Now WHERE id=@PlanId AND status=4",new{PlanId=planId,StaffId=staffId,Now=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},cancellationToken:transactionToken);
            if(changed!=1) throw new BusinessRuleException("采购计划已被其他操作处理，请刷新后重试。","procurement.plan.concurrent-change");
            return new(planId,"Completed",invoice.Id,invoice.Number,"Issued");
        },cancellationToken:token);
    }
}
