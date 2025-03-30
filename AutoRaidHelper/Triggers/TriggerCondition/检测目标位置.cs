using AEAssist;
using AEAssist.CombatRoutine.Trigger;
using AEAssist.GUI;
using ECommons.DalamudServices;
using ECommons.LanguageHelpers;
using System.Numerics;

namespace AutoRaidHelper.Triggers.TriggerCondition;

public class 检测目标位置 : ITriggerCond
{
    [LabelName("目标DataID")]
    public int targetDataID { get; set; }
    [LabelName("目标位置范围起始X坐标")]
    public float point1x { get; set; }
    [LabelName("目标位置范围起始Z坐标")]
    public float point1z { get; set; }
    [LabelName("目标位置范围终止X坐标")]
    public float point2x { get; set; }
    [LabelName("目标位置范围终止Z坐标")]
    public float point2z { get; set; }

    public string DisplayName => "检测目标位置".Loc();

    public string Remark { get; set; }

    public bool Draw()
    {
        return false;
    }

    public bool Handle(ITriggerCondParams triggerCondParams)
    {
        var target = Svc.Objects.FirstOrDefault(o =>
        {
            return o.DataId == targetDataID;
        });
        if(target == null)
        {
            return false;
        }

        Vector3 targetPosition = target.Position;

        bool isWithinX = (targetPosition.X >= Math.Min(point1x, point2x) && targetPosition.X <= Math.Max(point1x, point2x));
        bool isWithinZ = (targetPosition.Z >= Math.Min(point1z, point2z) && targetPosition.Z <= Math.Max(point1z, point2z));

        if (isWithinX && isWithinZ)
        {
            return true;
        }
        return false;
    }
}