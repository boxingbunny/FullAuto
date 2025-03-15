using System.Numerics;
using AEAssist;
using AEAssist.Helper;
using Dalamud.Game.ClientState.Objects.Types;

namespace AutoRaidHelper;

public static class Utilities
{
    /// <summary>
    /// 传送到指定位置同时绘制Debug点。
    /// </summary>
    /// <param name="regexRole">职能</param>
    /// <param name="position">坐标</param>
    public static void SetPosAndDebugPoint(string regexRole, Vector3 position)
    {
        RemoteControlHelper.SetPos(regexRole, position);
        Share.TrustDebugPoint.Add(position);
    }

    /// <summary>
    /// 锁定到指定位置同时绘制Debug点，
    /// 主要用于防止突进等导致TP失效。
    /// </summary>
    /// <param name="regexRole">职能</param>
    /// <param name="position">坐标</param>
    /// <param name="time">锁定时间</param>
    public static void LockPosAndDebugPoint(string regexRole, Vector3 position, int time = 10000)
    {
        RemoteControlHelper.LockPos(regexRole, position, time);
        Share.TrustDebugPoint.Add(position);
    }

    /// <summary>
    /// 旋转并缩放坐标
    /// </summary>
    /// <param name="center">旋转基准点</param>
    /// <param name="position">输入的坐标</param>
    /// <param name="angle">旋转的角度</param>
    /// <param name="length">缩放的长度(正值朝内，负值朝外)</param>
    /// <returns>旋转并缩放后的坐标</returns>
    public static Vector3 RotateAndExpend(Vector3 center, Vector3 position, float angle, float length = 0)
    {
        var radian = angle * MathF.PI / 180;
        var x = (position.X - center.X) * MathF.Cos(radian) - (position.Z - center.Z) * MathF.Sin(radian) + center.X;
        var z = (position.X - center.X) * MathF.Sin(radian) + (position.Z - center.Z) * MathF.Cos(radian) + center.Z;
        var position2 = new Vector3(x, 0, z);
        var unit = Vector3.Normalize(position2 - center);
        return position2 - unit * length;
    }

    /// <summary>
    /// 旋转并缩放坐标
    /// </summary>
    /// <param name="position">输入的坐标</param>
    /// <param name="angle">旋转的角度</param>
    /// <param name="length">缩放的长度(正值朝内，负值朝外)</param>
    /// <returns>旋转并缩放后的坐标</returns>
    public static Vector3 RotateAndExpend(Vector3 position, float angle, uint length = 0)
    {
        var center = new Vector3(100, 0, 100);
        var radian = angle * MathF.PI / 180;
        var x = (position.X - center.X) * MathF.Cos(radian) - (position.Z - center.Z) * MathF.Sin(radian) + center.X;
        var z = (position.X - center.X) * MathF.Sin(radian) + (position.Z - center.Z) * MathF.Cos(radian) + center.Z;
        var position2 = new Vector3(x, 0, z);
        var unit = Vector3.Normalize(position2 - center);
        return position2 - unit * length;
    }

    /// <summary>
    /// 龙诗P7找塔
    /// </summary>
    /// <param name="towers">三个塔的列表</param>
    /// <returns>未被其他塔指向的塔</returns>
    public static IBattleChara? FindUncoveredTower(List<IBattleChara> towers)
    {
        return (from target in towers
            let isCovered =
                (from source in towers
                    where source != target
                    let vec = target.Position - source.Position
                    where vec != Vector3.Zero
                    let dir = Vector2.Normalize(new Vector2(vec.X, vec.Z))
                    let forwardDir = new Vector2((float)Math.Sin(source.Rotation), (float)Math.Cos(source.Rotation))
                    let leftDir =
                        new Vector2((float)Math.Sin(source.Rotation + MathF.PI / 2),
                            (float)Math.Cos(source.Rotation + MathF.PI / 2))
                    let rightDir =
                        new Vector2((float)Math.Sin(source.Rotation - MathF.PI / 2),
                            (float)Math.Cos(source.Rotation - MathF.PI / 2))
                    where IsDirectionCovered(dir, forwardDir) || IsDirectionCovered(dir, leftDir) ||
                          IsDirectionCovered(dir, rightDir)
                    select dir).Any()
            where !isCovered
            select target).FirstOrDefault();
    }

