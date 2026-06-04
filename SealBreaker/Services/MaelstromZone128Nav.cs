using System;
using System.Collections.Generic;
using System.Numerics;

namespace SealBreaker.Services;

/// <summary>Zone 128 — stay within mapped routes; reconnect via closest waypoint then walk toward GC merchants.</summary>
internal static class MaelstromZone128Nav
{
    public const float AnchorRadius = 32f;
    /// <summary>GC officer/shop/crystal/corridor walkway — stay on this plane until repair descent.</summary>
    public const float GcWalkwayMinY = 38f;

    public enum Segment
    {
        Approach,
        Corridor,
        Repair,
    }

    public readonly record struct RouteHit(Segment Segment, int Index, Vector3 Point, float Distance);

    public static IEnumerable<(Segment Segment, Vector3[] Path)> GetMappedSegments(Configuration cfg, int gcIdx = 0)
    {
        var approach = GcNavRoutes.GetGcApproachPath(cfg, gcIdx);
        if (approach.Length > 0)
            yield return (Segment.Approach, approach);

        var corridor = GcNavRoutes.GetGcCorridorPath(cfg, gcIdx);
        if (corridor.Length > 0)
            yield return (Segment.Corridor, corridor);

        var repair = GcNavRoutes.GetRepairPath(cfg, gcIdx);
        if (repair.Length > 0)
            yield return (Segment.Repair, repair);
    }

    public static bool IsOnGcWalkway(Vector3 pos) => pos.Y >= GcWalkwayMinY;

    public static float RouteDistance(Vector3 pos, Vector3 point)
    {
        if (IsOnGcWalkway(pos) && point.Y >= GcWalkwayMinY)
        {
            var dx = pos.X - point.X;
            var dz = pos.Z - point.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        return Vector3.Distance(pos, point);
    }

    public static bool IsNearMappedOrMerchant(Vector3 pos, Configuration cfg, Vector3 officerPos, Vector3 shopPos)
    {
        if (RouteDistance(pos, officerPos) <= AnchorRadius)
            return true;
        if (RouteDistance(pos, shopPos) <= AnchorRadius)
            return true;

        foreach (var (_, path) in GetMappedSegments(cfg))
        {
            foreach (var point in path)
            {
                if (RouteDistance(pos, point) <= AnchorRadius)
                    return true;
            }
        }

        return false;
    }

    public static RouteHit? FindClosestMappedPoint(Vector3 pos, Configuration cfg, int gcIdx = 0)
    {
        RouteHit? best = null;
        foreach (var (segment, path) in GetMappedSegments(cfg, gcIdx))
        {
            for (var i = 0; i < path.Length; i++)
            {
                var dist = RouteDistance(pos, path[i]);
                if (best == null || dist < best.Value.Distance)
                    best = new RouteHit(segment, i, path[i], dist);
            }
        }

        return best;
    }

    public static RouteHit? FindClosestSegmentPoint(Vector3 pos, Segment segment, Configuration cfg, int gcIdx = 0)
    {
        RouteHit? best = null;
        foreach (var (seg, path) in GetMappedSegments(cfg, gcIdx))
        {
            if (seg != segment)
                continue;

            for (var i = 0; i < path.Length; i++)
            {
                var dist = RouteDistance(pos, path[i]);
                if (best == null || dist < best.Value.Distance)
                    best = new RouteHit(seg, i, path[i], dist);
            }
        }

        return best;
    }

    public static Vector3[] SliceFromIndex(Vector3[] path, int index)
    {
        if (path.Length == 0)
            return [];

        if (index <= 0)
            return path;

        if (index >= path.Length)
            return [];

        var result = new Vector3[path.Length - index];
        Array.Copy(path, index, result, 0, result.Length);
        return result;
    }
}
