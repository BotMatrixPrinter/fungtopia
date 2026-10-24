using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Travel.Tests;

public class TravelModuleTests
{
    private const double Step = 1.0 / 60;

    /// <summary>Fake of the shell's bus: records subscriptions and the messages Travel publishes.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<Type> Subscribed = new();
        public readonly List<MapState> States = new();
        public readonly List<TeleportPlayer> Teleports = new();
        public readonly List<UiFocus> Focus = new();
        public int OtherMessages;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscribed.Add(typeof(T));

        public void Publish<T>(in T message) where T : struct
        {
            if (message is MapState state)
                States.Add(state);
            else if (message is TeleportPlayer teleport)
                Teleports.Add(teleport);
            else if (message is UiFocus focus)
                Focus.Add(focus);
            else
                OtherMessages++;
        }
    }

    private sealed class Rig
    {
        public readonly FakeBus Bus = new();
        public readonly TravelModule Travel = new();
        private long _tick;
        public Vector3 Position;
        public bool Alive = true;

        public Rig(Vector3 position)
        {
            Position = position;
            Travel.Start(Bus);
        }

        /// <summary>
        /// One tick. <paramref name="move"/> goes into PlayerInput.Move; <paramref name="stick"/> is the
        /// view's held left stick (<see cref="TravelModule.Navigate"/>).
        /// </summary>
        public void Tick(bool interact = false, bool openMap = false, Vector2 move = default, Vector2 stick = default)
        {
            Travel.Navigate(stick);
            // Input and Player tick before Travel (modules tick in name order).
            Travel.Handle(new PlayerInput(move, Vector2.Zero, false, false, false, Vector3.Zero, interact, openMap));
            Travel.Handle(new PlayerState(Position, 0f, Vector3.Zero, true, 10, 10, Alive, CharacterKind.Girl));
            Travel.Tick(new TickContext(_tick++, Step));
        }

        public void Wait(float seconds)
        {
            for (int i = 0; i < (int)MathF.Ceiling(seconds * 60f) + 1; i++)
                Tick();
        }

        public bool LastOpen => Bus.States[^1].Open;

        /// <summary>Whether Input would see the map's owner open, from the UiFocus messages so far.</summary>
        public bool Focused => Bus.Focus.Count > 0 && Bus.Focus[^1].Open;
    }

    private static Vector3 Near(Place signpost, float dx = 1.2f, float dz = 0.8f) =>
        signpost.Position + new Vector3(dx, 0f, dz);

    private static readonly Vector3 TownSquare = new(0f, 0f, 230f);
    private static readonly Vector3 DeepForest = new(30f, 0f, 20f);

    public static IEnumerable<object[]> AllSignposts()
    {
        for (int i = 0; i < 4; i++)
            yield return new object[] { i };
    }

    [Fact]
    public void SubscribesToInputAndPlayerState()
    {
        var rig = new Rig(TownSquare);
        Assert.Contains(typeof(PlayerInput), rig.Bus.Subscribed);
        Assert.Contains(typeof(PlayerState), rig.Bus.Subscribed);
    }

    [Fact]
    public void SignpostsAreTheMapsFourSignposts()
    {
        var expected = new[]
        {
            Places.SignpostTown, Places.SignpostForestSouth, Places.SignpostForestNorth, Places.SignpostPetCity,
        };
        Assert.Equal(expected.Length, TravelModule.Signposts.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Position, TravelModule.Signposts[i].Position);
            Assert.Equal(expected[i].Yaw, TravelModule.Signposts[i].Yaw);
        }
    }

    [Fact]
    public void PublishesAClosedMapStateEveryTick()
    {
        var rig = new Rig(TownSquare);
        for (int i = 0; i < 5; i++)
            rig.Tick();
        Assert.Equal(5, rig.Bus.States.Count);
        Assert.All(rig.Bus.States, s => Assert.False(s.Open));
        Assert.Empty(rig.Bus.Teleports);
        Assert.Equal(0, rig.Bus.OtherMessages);
    }

    [Theory]
    [MemberData(nameof(AllSignposts))]
    public void PromptShowsWithinTwoAndAHalfMetersOfEachSignpost(int index)
    {
        var signpost = TravelModule.Signposts[index];
        var rig = new Rig(Near(signpost, 2.0f, 1.0f)); // 2.24 m away
        rig.Tick();
        Assert.True(rig.Travel.ShowPrompt);
        Assert.Equal(index, rig.Travel.NearSignpost);
        Assert.Equal("E / X: Map", TravelModule.PromptText);

        rig.Position = Near(signpost, 2.2f, 1.4f); // 2.61 m away
        rig.Tick();
        Assert.False(rig.Travel.ShowPrompt);
        Assert.Equal(-1, rig.Travel.NearSignpost);
    }

    [Fact]
    public void NoPromptAwayFromSignposts()
    {
        var rig = new Rig(TownSquare);
        rig.Tick();
        Assert.False(rig.Travel.ShowPrompt);
        Assert.Equal(-1, rig.Travel.NearSignpost);
    }

    [Fact]
    public void PromptHidesWhileTheMapIsOpen()
    {
        var rig = new Rig(Near(Places.SignpostTown));
        rig.Tick(interact: true);
        Assert.True(rig.Travel.Open);
        Assert.False(rig.Travel.ShowPrompt);
    }

    [Theory]
    [MemberData(nameof(AllSignposts))]
    public void InteractNearASignpostOpensTheMap(int index)
    {
        var rig = new Rig(Near(TravelModule.Signposts[index]));
        rig.Tick();
        Assert.False(rig.LastOpen);
        rig.Tick(interact: true);
        Assert.True(rig.Travel.Open);
        Assert.True(rig.LastOpen);
        rig.Tick();
        Assert.True(rig.LastOpen);
        Assert.Empty(rig.Bus.Teleports);
    }

    [Fact]
    public void InteractAwayFromSignpostsDoesNotOpenTheMap()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(interact: true);
        Assert.False(rig.Travel.Open);
        Assert.False(rig.LastOpen);
    }

    [Fact]
    public void OpenMapOpensTheMapAnywhereOutdoors()
    {
        foreach (var where in new[] { TownSquare, DeepForest, new Vector3(0f, 0f, -220f), new Vector3(0f, 0f, 125f) })
        {
            var rig = new Rig(where);
            rig.Tick(openMap: true);
            Assert.True(rig.LastOpen);
        }
    }

    [Fact]
    public void TheMapIsNotAvailableInsideTheHouse()
    {
        var rig = new Rig(Places.HomeInterior.Position);
        rig.Tick(openMap: true);
        rig.Tick(interact: true);
        Assert.False(rig.Travel.Open);
        Assert.False(rig.Travel.ShowPrompt);
        Assert.All(rig.Bus.States, s => Assert.False(s.Open));
    }

    [Fact]
    public void TheMapDoesNotOpenAfterDefeat()
    {
        var rig = new Rig(Near(Places.SignpostTown)) { Alive = false };
        rig.Tick(interact: true);
        rig.Tick(openMap: true);
        Assert.False(rig.Travel.Open);
        Assert.False(rig.Travel.ShowPrompt);
    }

    [Fact]
    public void EscOrBClosesTheMapWithoutTeleporting()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        rig.Travel.Close();
        rig.Tick();
        Assert.False(rig.Travel.Open);
        Assert.False(rig.LastOpen);
        rig.Wait(1f);
        Assert.Empty(rig.Bus.Teleports);
    }

    [Fact]
    public void OpenMapAgainClosesTheMap()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        rig.Tick(openMap: true);
        Assert.False(rig.LastOpen);
        rig.Wait(1f);
        Assert.Empty(rig.Bus.Teleports);
    }

    [Fact]
    public void CloseWhileClosedIsIgnored()
    {
        var rig = new Rig(TownSquare);
        rig.Travel.Close();
        rig.Tick();
        rig.Tick(openMap: true);
        Assert.True(rig.LastOpen);
    }

    [Fact]
    public void StaysOpenAndSendsNoTeleportUntilAPlaceIsChosen()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        rig.Wait(5f);
        Assert.True(rig.Travel.Open);
        Assert.All(rig.Bus.States, s => Assert.True(s.Open));
        Assert.Empty(rig.Bus.Teleports);
    }

    [Theory]
    [InlineData(0f, 230f, 0)]
    [InlineData(30f, 20f, 1)]
    [InlineData(0f, -220f, 2)]
    [InlineData(0f, 140f, 0)]
    [InlineData(0f, 112f, 1)]
    [InlineData(0f, -115f, 1)]
    [InlineData(0f, -145f, 2)]
    public void YouAreHereIsTheNearestPlace(float x, float z, int expected)
    {
        var rig = new Rig(new Vector3(x, 0f, z));
        rig.Tick(openMap: true);
        Assert.Equal(expected, rig.Travel.Here);
        Assert.Equal(expected, rig.Travel.Selected);
    }

    [Theory]
    [InlineData(0, Area.Town)]
    [InlineData(1, Area.Forest)]
    [InlineData(2, Area.PetCity)]
    public void ClickingAPlaceTeleportsToItsSignpostAfterAShortFade(int index, Area area)
    {
        var rig = new Rig(DeepForest);
        rig.Tick(openMap: true);
        rig.Travel.Pick(index);
        rig.Tick();
        // The fade to black starts; the map still shows and nothing moves yet.
        Assert.Empty(rig.Bus.Teleports);
        Assert.Equal(index, rig.Travel.Traveling);
        Assert.True(rig.LastOpen);
        rig.Tick();
        Assert.Empty(rig.Bus.Teleports);
        Assert.True(rig.Travel.Fade > 0f && rig.Travel.Fade < 1f);

        int ticks = 1;
        while (rig.Bus.Teleports.Count == 0 && ticks < 120)
        {
            rig.Tick();
            ticks++;
        }
        Assert.Single(rig.Bus.Teleports);
        Assert.InRange(ticks / 60f, TravelModule.FadeOutSeconds - 0.02f, TravelModule.FadeOutSeconds + 0.02f);
        Assert.False(rig.LastOpen);
        Assert.Equal(1f, rig.Travel.Fade);

        var signpost = area == Area.Forest ? Places.SignpostForestSouth : Places.Signpost(area);
        var teleport = rig.Bus.Teleports[0];
        Assert.Equal(signpost.Yaw, teleport.Facing);
        Assert.Equal(TravelModule.ArrivalPoint(area), teleport.Position);
        var offset = teleport.Position - signpost.Position;
        Assert.InRange(new Vector2(offset.X, offset.Z).Length(), 1f, 2f);
        Assert.Equal(Areas.At(signpost.Position.X, signpost.Position.Z), Areas.At(teleport.Position.X, teleport.Position.Z));

        // Then it fades back in and stays closed: one teleport only.
        rig.Wait(TravelModule.FadeInSeconds);
        Assert.Equal(0f, rig.Travel.Fade);
        Assert.Equal(-1, rig.Travel.Traveling);
        rig.Wait(1f);
        Assert.Single(rig.Bus.Teleports);
        Assert.False(rig.LastOpen);
    }

    [Fact]
    public void TheForestDestinationIsItsSouthEntrance()
    {
        Assert.Equal(Places.SignpostForestSouth.Position, TravelModule.Destination(Area.Forest).Position);
        Assert.Equal(Places.SignpostTown.Position, TravelModule.Destination(Area.Town).Position);
        Assert.Equal(Places.SignpostPetCity.Position, TravelModule.Destination(Area.PetCity).Position);
    }

    [Theory]
    [InlineData(Area.Town)]
    [InlineData(Area.Forest)]
    [InlineData(Area.PetCity)]
    public void ArrivalPointsAreFreeGroundInFrontOfTheSignpost(Area area)
    {
        var p = TravelModule.ArrivalPoint(area);
        Assert.Equal(Ground.HeightAt(p.X, p.Z), p.Y);
        Assert.True(Areas.Walkable(p.X, p.Z, 0.4f));
        Assert.False(Ground.Blocks(p.X, p.Z, 0.4f));
        var place = TravelModule.Destination(area);
        var ahead = p - place.Position;
        Assert.True(Vector3.Dot(new Vector3(ahead.X, 0f, ahead.Z), place.Facing) > 1f);
    }

    [Fact]
    public void StickAndArrowsMoveTheSelectionThenInteractPicksIt()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        Assert.Equal(0, rig.Travel.Selected);

        // One push right is one step, however long it is held.
        for (int i = 0; i < 20; i++)
            rig.Tick(stick: new Vector2(1f, 0f));
        Assert.Equal(1, rig.Travel.Selected);
        rig.Tick();
        rig.Tick(stick: new Vector2(0f, 0.9f));
        Assert.Equal(2, rig.Travel.Selected);
        rig.Tick();
        rig.Tick(stick: new Vector2(1f, 0f));
        Assert.Equal(2, rig.Travel.Selected); // stays on the last one
        rig.Tick();
        rig.Tick(stick: new Vector2(-0.3f, 0f)); // under StickStep: no step
        Assert.Equal(2, rig.Travel.Selected);
        rig.Tick(stick: new Vector2(0f, -1f));
        Assert.Equal(1, rig.Travel.Selected);
        rig.Travel.Step(-1);
        rig.Tick();
        Assert.Equal(0, rig.Travel.Selected);
        rig.Travel.Step(1);
        rig.Tick();
        Assert.Equal(1, rig.Travel.Selected);
        Assert.Empty(rig.Bus.Teleports);

        rig.Tick(interact: true);
        rig.Wait(TravelModule.FadeOutSeconds);
        Assert.Single(rig.Bus.Teleports);
        Assert.Equal(TravelModule.ArrivalPoint(Area.Forest), rig.Bus.Teleports[0].Position);
    }

    [Fact]
    public void PlayerInputMoveDoesNotStepTheSelection()
    {
        // Input zeroes Move while the map holds UiFocus; the map's choosing comes from the view.
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        rig.Tick(move: new Vector2(1f, 0f));
        rig.Tick();
        rig.Tick(move: new Vector2(0f, 1f));
        Assert.Equal(0, rig.Travel.Selected);
    }

    [Fact]
    public void AStickHeldWhenTheMapOpensDoesNotStep()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(stick: new Vector2(1f, 0f));
        rig.Tick(openMap: true, stick: new Vector2(1f, 0f));
        rig.Tick(stick: new Vector2(1f, 0f));
        Assert.Equal(0, rig.Travel.Selected);
        rig.Tick();
        rig.Tick(stick: new Vector2(1f, 0f));
        Assert.Equal(1, rig.Travel.Selected);
    }

    [Fact]
    public void ConfirmPicksTheSelectedPlace()
    {
        var rig = new Rig(TownSquare);
        rig.Travel.Confirm(); // ignored while closed
        rig.Tick();
        rig.Tick(openMap: true);
        Assert.True(rig.Travel.Open);
        Assert.Equal(-1, rig.Travel.Traveling);
        rig.Travel.Step(2);
        rig.Tick();
        rig.Travel.Confirm();
        rig.Tick();
        Assert.Equal(2, rig.Travel.Traveling);
        rig.Wait(TravelModule.FadeOutSeconds);
        Assert.Single(rig.Bus.Teleports);
        Assert.Equal(TravelModule.ArrivalPoint(Area.PetCity), rig.Bus.Teleports[0].Position);
    }

    [Fact]
    public void HoldsUiFocusWhileTheMapIsOpen()
    {
        var rig = new Rig(Near(Places.SignpostTown));
        rig.Wait(0.5f);
        Assert.Empty(rig.Bus.Focus); // nothing while closed

        rig.Tick(interact: true);
        Assert.Single(rig.Bus.Focus);
        Assert.Equal(TravelModule.FocusOwner, rig.Bus.Focus[0].Owner);
        Assert.True(rig.Bus.Focus[0].Open);
        rig.Wait(2f);
        Assert.Single(rig.Bus.Focus); // published once, not every tick
        Assert.True(rig.Focused);

        rig.Travel.Close();
        rig.Tick();
        Assert.Equal(2, rig.Bus.Focus.Count);
        Assert.Equal(TravelModule.FocusOwner, rig.Bus.Focus[1].Owner);
        Assert.False(rig.Focused);
        rig.Wait(1f);
        Assert.Equal(2, rig.Bus.Focus.Count);
    }

    [Fact]
    public void OpenMapAgainReleasesUiFocus()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        Assert.True(rig.Focused);
        rig.Tick(openMap: true);
        Assert.False(rig.Focused);
        Assert.Equal(2, rig.Bus.Focus.Count);
        rig.Tick(openMap: true);
        Assert.True(rig.Focused);
        Assert.Equal(3, rig.Bus.Focus.Count);
    }

    [Fact]
    public void GoingIndoorsReleasesUiFocus()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        rig.Position = Places.HomeInterior.Position;
        rig.Tick();
        Assert.False(rig.Travel.Open);
        Assert.False(rig.Focused);
        Assert.Equal(2, rig.Bus.Focus.Count);
    }

    [Fact]
    public void TravelReleasesUiFocusWhenTheMapClosesAtTheTeleport()
    {
        var rig = new Rig(DeepForest);
        rig.Tick(openMap: true);
        rig.Travel.Pick(0);
        rig.Tick();
        // Still held through the fade to black, while the map shows.
        while (rig.Bus.Teleports.Count == 0)
        {
            Assert.True(rig.Focused);
            rig.Tick();
        }
        Assert.False(rig.Focused);
        Assert.Equal(2, rig.Bus.Focus.Count);
        rig.Wait(TravelModule.FadeInSeconds + 1f);
        Assert.Equal(2, rig.Bus.Focus.Count);
        Assert.False(rig.Focused);
    }

    [Fact]
    public void TheInteractThatOpensTheMapDoesNotPick()
    {
        var rig = new Rig(Near(Places.SignpostPetCity));
        rig.Tick(interact: true);
        rig.Wait(1f);
        Assert.True(rig.Travel.Open);
        Assert.Empty(rig.Bus.Teleports);
        Assert.Equal(2, rig.Travel.Selected);

        rig.Travel.Step(-2);
        rig.Tick();
        rig.Tick(interact: true);
        rig.Wait(TravelModule.FadeOutSeconds);
        Assert.Single(rig.Bus.Teleports);
        Assert.Equal(TravelModule.ArrivalPoint(Area.Town), rig.Bus.Teleports[0].Position);
    }

    [Fact]
    public void HoverSelectsAndViewInputIsIgnoredWhileClosed()
    {
        var rig = new Rig(TownSquare);
        rig.Travel.Pick(2);
        rig.Travel.Step(1);
        rig.Tick();
        Assert.False(rig.Travel.Open);
        rig.Wait(1f);
        Assert.Empty(rig.Bus.Teleports);

        rig.Tick(openMap: true);
        Assert.Equal(0, rig.Travel.Selected);
        rig.Travel.Hover(2);
        rig.Tick();
        Assert.Equal(2, rig.Travel.Selected);
        // A resting mouse does not hold the selection against the keys.
        rig.Travel.Step(-1);
        rig.Tick();
        Assert.Equal(1, rig.Travel.Selected);
    }

    [Fact]
    public void InputDuringTheFadeIsIgnored()
    {
        var rig = new Rig(TownSquare);
        rig.Tick(openMap: true);
        rig.Travel.Pick(2);
        rig.Tick();
        rig.Travel.Close();
        rig.Travel.Pick(1);
        rig.Tick(interact: true, openMap: true);
        rig.Wait(TravelModule.FadeOutSeconds + TravelModule.FadeInSeconds);
        Assert.Single(rig.Bus.Teleports);
        Assert.Equal(TravelModule.ArrivalPoint(Area.PetCity), rig.Bus.Teleports[0].Position);
        Assert.False(rig.Travel.Open);
    }

    [Fact]
    public void MapLayoutHitsTheThreeCircles()
    {
        Assert.Equal(3, MapLayout.Centers.Length);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(i, MapLayout.Hit(MapLayout.Centers[i]));
            Assert.Equal(i, MapLayout.Hit(MapLayout.Centers[i] + new Vector2(MapLayout.Radius * 0.9f, 0f)));
        }
        Assert.Equal(-1, MapLayout.Hit(new Vector2(20f, 20f)));
        Assert.Equal("Mushroom Town", MapLayout.Name(Area.Town));
        Assert.Equal("Mushroom Forest", MapLayout.Name(Area.Forest));
        Assert.Equal("Pet City", MapLayout.Name(Area.PetCity));
        // Circles do not overlap and fit the canvas.
        for (int i = 0; i < 3; i++)
        {
            var c = MapLayout.Centers[i];
            Assert.InRange(c.X - MapLayout.Radius, 0f, MapLayout.Width);
            Assert.InRange(c.X + MapLayout.Radius, 0f, MapLayout.Width);
            for (int j = i + 1; j < 3; j++)
                Assert.True(Vector2.Distance(c, MapLayout.Centers[j]) > 2f * MapLayout.Radius);
        }
    }

    [Fact]
    public void ScreenClicksMapToTheCanvas()
    {
        var screen = new Vector2(1280f, 720f);
        var center = MapLayout.Centers[2] * MapLayout.Scale(screen) + MapLayout.Origin(screen);
        Assert.Equal(2, MapLayout.Hit(MapLayout.ToCanvas(center, screen)));
        // A wider screen letterboxes the canvas in the middle.
        var wide = new Vector2(2560f, 1080f);
        var left = MapLayout.Centers[0] + MapLayout.Origin(wide);
        Assert.Equal(0, MapLayout.Hit(MapLayout.ToCanvas(left, wide)));
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new TravelModule().Tick(new TickContext(0, Step)));
    }
}
