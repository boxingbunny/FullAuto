using System.Numerics;

namespace AutoRaidHelper.Utils;

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
    
    /// <summary>
    /// 计算给定点相对于一个参考方向的偏移量。
    /// 该参考方向由“场地中心”到“顶点”（apexPos）确定，
    /// 即将场地中心 (stageCenter) 到 apexPos 的向量作为前进方向，并以其顺时针旋转 90° 得到右侧方向。
    /// 返回的 offsetZ 表示点在前进方向上的分量（正值表示点离场地中心沿参考方向的距离），
    /// offsetX 表示点在右侧方向上的分量（正值表示点在右侧的偏移）。
    /// </summary>
    /// <param name="point">需要计算偏移的目标点</param>
    /// <param name="apexPos">作为参考的顶点位置，通常为用户通过 Alt 键记录的点</param>
    /// <param name="stageCenter">场地中心坐标，作为计算偏移的基准点</param>
    /// <returns>一个元组，其中 offsetX 为右侧偏移，offsetZ 为前进偏移</returns>
    public static (float offsetX, float offsetZ) CalculateOffsetFromReference(Vector3 point, Vector3 apexPos, Vector3 stageCenter)
    {
        // 1) 计算前进方向：从场地中心指向顶点
        //    这里 dir 表示场地中心到 apexPos 的向量（忽略 Y 轴）
        Vector3 dir = apexPos - stageCenter;
        dir.Y = 0;

        // 如果场地中心与顶点几乎重合（向量长度非常小），则无法确定参考方向，
        // 此时直接使用 point 与 apexPos 的差作为偏移（退化处理）
        if (dir.LengthSquared() < 1e-6f)
        {
            float fallbackX = point.X - apexPos.X;
            float fallbackZ = point.Z - apexPos.Z;
            return (fallbackX, fallbackZ);
        }

        // 归一化前进方向向量，使其长度为 1，
        // 这样后续计算偏移时，投影得到的分量直接代表在该方向上的距离
        dir = Vector3.Normalize(dir);

        // 2) 计算右侧方向：将前进方向顺时针旋转 90° 得到
        //    右侧方向用于计算点在参考方向正交方向上的偏移
        Vector3 perpendicular = new Vector3(-dir.Z, 0, dir.X);

        // 3) 计算目标点相对于场地中心的差向量（忽略 Y 轴）
        //    diff 表示从场地中心到目标点的向量
        Vector3 diff = point - stageCenter;
        diff.Y = 0;

        // 4) 将差向量分别投影到前进方向和右侧方向上
        //    使用点积计算分量：
        //    offsetZ：在前进方向上的分量
        //    offsetX：在右侧方向上的分量
        float offsetZ = Vector3.Dot(diff, dir);
        float offsetX = Vector3.Dot(diff, perpendicular);

        return (offsetX, offsetZ);
    }


}