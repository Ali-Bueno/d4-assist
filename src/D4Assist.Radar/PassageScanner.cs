using D4Assist.Audio;

namespace D4Assist.Radar;

/// <summary>What one scan needs besides the grid: D2R's PassageQuery (d2r-access src/radar.h:100-106).</summary>
/// <param name="Committed">The sensor's confirmed reading: inside the hysteresis band it stands.</param>
/// <param name="Radius">Passage window half-side, in cells (RadarSettings.PassageRadius).</param>
internal readonly record struct PassageQuery(
    double OpenDepth, double CloseDepth, RadarCueKind? Committed, double WayThrough, double LateralTolerance, int Radius);

/// <summary>
/// The straight route along one ray and what it means: port of D2R ScanPassage (d2r-access
/// src/radar_passage.cpp:35-108). BFS forward with one cell of lateral tolerance; passable at or beyond the
/// opening depth, an obstacle at or below the closing depth, the committed reading in between (no dead band).
/// D4: a route into unknown cells is an Exit (it leads beyond the loaded tiles), never a wall or a stall.
/// </summary>
internal static class PassageScanner
{
    // One diagonal step: a door counts when it stops the route within this much past the closing depth (radar_passage.cpp:9).
    private static readonly double MaxStep = Math.Sqrt(2);

    // Native movement trace 0x364700 allows diagonal destination steps (radar_passage.cpp:27-28).
    private static readonly (int X, int Y)[] Steps = [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)];

    public static Passage Scan(LocalGrid grid, RadarVector axis, PassageQuery query)
    {
        if (!grid.OriginKnown) return new Passage { OpenDepth = query.OpenDepth, CloseDepth = query.CloseDepth };

        var home = grid.Room(0, 0);
        double depth = 0, crossing = -1, unknownDepth = -1;
        var door = false;
        var crossingRoom = -1;
        var queue = new List<(int X, int Y)> { (0, 0) };
        var visited = new bool[grid.Side * grid.Side];
        visited[grid.Index(0, 0)] = true;
        for (var head = 0; head < queue.Count; head++)
        {
            var current = queue[head];
            foreach (var step in Steps)
            {
                // Never a route that needs a retreat; the first step must make progress (radar_passage.cpp:57-60).
                var advance = axis.X * step.X + axis.Y * step.Y;
                if (advance < 0 || (head == 0 && advance <= 0)) continue;
                var next = (X: current.X + step.X, Y: current.Y + step.Y);
                var at = new RadarVector(next.X, next.Y);
                if (Math.Abs(axis.Across(at)) > query.LateralTolerance) continue;
                if (Math.Max(Math.Abs(next.X), Math.Abs(next.Y)) > query.Radius) continue; // the window only caps the reach
                var cell = grid.Cell(next.X, next.Y);
                var projection = axis.Dot(at);
                if (cell == RadarCell.Unknown)
                {
                    if (unknownDepth < 0 || projection < unknownDepth) unknownDepth = projection;
                    continue;
                }
                if (cell == RadarCell.Door && projection <= query.CloseDepth + MaxStep) door = true;
                if (cell != RadarCell.Open || (step.X != 0 && step.Y != 0 && Pinched(grid, current, step))) continue;
                var index = grid.Index(next.X, next.Y);
                if (visited[index]) continue;
                visited[index] = true;
                depth = Math.Max(depth, projection);
                var room = grid.Room(next.X, next.Y);
                if (home >= 0 && room >= 0 && room != home && (crossing < 0 || projection < crossing))
                {
                    crossing = projection;
                    crossingRoom = room;
                }
                queue.Add(next);
            }
        }

        var passage = new Passage
        {
            Depth = depth, OpenDepth = query.OpenDepth, CloseDepth = query.CloseDepth, Available = true,
            Crossing = crossing, CrossingRoom = crossingRoom, UnknownDepth = unknownDepth, Door = door,
        };
        return passage with { Observed = Classify(passage, query) };
    }

    private static RadarCueKind Classify(Passage passage, PassageQuery query)
    {
        if (passage.LeadsUnknown) return RadarCueKind.Exit;
        if (passage.Depth <= query.CloseDepth) return passage.Door ? RadarCueKind.Door : RadarCueKind.Blocked;
        if (passage.Depth < query.OpenDepth && query.Committed is { } committed && committed.IsObstacle()) return committed;
        // Another room entered and its border cleared by a doorway's width (radar_passage.cpp:93-106).
        var leaves = passage.Crossing >= 0 && passage.Depth >= passage.Crossing + query.WayThrough;
        return leaves ? RadarCueKind.Exit : RadarCueKind.Open;
    }

    // A diagonal step between two non-open cells is a zero-width gap; D2R's cross body could not take it either.
    private static bool Pinched(LocalGrid grid, (int X, int Y) from, (int X, int Y) step) =>
        grid.Cell(from.X + step.X, from.Y) != RadarCell.Open && grid.Cell(from.X, from.Y + step.Y) != RadarCell.Open;
}

internal static class RadarCueKindObstacle
{
    /// <summary>D2R Obstacle(state), radar.h:17.</summary>
    public static bool IsObstacle(this RadarCueKind kind) => kind is RadarCueKind.Blocked or RadarCueKind.Door;
}