    /// <summary>
    /// 方向是否被覆盖
    /// </summary>
    /// <param name="dir">方向</param>
    /// <param name="sourceDir">源方向</param>
    /// <param name="toleranceDegrees">容忍角度</param>
    /// <returns>是否被覆盖</returns>
    public static bool IsDirectionCovered(Vector2 dir, Vector2 sourceDir, float toleranceDegrees = 22.5f)
    {
        var angleDir = MathF.Atan2(dir.Y, dir.X);
        angleDir = (angleDir + 2 * MathF.PI) % (2 * MathF.PI);

        var angleSource = MathF.Atan2(sourceDir.Y, sourceDir.X);
        angleSource = (angleSource + 2 * MathF.PI) % (2 * MathF.PI);

        var angleDiff = MathF.Abs(angleDir - angleSource);
        angleDiff = MathF.Min(angleDiff, 2 * MathF.PI - angleDiff);

        return angleDiff <= toleranceDegrees * MathF.PI / 180f;
    }

    /// <summary>
    /// 计算同时背对两个坐标的面向
    /// </summary>
    /// <param name="boss1V3">需要背对的BOSS1坐标</param>
    /// <param name="boss2V3">需要背对的BOSS2坐标</param>
    /// <param name="playerV3">角色坐标</param>
    /// <returns>同时背对的面向</returns>
    public static float CalculateRotationAngle(Vector3 boss1V3, Vector3 boss2V3, Vector3 playerV3)
    {
        var boss1 = new Vector2(boss1V3.X, boss1V3.Z);
        var boss2 = new Vector2(boss2V3.X, boss2V3.Z);
        var player = new Vector2(playerV3.X, playerV3.Z);
        var awayFromBoss1 = Vector2.Normalize(player - boss1);
        var awayFromBoss2 = Vector2.Normalize(player - boss2);
        var averageDirection = Vector2.Normalize(awayFromBoss1 + awayFromBoss2);
        var angle = (float)Math.Atan2(averageDirection.X, averageDirection.Y);
        switch (angle)
        {
            case > (float)Math.PI:
                angle -= 2 * (float)Math.PI;
                break;
            case < -(float)Math.PI:
                angle += 2 * (float)Math.PI;
                break;
        }

        return angle;
    }

    /// <summary>
    /// 计算三角形长边对角的坐标
    /// </summary>
    /// <param name="A">A点坐标</param>
    /// <param name="B">B点坐标</param>
    /// <param name="C">C点坐标</param>
    /// <returns>长边对角坐标</returns>
    public static Vector3 FindRightAngle(Vector3 A, Vector3 B, Vector3 C)
    {
        var AB2 = Vector3.DistanceSquared(A, B);
        var BC2 = Vector3.DistanceSquared(B, C);
        var CA2 = Vector3.DistanceSquared(C, A);

        if (AB2 >= BC2 && AB2 >= CA2)
        {
            return C;
        }

        if (BC2 >= AB2 && BC2 >= CA2)
        {
            return A;
        }

        return B;
    }

    /// <summary>
    /// 计算两点相对于基准点的角度
    /// </summary>
    /// <param name="pivot">基准点坐标</param>
    /// <param name="reference">参考点坐标</param>
    /// <param name="target">目标坐标</param>
    /// <returns>旋转角度</returns>
    public static float GetAngleClockwise(Vector3 pivot, Vector3 reference, Vector3 target)
    {
        var baselineVec = reference - pivot;
        var targetVec = target - pivot;

        var baselineAngle = Math.Atan2(baselineVec.X, baselineVec.Z) * (180.0 / Math.PI);
        var targetAngle = Math.Atan2(targetVec.X, targetVec.Z) * (180.0 / Math.PI);

        baselineAngle = (baselineAngle + 360.0) % 360.0;
        targetAngle = (targetAngle + 360.0) % 360.0;

        var diffAngleCW = baselineAngle - targetAngle;

        diffAngleCW = (diffAngleCW + 360.0) % 360.0;

        return (float)diffAngleCW;
    }

