using Eggrack.Operations.Common.Models;
using Eggrack.Operations.Infrastructure.Database;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed partial class ProcurementDataService
{
    private sealed record EditablePlanItem(uint PlanId,byte Status);

    private static async Task<uint> EnsureSourcingEditableAsync(DatabaseSession db,uint planItemId,CancellationToken token)
    {
        var rows=await db.QueryAsync<EditablePlanItem>("SELECT p.id PlanId,p.status Status FROM purchase_plan_items i JOIN purchase_plans p ON p.id=i.plan_id WHERE i.id=@PlanItemId",new{PlanItemId=planItemId},cancellationToken:token);
        var item=rows.SingleOrDefault()??throw new BusinessRuleException("采购计划产品不存在。","procurement.plan-item.missing");
        if(item.Status is not (1 or 2)) throw new BusinessRuleException("采购计划已进入审核、报价或完成阶段，不能再修改寻源数据。","procurement.sourcing.readonly");
        return item.PlanId;
    }

    private static async Task EnsureRecordPlanAsync(DatabaseSession db,string table,uint recordId,uint targetPlanId,CancellationToken token)
    {
        if(table is not ("procurement_candidate_products" or "procurement_inquiries" or "procurement_samples")) throw new ArgumentOutOfRangeException(nameof(table));
        var rows=await db.QueryAsync<uint>($"SELECT i.plan_id FROM {table} r JOIN purchase_plan_items i ON i.id=r.plan_item_id WHERE r.id=@RecordId",new{RecordId=recordId},cancellationToken:token);
        if(rows.Count!=1) throw new BusinessRuleException("要修改的寻源记录不存在。","procurement.sourcing-record.missing");
        if(rows.Single()!=targetPlanId) throw new BusinessRuleException("不能把寻源记录移动到其他采购计划。","procurement.sourcing-record.cross-plan");
    }
}
