using System.Numerics;

namespace AutoRaidHelper;

public static class GeometryUtilsXZ
{
    /// <summary>
    /// 在 XZ 平面计算两点的 2D 距离，忽略 Y
    /// </summary>
    public static float DistanceXZ(Vector3 a, Vector3 b)
    {
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// 在 XZ 平面计算：以 basePos 为圆心，向量(base->v1)与(base->v2) 的夹角(0~180)
    /// 忽略 Y
    /// </summary>
    public static float AngleXZ(Vector3 v1, Vector3 v2, Vector3 basePos)
    {
        var a = new Vector3(v1.X - basePos.X, 0, v1.Z - basePos.Z);
        var b = new Vector3(v2.X - basePos.X, 0, v2.Z - basePos.Z);

        var dot = Vector3.Dot(a, b);
        var magA = a.Length();
        var magB = b.Length();

        if (magA < 1e-6f || magB < 1e-6f)
            return 0f;

        var cosTheta = dot / (magA * magB);
        cosTheta = Math.Clamp(cosTheta, -1f, 1f);

        var rad = MathF.Acos(cosTheta);
        return rad * (180f / MathF.PI);
    }

    /// <summary>
    /// 弦长、角度(°)、半径 互算
    /// chord=null => angle+radius => chord
    /// angle=null => chord+radius => angle
    /// radius=null => chord+angle => radius
    /// </summary>
    public static (float? value, string desc) ChordAngleRadius(float? chord, float? angleDeg, float? radius)
    {
        // 1) angle+radius => chord
        if (chord == null && angleDeg != null && radius != null)
        {
            var angleRad = angleDeg.Value * MathF.PI / 180f;
            var c = 2f * radius.Value * MathF.Sin(angleRad / 2f);
            return (c, "弦长");
        }

        // 2) chord+radius => angle
        if (angleDeg == null && chord != null && radius != null)
        {
            var x = chord.Value / (2f * radius.Value);
            x = Math.Clamp(x, -1f, 1f);
            var aRad = 2f * MathF.Asin(x);
            var aDeg = aRad * (180f / MathF.PI);
            return (aDeg, "角度(°)");
        }

        // 3) chord+angle => radius
        if (radius == null && chord != null && angleDeg != null)
        {
            var angleRad = angleDeg.Value * MathF.PI / 180f;
            var denominator = 2f * MathF.Sin(angleRad / 2f);
            if (Math.Abs(denominator) < 1e-6f)
                return (null, "角度过小,无法计算半径");
            var r = chord.Value / denominator;
            return (r, "半径");
        }

        return (null, "请只留一个值为空，其余两个有值");
    }
}