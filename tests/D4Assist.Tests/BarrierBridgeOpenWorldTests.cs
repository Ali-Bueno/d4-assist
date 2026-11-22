using System.Numerics;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public sealed class BarrierBridgeOpenWorldTests
{
    private const int TableSno = 1;
    private static readonly ActorDefinitionInfo TableInfo =
        new(2, Conventions.GizmoBreakable, PropCenter: new Vector3(0, 0, 0.8f), PropHalfExtents: new Vector3(0.7f, 1.2f, 0.8f));
    private static readonly ActorState Table = new(7, TableSno, 11.5f, 28.5f, 0);

    private sealed class OpenWorldFeed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged { add { } remove { } }
        public event Action<IReadOnlyList<TileState>>? TilesUpdated { add { } remove { } }
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
        public (string Name, bool IsDungeon)? CurrentWorld => ("Sanctuary_Eastern_Continent", false);
        public WorldKind CurrentWorldKind => WorldKind.Overworld;
        public void Poll(params ActorState[] actors)
        {
            PlayerMoved?.Invoke(new PlayerState(5, 28, 0, 0));
            ActorsUpdated?.Invoke(actors);
        }
    }

    [Fact]
    public void Open_world_props_are_tracked_only_while_the_guide_leads_and_forgotten_when_it_stops()
    {
        var feed = new OpenWorldFeed();
        var props = new BreakableProps(sno => sno == TableSno ? "Prop_Table_Arrangement" : null, sno => sno == TableSno ? TableInfo : null);
        bool guiding = false;
        _ = new BarrierBridge(feed, null, null, _ => { }, _ => { }, openWorldProps: props, openWorldPropsWanted: () => guiding);

        feed.Poll(Table);
        Assert.Empty(props.Walls());

        guiding = true;
        feed.Poll(Table);
        Assert.NotEmpty(props.Walls());

        guiding = false;
        feed.Poll(Table);
        Assert.Empty(props.Walls());
    }
}
