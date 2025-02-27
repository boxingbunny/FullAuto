using System.Numerics;
using AEAssist;
using AEAssist.Helper;
using Dalamud.Game.ClientState.Objects.Types;

namespace AutoRaidHelper;

public static class Utilities
{
    private static void SetPosAndDebugPoint(string regexRole, Vector3 position)
    {
        RemoteControlHelper.SetPos(regexRole, position);
        Share.TrustDebugPoint.Add(position);
    }

    private static Vector3 RotateAndExpend(Vector3 center, Vector3 position, float angle, float length = 0)
    {
        var radian = angle * MathF.PI / 180;
        var x = (position.X - center.X) * MathF.Cos(radian) - (position.Z - center.Z) * MathF.Sin(radian) + center.X;
        var z = (position.X - center.X) * MathF.Sin(radian) + (position.Z - center.Z) * MathF.Cos(radian) + center.Z;
        var position2 = new Vector3(x, 0, z);
        var unit = Vector3.Normalize(position2 - center);
        return position2 - unit * length;
    }

    private static Vector3 RotateAndExpend(Vector3 position, float angle, uint length = 0)
    {
        var center = new Vector3(100, 0, 100);
        var radian = angle * MathF.PI / 180;
        var x = (position.X - center.X) * MathF.Cos(radian) - (position.Z - center.Z) * MathF.Sin(radian) + center.X;
        var z = (position.X - center.X) * MathF.Sin(radian) + (position.Z - center.Z) * MathF.Cos(radian) + center.Z;
        var position2 = new Vector3(x, 0, z);
        var unit = Vector3.Normalize(position2 - center);
        return position2 - unit * length;
    }

    private static IBattleChara? FindUncoveredTower(List<IBattleChara> towers)
    {
        foreach (var target in towers)
        {
            var isCovered = false;
            foreach (var source in towers)
            {
                if (source == target) continue;

                var vec = target.Position - source.Position;
                if (vec == Vector3.Zero) continue;

                var dir = Vector2.Normalize(new Vector2(vec.X, vec.Z));

                var forwardDir = new Vector2(
                    (float)Math.Sin(source.Rotation),
                    (float)Math.Cos(source.Rotation)
                );
                var leftDir = new Vector2(
                    (float)Math.Sin(source.Rotation + MathF.PI / 2),
                    (float)Math.Cos(source.Rotation + MathF.PI / 2)
                );
                var rightDir = new Vector2(
                    (float)Math.Sin(source.Rotation - MathF.PI / 2),
                    (float)Math.Cos(source.Rotation - MathF.PI / 2)
                );

                if (IsDirectionCovered(dir, forwardDir) ||
                    IsDirectionCovered(dir, leftDir) ||
                    IsDirectionCovered(dir, rightDir))
                {
                    isCovered = true;
                    break;
                }
            }

            if (!isCovered) return target;
        }
        return null;
    }

    private static bool IsDirectionCovered(Vector2 dir, Vector2 sourceDir, float toleranceDegrees = 22.5f)
    {
        var angleDir = MathF.Atan2(dir.Y, dir.X);
        angleDir = (angleDir + 2 * MathF.PI) % (2 * MathF.PI);

        var angleSource = MathF.Atan2(sourceDir.Y, sourceDir.X);
        angleSource = (angleSource + 2 * MathF.PI) % (2 * MathF.PI);

        var angleDiff = MathF.Abs(angleDir - angleSource);
        angleDiff = MathF.Min(angleDiff, 2 * MathF.PI - angleDiff);

        return angleDiff <= toleranceDegrees * MathF.PI / 180f;
    }

    private static float CalculateRotationAngle(Vector3 boss1V3, Vector3 boss2V3, Vector3 playerV3)
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
}