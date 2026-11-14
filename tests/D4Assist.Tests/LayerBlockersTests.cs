using System.Numerics;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class LayerBlockersTests
{
    private const int Cube = 1, Door = 2, Sphere = 3;
    private const string Quest = "W (Quest)", Game = "W (Game)";

    private static Matrix4x4 At(float x, float y, float z, float scale = 1) =>
        Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(x, y, z);

    private static ActorDefinitionInfo? Definition(int sno) =>
        new ActorDefinitionInfo(sno == Cube ? Conventions.ActorTypeServerProp : 2, 0, Vector3.Zero, new Vector3(0.5f));

    // A quest layer with a scaled cube, a door of its own and a trigger the base layer places too.
    private static IReadOnlyList<WorldLayer> Layers() => LayerBlockers.Layers(
        [
            (Quest, [(Cube, At(10, 20, 3, scale: 6)), (Door, At(40, 20, 0)), (Sphere, At(60, 20, 0))]),
            (Game, [(Sphere, At(60.4f, 20, 0)), (Door, At(80, 20, 0))]),
        ],
        sno => sno == Cube, Definition);

    private static ActorState Actor(int sno, float x, float y) => new(1, sno, x, y, 0);

    [Fact]
    public void A_layer_with_a_collision_prop_keeps_the_prop_as_scaled_and_the_actors_only_it_places()
    {
        var layer = Assert.Single(Layers());

        Assert.Equal(Quest, layer.Name);
        var wall = Assert.Single(layer.Walls);
        Assert.Equal(36, wall.Cells.Count); // 6 x 6 units round (10,20)
        Assert.Contains((7, 17), wall.Cells);
        Assert.Contains((12, 22), wall.Cells);
        Assert.Equal(0f, wall.Z, 3);
        Assert.Equal(6f, wall.Height, 3);
        Assert.Equal([Cube, Door], layer.Actors.Select(a => a.Sno)); // the trigger stands in the base layer too
    }

    [Fact]
    public void A_turned_prop_covers_the_cells_under_its_turned_box()
    {
        var turned = Matrix4x4.CreateScale(8, 2, 2) * Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0, 0);

        var wall = LayerBlockers.Footprint(Vector3.Zero, new Vector3(0.5f), turned)!;

        Assert.Equal(16, wall.Cells.Count); // 2 wide in X, 8 long in Y
        Assert.All(wall.Cells, c => Assert.InRange(c.X, -1, 0));
        Assert.Contains((0, 3), wall.Cells);
        Assert.Contains((-1, -4), wall.Cells);
    }

    [Theory]
    [InlineData("InvisibleCollision_Cube_ServerProp", Conventions.ActorTypeServerProp, true)]
    [InlineData("Hotfix_ServerProp_InvisCollision_8m", Conventions.ActorTypeServerProp, true)]
    [InlineData("MrHardpointServerPropNoCollision", Conventions.ActorTypeServerProp, false)]
    [InlineData("Frac_Buildings_Nevesk_Chapel_01_Collision", 5, false)]
    [InlineData("Ancients_Ruins_BrickPile_A_01_ServerProp", Conventions.ActorTypeServerProp, false)]
    public void A_blocker_is_a_server_prop_named_for_its_collision(string name, int type, bool blocker) =>
        Assert.Equal(blocker, LayerBlockers.IsBlocker(name, new ActorDefinitionInfo(type, 0)));

    [Fact]
    public async Task A_layer_stands_once_one_of_its_own_actors_is_listed()
    {
        var log = new List<string>();
        var blockers = new LayerBlockers(_ => Layers(), log.Add);
        await blockers.EnterWorld("W");
        int version = blockers.Version;

        blockers.Update([Actor(Sphere, 60, 20), Actor(Door, 80, 20)]); // the base layer's actors
        Assert.Empty(blockers.Walls());
        Assert.Equal(version, blockers.Version);

        blockers.Update([Actor(Door, 40.3f, 20)]);
        Assert.Single(blockers.Walls());
        Assert.NotEqual(version, blockers.Version);
        Assert.Equal(["LAYER on: W (Quest), 1 collision prop(s)"], log);
    }

    [Fact]
    public async Task A_quest_moving_on_drops_a_layer_with_no_actor_listed_and_keeps_one_listed()
    {
        var blockers = new LayerBlockers(_ => Layers());
        await blockers.EnterWorld("W");
        blockers.Update([Actor(Door, 40, 20)]);

        blockers.QuestMovedOn();
        Assert.Single(blockers.Walls());

        blockers.Update([]); // out of the client's range
        Assert.Single(blockers.Walls());
        int version = blockers.Version;
        blockers.QuestMovedOn();
        Assert.Empty(blockers.Walls());
        Assert.NotEqual(version, blockers.Version);

        blockers.Update([Actor(Cube, 10, 20)]);
        Assert.Single(blockers.Walls());
    }

    [Fact]
    public async Task A_new_world_starts_with_no_layer_standing()
    {
        var blockers = new LayerBlockers(world => world == "W" ? Layers() : []);
        await blockers.EnterWorld("W");
        blockers.Update([Actor(Door, 40, 20)]);

        await blockers.EnterWorld("Other");
        blockers.Update([Actor(Door, 40, 20)]);

        Assert.Empty(blockers.Walls());
    }
}
