using MUServer.Core.Models;

namespace MUServer.Core.World;

/// <summary>
/// Calcula qué entidades son visibles dentro del mundo.
/// No envía packets. Solo decide visibilidad.
/// </summary>
public sealed class VisionService
{
    public const int DefaultVisionRange = 15;

    public bool CanSee(Player viewer, Player target, int range = DefaultVisionRange)
    {
        if (viewer.PlayerId == target.PlayerId)
        {
            return false;
        }

        if (!viewer.IsOnline || !target.IsOnline)
        {
            return false;
        }

        if (viewer.CurrentMapId != target.CurrentMapId)
        {
            return false;
        }

        return IsInsideRange(viewer.X, viewer.Y, target.X, target.Y, range);
    }

    public bool CanSee(Player viewer, Monster monster, int range = DefaultVisionRange)
    {
        if (!viewer.IsOnline)
        {
            return false;
        }

        if (!monster.IsAlive)
        {
            return false;
        }

        if (viewer.CurrentMapId != monster.MapId)
        {
            return false;
        }

        return IsInsideRange(viewer.X, viewer.Y, monster.X, monster.Y, range);
    }

    public IReadOnlyList<Player> GetVisiblePlayers(Player viewer, IEnumerable<Player> players, int range = DefaultVisionRange)
    {
        return players
            .Where(target => CanSee(viewer, target, range))
            .ToList();
    }

    public IReadOnlyList<Monster> GetVisibleMonsters(Player viewer, IEnumerable<Monster> monsters, int range = DefaultVisionRange)
    {
        return monsters
            .Where(monster => CanSee(viewer, monster, range))
            .ToList();
    }

    private static bool IsInsideRange(byte x1, byte y1, byte x2, byte y2, int range)
    {
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);

        return dx <= range && dy <= range;
    }
}