    /// <summary>
    /// 输入坐标，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <param name="diagDivision">斜分割，默认true</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int Position2Dirs(this Vector3 point, Vector3 center, int dirs, bool diagDivision = true)
    {
        double dirsDouble = dirs;
        var r = diagDivision
            ? Math.Round(dirsDouble / 2 -
                         dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble
            : Math.Floor(dirsDouble / 2 -
                         dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble;
        return (int)r;
    }

    private static Vector3 _stageCenter = new(100f, 0f, 100f);

    /// <summary>
    /// 设置场地中心
    /// </summary>
    /// <param name="center">新的场地中心坐标</param>
    public static void SetStageCenter(Vector3 center) => _stageCenter = center;

    /// <summary>
    /// 通过旋转角度和距离计算坐标
    /// </summary>
    /// <param name="startPoint">起始点坐标</param>
    /// <param name="degrees">旋转角度（度）</param>
    /// <param name="clockwise">是否顺时针旋转</param>
    /// <param name="distance">旋转后沿该方向移动的距离</param>
    /// <returns>计算得到的新坐标</returns>
    public static Vector3 GetPositionByRotation(Vector3 startPoint, float degrees, bool clockwise, float distance)
    {
        // 计算初始点到场地中心的方向向量
        Vector3 direction = startPoint - _stageCenter;
        // 计算旋转方向
        float radians = MathF.PI * degrees / 180f * (clockwise ? 1 : -1); // 顺时针为正，逆时针为负

        // 计算旋转后的方向向量
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        Vector3 rotatedDirection = new Vector3(
            direction.X * cos - direction.Z * sin,
            0,
            direction.X * sin + direction.Z * cos
        );

        // 计算最终位置（在旋转后的方向上移动指定距离）
        Vector3 finalPosition = _stageCenter + Vector3.Normalize(rotatedDirection) * distance;

        return finalPosition;
    }

    /// <summary>
    /// 计算从场地中心 (_stageCenter) 到目标点 (target) 的连线上，
    /// 根据给定的偏移量 (offsetX, offsetZ) 得到一个新的坐标点。
    /// </summary>
    /// <param name="target">目标坐标 (X, Z)</param>
    /// <param name="offsetX">沿正交方向 (右侧) 的偏移量</param>
    /// <param name="offsetZ">沿目标方向 (前进/后退) 的偏移量</param>
    /// <returns>偏移后的 Vector3 坐标，Y 固定为 0</returns>
    public static Vector3 GetOffsetPosition(Vector3 target, float offsetX, float offsetZ)
    {
        var dirVector = target - _stageCenter;
        dirVector.Y = 0;
        if (dirVector.LengthSquared() == 0)
            return _stageCenter + new Vector3(offsetX, 0, offsetZ);
        dirVector = Vector3.Normalize(dirVector);
        // 计算正交方向（顺时针旋转 90°，即右侧方向）
        var perpendicular = new Vector3(-dirVector.Z, 0, dirVector.X);
        // 计算最终偏移坐标
        var offsetPosition = _stageCenter + (dirVector * offsetZ) + (perpendicular * offsetX);
        return offsetPosition;
    }

    /// <summary>
    /// 绝欧P3塔排序，判断两塔的顺逆并调整位置，确保塔0到塔1的坐标顺序始终为顺时针。
    /// </summary>
    /// <param name="towerPositions">两塔坐标的数组</param>
    public static void SortTowersClockwise(Vector3[] towerPositions)
    {
        Vector3 v0 = towerPositions[0] - _stageCenter;
        Vector3 v1 = towerPositions[1] - _stageCenter;

        float crossProduct = v0.X * v1.Z - v0.Z * v1.X;

        // 如果叉积大于 0，说明两座塔按顺时针排列；否则为逆时针
        if (crossProduct > 0)
            return;

        // 如果是逆时针，则交换两塔的位置
        (towerPositions[0], towerPositions[1]) = (towerPositions[1], towerPositions[0]);
    }

    /// <summary>
    /// 计算场地中心与目标点之间的角度并归一化。
    /// </summary>
    /// <param name="pos">目标点坐标</param>
    /// <returns>目标点相对于场地中心的角度</returns>
    public static float CalculateAngleFromCenter(Vector3 pos)
    {
        Vector3 diff = pos - _stageCenter;
        float rad = MathF.Atan2(diff.X, -diff.Z);
        float angle = (rad * (180f / MathF.PI) + 360f) % 360f;
        return angle;
    }

    /// <summary>
    /// 判断两个地火位置相对于场地中心的刷新方向
    /// </summary>
    /// <param name="firePos1">第一个地火的位置</param>
    /// <param name="firePos2">第二个地火的位置</param>
    /// <returns>如果刷新方向为顺时针则返回 true，否则返回 false</returns>
    public static bool IsExaflareClockwise(Vector3 firePos1, Vector3 firePos2)
    {
        float angle1 = CalculateAngleFromCenter(firePos1);
        float angle2 = CalculateAngleFromCenter(firePos2);

        // 计算从第一个地火到第二个地火的顺时针角度差
        float diffCW = (angle2 - angle1 + 360f) % 360f;
        // 计算逆时针角度差
        float diffCCW = (angle1 - angle2 + 360f) % 360f;

        return diffCW < diffCCW;
    }


    /// <summary>
    /// 将弧度转换为标准化角度（0-360度）
    /// </summary>
    /// <param name="radians">输入的弧度值</param>
    /// <returns>转换后的角度，范围为0到360度</returns>
    public static float RadiansToNormalizedDegrees(float radians)
    {
        var degrees = radians * (180f / MathF.PI);
        return (degrees % 360 + 360) % 360; // 确保结果在0-360之间
    }

    /// <summary>
    /// 弧度转换为标准化角度（0-360度）的扩展方法
    /// </summary>
    /// <param name="radians">输入的弧度值</param>
    /// <returns>转换后的角度，范围为0到360度</returns>
    public static float RadToNormalizedDeg(this float radians) => RadiansToNormalizedDegrees(radians);

    /// <summary>
    /// 根据角色名称传送到指定位置，并记录调试信息
    /// </summary>
    /// <param name="name">角色名称或职能</param>
    /// <param name="pos">目标位置坐标</param>
    /// <param name="dev">调试信息或调用描述</param>
    public static void TPbyRole(string name, Vector3 pos, string dev)
    {
        try
        {
            var role = new HashSet<string> { "D1", "D2", "D3", "D4", "H1", "H2", "MT", "ST" };
            RemoteControlHelper.SetPos(!role.Contains(name) ? RemoteControlHelper.GetRoleByPlayerName(name) : name,
                pos);
            if (!FullAutoSettings.PrintDebugInfo) return;
            LogHelper.Print($"{dev}: {name} 移动至 {pos}");
            Share.TrustDebugPoint.Add(pos);
        }
        catch (Exception ex)
        {
            LogHelper.PrintError($"移动到位置失败 ({dev}): {ex.Message}");
        }
    }

    /// <summary>
    /// 锁定指定角色到给定位置，并记录调试信息
    /// </summary>
    /// <param name="name">角色名称或职能</param>
    /// <param name="pos">目标位置坐标</param>
    /// <param name="duration">锁定持续时间（毫秒）</param>
    /// <param name="dev">调试信息或调用描述</param>
    public static void LockbyRole(string name, Vector3 pos, int duration, string dev)
    {
        try
        {
            var role = new HashSet<string> { "D1", "D2", "D3", "D4", "H1", "H2", "MT", "ST" };
            RemoteControlHelper.LockPos(!role.Contains(name) ? RemoteControlHelper.GetRoleByPlayerName(name) : name, pos, duration);
            RemoteControlHelper.LockPos(name, pos, duration);
            LogHelper.Print($"{dev}: {name} 锁定在 {pos} {duration}ms");
            Share.TrustDebugPoint.Add(pos);
        }
        catch (Exception ex)
        {
            LogHelper.PrintError($"锁定到位置失败 ({dev}): {ex.Message}");
        }
    }

    /// <summary>
    /// 调整角色面向，使其同时背向指定目标点，并通过传送实现旋转效果
    /// </summary>
    /// <param name="name">角色名称或职能</param>
    /// <param name="rot">目标旋转角度（弧度）</param>
    /// <param name="targetPos">目标位置坐标</param>
    public static async void SetRotbyRole(string name, float rot, Vector3 targetPos)
    {
        try
        {
            name = RemoteControlHelper.GetRoleByPlayerName(name);
            RemoteControlHelper.Stop(name, true);
            await Coroutine.Instance.WaitAsync(500);
            RemoteControlHelper.Cmd(name, "/共通技能 跳跃");
            var offsetDistance = 1.0f;
            var offsetAngle = NormalizeRotation(rot + MathF.PI);
            var offsetX = offsetDistance * MathF.Sin(offsetAngle);
            var offsetZ = offsetDistance * MathF.Cos(offsetAngle);
            var offsetPos = new Vector3(targetPos.X + offsetX, targetPos.Y, targetPos.Z + offsetZ);
            TPbyRole(name, offsetPos, "调整面向 - TP到偏移位置");
            RemoteControlHelper.MoveTo(name, targetPos);
        }
        catch (Exception ex)
        {
            LogHelper.PrintError($"设置旋转角度失败 ({name}): {ex.Message}");
        }
    }

    /// <summary>
    /// 将角度归一化到 [-π, +π] 范围内
    /// </summary>
    /// <param name="rotation">原始角度（弧度）</param>
    /// <returns>归一化后的角度（弧度）</returns>
    public static float NormalizeRotation(float rotation)
    {
        while (rotation > MathF.PI)
            rotation -= 2f * MathF.PI;
        while (rotation <= -MathF.PI)
            rotation += 2f * MathF.PI;
        return rotation;
    }

    private static Vector3 ExtendLine(Vector3 Center, Vector3 Direction, float distance)
    {
        var dx = Direction.X - Center.X;
        var dz = Direction.Z - Center.Z;

        var length = MathF.Sqrt(dx * dx + dz * dz);

        var ux = dx / length;
        var uz = dz / length;

        var px = Direction.X + distance * ux;
        var pz = Direction.Z + distance * uz;

        return new Vector3(px, Direction.Y, pz);
    }
}