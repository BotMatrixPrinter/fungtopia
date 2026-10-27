using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Input.Tests;

public class InputModuleTests
{
    private const double Step = 1.0 / 60;

    /// <summary>
    /// Fake of the shell's bus: records every PlayerInput and ActiveDevice published and delivers
    /// UiFocus and InteractAvailable to their subscribers.
    /// </summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<PlayerInput> Published = new();
        public readonly List<ActiveDevice> Devices = new();
        public readonly List<MenuNav> MenuNavs = new();
        public readonly List<IMessageHandler<UiFocus>> FocusHandlers = new();
        public readonly List<IMessageHandler<InteractAvailable>> AvailableHandlers = new();
        public int OtherMessages;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (handler is IMessageHandler<UiFocus> focus && typeof(T) == typeof(UiFocus))
                FocusHandlers.Add(focus);
            else if (handler is IMessageHandler<InteractAvailable> available && typeof(T) == typeof(InteractAvailable))
                AvailableHandlers.Add(available);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is PlayerInput input)
                Published.Add(input);
            else if (message is ActiveDevice device)
                Devices.Add(device);
            else if (message is MenuNav nav)
                MenuNavs.Add(nav);
            else if (message is UiFocus focus)
                foreach (var handler in FocusHandlers)
                    handler.Handle(focus);
            else if (message is InteractAvailable available)
                foreach (var handler in AvailableHandlers)
                    handler.Handle(available);
            else
                OtherMessages++;
        }

        public void Focus(int owner, bool open) => Publish(new UiFocus(owner, open));

        public void Available(int owner, bool available) => Publish(new InteractAvailable(owner, available));
    }

    private static InputModule Started(FakeBus bus)
    {
        var input = new InputModule();
        input.Start(bus);
        return input;
    }

    private static PlayerInput TickOnce(InputModule input, FakeBus bus, long tick = 0)
    {
        input.Tick(new TickContext(tick, Step));
        return bus.Published[^1];
    }

    private static void AssertNear(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 1e-5f, $"expected {expected}, got {actual}");

    private static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-5f, $"expected {expected}, got {actual}");

    // --- one message per tick ---

    [Fact]
    public void PublishesExactlyOnePlayerInputActiveDeviceAndMenuNavPerTick()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.Empty(bus.Published);
        Assert.Empty(bus.Devices);
        Assert.Empty(bus.MenuNavs);
        for (int i = 0; i < 5; i++)
            input.Tick(new TickContext(i, Step));
        Assert.Equal(5, bus.Published.Count);
        Assert.Equal(5, bus.Devices.Count);
        Assert.Equal(5, bus.MenuNavs.Count);
        Assert.Equal(0, bus.OtherMessages);
    }

    [Fact]
    public void PublishesEvenWithNoDeviceStateAsAllZero()
    {
        var bus = new FakeBus();
        var msg = TickOnce(Started(bus), bus);
        Assert.Equal(Vector2.Zero, msg.Move);
        Assert.Equal(Vector2.Zero, msg.Look);
        Assert.False(msg.Jump);
        Assert.False(msg.Fire);
        Assert.False(msg.Interact);
        Assert.False(msg.OpenMap);
        Assert.False(msg.HasAim);
        Assert.Equal(Vector3.Zero, msg.AimDirection);
        Assert.Equal(Vector3.Zero, msg.AimOrigin);
        Assert.False(msg.Eat);
        Assert.Equal(-1, msg.FoodSelect);
        Assert.Equal(0, msg.FoodCycle);
    }

    [Fact]
    public void LatestMatchesThePublishedMessage()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetKeyboardMove(new Vector2(1, 0));
        input.SetFireButton(true);
        var msg = TickOnce(input, bus);
        Assert.Equal(msg.Move, input.Latest.Move);
        Assert.Equal(msg.Fire, input.Latest.Fire);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new InputModule().Tick(new TickContext(0, Step)));
    }

    // --- move values and normalization ---

    [Fact]
    public void KeyboardForwardIsPlusY()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetKeyboardMove(new Vector2(0, 1));
        AssertNear(new Vector2(0, 1), TickOnce(input, bus).Move);
    }

    [Fact]
    public void KeyboardDiagonalIsNormalizedToLengthOne()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetKeyboardMove(new Vector2(1, 1));
        var move = TickOnce(input, bus).Move;
        Assert.Equal(1f, move.Length(), 5);
        AssertNear(Vector2.Normalize(new Vector2(1, 1)), move);
    }

    [Fact]
    public void StickLongerThanOneIsClampedToOne()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetMoveStick(new Vector2(1, 1));
        var move = TickOnce(input, bus).Move;
        Assert.Equal(1f, move.Length(), 5);
        Assert.True(move.X > 0 && move.Y > 0);
    }

    [Fact]
    public void PartialStickKeepsAnAnalogLengthBelowOne()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetMoveStick(new Vector2(0, 0.6f));
        var move = TickOnce(input, bus).Move;
        // Dead zone 0.2: (0.6 - 0.2) / 0.8 = 0.5.
        AssertNear(new Vector2(0, 0.5f), move);
    }

    [Fact]
    public void StickInsideTheDeadZoneDoesNotMove()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetMoveStick(new Vector2(0.1f, -0.1f));
        Assert.Equal(Vector2.Zero, TickOnce(input, bus).Move);
    }

    [Fact]
    public void KeyboardWinsOverStickWhenBothAreUsed()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetMoveStick(new Vector2(0, 1));
        input.SetKeyboardMove(new Vector2(-1, 0));
        AssertNear(new Vector2(-1, 0), TickOnce(input, bus).Move);
        input.SetKeyboardMove(Vector2.Zero);
        AssertNear(new Vector2(0, 1), TickOnce(input, bus, 1).Move);
    }

    [Fact]
    public void MoveIsHeldStateAndRepeatsUntilChanged()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetKeyboardMove(new Vector2(0, -1));
        TickOnce(input, bus);
        AssertNear(new Vector2(0, -1), TickOnce(input, bus, 1).Move);
    }

    [Fact]
    public void NonFiniteMoveIsIgnored()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetMoveStick(new Vector2(float.NaN, 1));
        Assert.Equal(Vector2.Zero, TickOnce(input, bus).Move);
    }

    [Theory]
    [InlineData(0.5f, 0f, 0.2f, 0.375f)]
    [InlineData(1f, 0f, 0.2f, 1f)]
    [InlineData(0.2f, 0f, 0.2f, 0f)]
    [InlineData(0.9f, 0f, 0f, 0.9f)]
    public void DeadZoneRescalesRadially(float x, float y, float deadZone, float expectedLength)
    {
        var v = InputModule.ApplyDeadZone(new Vector2(x, y), deadZone);
        Assert.Equal(expectedLength, v.Length(), 5);
        if (expectedLength > 0)
            AssertNear(Vector2.Normalize(new Vector2(x, y)), Vector2.Normalize(v));
    }

    // --- look ---

    [Fact]
    public void MouseMotionTurnsRightAndUpInRadians()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.MouseRadiansPerPixel = 0.01f;
        // Right 10 px and up 5 px (screen y grows downward).
        input.AddMouseMotion(new Vector2(10, -5));
        AssertNear(new Vector2(0.1f, 0.05f), TickOnce(input, bus).Look);
    }

    [Fact]
    public void MouseMotionBetweenTicksAddsUpAndIsUsedOnce()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.MouseRadiansPerPixel = 0.01f;
        input.AddMouseMotion(new Vector2(3, 0));
        input.AddMouseMotion(new Vector2(2, 4));
        AssertNear(new Vector2(0.05f, -0.04f), TickOnce(input, bus).Look);
        Assert.Equal(Vector2.Zero, TickOnce(input, bus, 1).Look);
    }

    [Fact]
    public void RightStickLooksAtSensitivityTimesStep()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.StickLookRadiansPerSecond = 2f;
        input.SetLookStick(new Vector2(1, 0));
        AssertNear(new Vector2(2f / 60, 0), TickOnce(input, bus).Look);
        // Held stick keeps turning every tick.
        AssertNear(new Vector2(2f / 60, 0), TickOnce(input, bus, 1).Look);
    }

    [Fact]
    public void RightStickInsideTheDeadZoneDoesNotLook()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.StickDeadZone = 0.25f;
        input.SetLookStick(new Vector2(0, 0.2f));
        Assert.Equal(Vector2.Zero, TickOnce(input, bus).Look);
    }

    // --- jump and fire ---

    [Fact]
    public void JumpIsTrueOnlyOnTheTickAfterThePress()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressJump();
        input.PressJump();
        Assert.True(TickOnce(input, bus).Jump);
        Assert.False(TickOnce(input, bus, 1).Jump);
    }

    [Fact]
    public void FireIsHeldUntilReleased()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetFireButton(true);
        Assert.True(TickOnce(input, bus).Fire);
        Assert.True(TickOnce(input, bus, 1).Fire);
        input.SetFireButton(false);
        Assert.False(TickOnce(input, bus, 2).Fire);
    }

    [Fact]
    public void TriggerFiresAtTheThreshold()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetFireTrigger(0.4f);
        Assert.False(TickOnce(input, bus).Fire);
        input.SetFireTrigger(0.8f);
        Assert.True(TickOnce(input, bus, 1).Fire);
    }

    // --- run ---

    [Fact]
    public void ShiftRunsWhileHeld()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.False(TickOnce(input, bus).Run);
        input.SetRunKey(true);
        Assert.True(TickOnce(input, bus, 1).Run);
        Assert.True(TickOnce(input, bus, 2).Run);
        input.SetRunKey(false);
        Assert.False(TickOnce(input, bus, 3).Run);
    }

    [Fact]
    public void LeftTriggerRunsPastTheFireThreshold()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetRunTrigger(input.TriggerThreshold - 0.1f);
        Assert.False(TickOnce(input, bus).Run);
        input.SetRunTrigger(input.TriggerThreshold);
        Assert.True(TickOnce(input, bus, 1).Run);
        input.SetRunTrigger(0.9f);
        var msg = TickOnce(input, bus, 2);
        Assert.True(msg.Run);
        Assert.False(msg.Fire);
        input.SetRunTrigger(0f);
        Assert.False(TickOnce(input, bus, 3).Run);
        input.SetRunTrigger(float.NaN);
        Assert.False(TickOnce(input, bus, 4).Run);
    }

    [Fact]
    public void RightTriggerFiresButDoesNotRun()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetFireTrigger(1f);
        var msg = TickOnce(input, bus);
        Assert.True(msg.Fire);
        Assert.False(msg.Run);
    }

    [Fact]
    public void OlderConstructorsLeaveRunFalse()
    {
        var aim = new Vector3(0, 0, -1);
        foreach (var msg in new[]
        {
            default(PlayerInput),
            new PlayerInput(Vector2.One, Vector2.One, true, true, true, aim),
            new PlayerInput(Vector2.One, Vector2.One, true, true, true, aim, true, true),
            new PlayerInput(Vector2.One, Vector2.One, true, true, true, aim, true, true, Vector3.One),
            new PlayerInput(Vector2.One, Vector2.One, true, true, true, aim, true, true, Vector3.One, true, 1, 1),
        })
            Assert.False(msg.Run);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FullConstructorSetsRun(bool run)
    {
        var msg = new PlayerInput(new Vector2(0, 1), Vector2.Zero, false, false, true, new Vector3(0, 0, -1),
            false, false, Vector3.One, true, 2, -1, run);
        Assert.Equal(run, msg.Run);
        Assert.True(msg.Eat);
        Assert.Equal(2, msg.FoodSelect);
        Assert.Equal(-1, msg.FoodCycle);
        Assert.Equal(Vector3.One, msg.AimOrigin);
    }

    [Fact]
    public void ViewReadsShiftAndTheLeftTriggerForRun()
    {
        var source = ViewSource();
        Assert.Contains("_input.SetRunKey(GodotInput.IsPhysicalKeyPressed(Godot.Key.Shift));", source);
        Assert.Contains("_input.SetRunTrigger(GodotInput.GetJoyAxis(Pad, JoyAxis.TriggerLeft));", source);
        Assert.Contains("_input.SetFireTrigger(GodotInput.GetJoyAxis(Pad, JoyAxis.TriggerRight));", source);
    }

    // --- interact and map ---

    [Fact]
    public void InteractIsTrueOnlyOnTheTickAfterThePress()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressInteract();
        input.PressInteract();
        var msg = TickOnce(input, bus);
        Assert.True(msg.Interact);
        Assert.False(msg.OpenMap);
        Assert.False(msg.Jump);
        Assert.False(TickOnce(input, bus, 1).Interact);
    }

    [Fact]
    public void OpenMapIsTrueOnlyOnTheTickAfterThePress()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressOpenMap();
        var msg = TickOnce(input, bus);
        Assert.True(msg.OpenMap);
        Assert.False(msg.Interact);
        Assert.False(msg.Jump);
        Assert.False(TickOnce(input, bus, 1).OpenMap);
    }

    [Fact]
    public void LatestCarriesInteractAndOpenMap()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressInteract();
        input.PressOpenMap();
        TickOnce(input, bus);
        Assert.True(input.Latest.Interact);
        Assert.True(input.Latest.OpenMap);
    }

    [Fact]
    public void OldConstructorLeavesInteractAndOpenMapFalse()
    {
        var msg = new PlayerInput(new Vector2(0, 1), Vector2.Zero, true, true, true, new Vector3(0, 0, -1));
        Assert.True(msg.Jump);
        Assert.True(msg.Fire);
        Assert.False(msg.Interact);
        Assert.False(msg.OpenMap);
    }

    [Fact]
    public void FullConstructorSetsInteractAndOpenMap()
    {
        var msg = new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero, interact: true, openMap: false);
        Assert.True(msg.Interact);
        Assert.False(msg.OpenMap);
        msg = new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero, interact: false, openMap: true);
        Assert.False(msg.Interact);
        Assert.True(msg.OpenMap);
    }

    // --- eat and food selection ---

    [Fact]
    public void EatIsTrueOnlyOnTheTickAfterThePress()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressEat();
        input.PressEat();
        var msg = TickOnce(input, bus);
        Assert.True(msg.Eat);
        Assert.False(msg.Jump);
        Assert.False(msg.Interact);
        Assert.Equal(-1, msg.FoodSelect);
        Assert.Equal(0, msg.FoodCycle);
        Assert.True(input.Latest.Eat);
        Assert.False(TickOnce(input, bus, 1).Eat);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FoodSelectReportsThePressedSlotForOneTick(int slot)
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressFoodSelect(slot);
        var msg = TickOnce(input, bus);
        Assert.Equal(slot, msg.FoodSelect);
        Assert.Equal(slot, input.Latest.FoodSelect);
        Assert.False(msg.Eat);
        Assert.Equal(0, msg.FoodCycle);
        Assert.Equal(-1, TickOnce(input, bus, 1).FoodSelect);
    }

    [Fact]
    public void LastFoodSlotPressedBetweenTicksWins()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressFoodSelect(2);
        input.PressFoodSelect(0);
        Assert.Equal(0, TickOnce(input, bus).FoodSelect);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void FoodSelectOutsideTheThreeSlotsIsIgnored(int slot)
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressFoodSelect(1);
        input.PressFoodSelect(slot);
        Assert.Equal(1, TickOnce(input, bus).FoodSelect);
        input.PressFoodSelect(slot);
        Assert.Equal(-1, TickOnce(input, bus, 1).FoodSelect);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void FoodCycleReportsTheDirectionForOneTick(int direction)
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressFoodCycle(direction);
        var msg = TickOnce(input, bus);
        Assert.Equal(direction, msg.FoodCycle);
        Assert.Equal(direction, input.Latest.FoodCycle);
        Assert.Equal(-1, msg.FoodSelect);
        Assert.False(msg.Eat);
        Assert.Equal(0, TickOnce(input, bus, 1).FoodCycle);
    }

    [Fact]
    public void FoodCyclePressesBetweenTicksAddUpToTheirSign()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressFoodCycle(1);
        input.PressFoodCycle(1);
        Assert.Equal(1, TickOnce(input, bus).FoodCycle);
        input.PressFoodCycle(-1);
        input.PressFoodCycle(1);
        Assert.Equal(0, TickOnce(input, bus, 1).FoodCycle);
        input.PressFoodCycle(-5);
        Assert.Equal(-1, TickOnce(input, bus, 2).FoodCycle);
    }

    [Fact]
    public void OlderConstructorsLeaveEatOffAndNoFoodSelected()
    {
        var aim = new Vector3(0, 0, -1);
        foreach (var msg in new[]
        {
            default(PlayerInput),
            new PlayerInput(Vector2.Zero, Vector2.Zero, true, true, true, aim),
            new PlayerInput(Vector2.Zero, Vector2.Zero, true, true, true, aim, true, true),
            new PlayerInput(Vector2.Zero, Vector2.Zero, true, true, true, aim, true, true, Vector3.One),
        })
        {
            Assert.False(msg.Eat);
            Assert.Equal(-1, msg.FoodSelect);
            Assert.Equal(0, msg.FoodCycle);
        }
    }

    [Theory]
    [InlineData(true, 0, -1, true, 0, -1)]
    [InlineData(false, 2, 1, false, 2, 1)]
    [InlineData(false, 3, 4, false, -1, 1)]
    [InlineData(true, -7, -3, true, -1, -1)]
    public void FullConstructorSetsEatAndFood(bool eat, int select, int cycle, bool expectEat, int expectSelect, int expectCycle)
    {
        var msg = new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, true, new Vector3(0, 0, -1),
            false, false, Vector3.One, eat, select, cycle);
        Assert.Equal(expectEat, msg.Eat);
        Assert.Equal(expectSelect, msg.FoodSelect);
        Assert.Equal(expectCycle, msg.FoodCycle);
        Assert.Equal(Vector3.One, msg.AimOrigin);
    }

    // --- aim ---

    [Fact]
    public void AimIsNormalizedAndHasAimIsSet()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetAim(new Vector3(1, 2, 3), new Vector3(0, 0, -5));
        var msg = TickOnce(input, bus);
        Assert.True(msg.HasAim);
        AssertNear(new Vector3(0, 0, -1), msg.AimDirection);
    }

    [Fact]
    public void AimOriginIsTheCameraPositionPublishedWithTheDirection()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetAim(new Vector3(4, 1.5f, -2), new Vector3(0, 0, -5));
        var msg = TickOnce(input, bus);
        Assert.True(msg.HasAim);
        Assert.Equal(new Vector3(4, 1.5f, -2), msg.AimOrigin);
        Assert.Equal(msg.AimOrigin, input.Latest.AimOrigin);

        input.SetAim(new Vector3(-1, 3, 7), new Vector3(1, 0, 0));
        msg = TickOnce(input, bus, 1);
        Assert.Equal(new Vector3(-1, 3, 7), msg.AimOrigin);
        AssertNear(new Vector3(1, 0, 0), msg.AimDirection);
    }

    [Fact]
    public void AimStaysUntilChanged()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetAim(new Vector3(5, 6, 7), new Vector3(3, 0, 4));
        TickOnce(input, bus);
        var msg = TickOnce(input, bus, 1);
        Assert.True(msg.HasAim);
        AssertNear(new Vector3(0.6f, 0, 0.8f), msg.AimDirection);
        Assert.Equal(new Vector3(5, 6, 7), msg.AimOrigin);
    }

    [Fact]
    public void ClearAimWithoutCameraTurnsHasAimOff()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetAim(new Vector3(1, 2, 3), new Vector3(0, 0, -1));
        TickOnce(input, bus);
        input.ClearAim();
        var msg = TickOnce(input, bus, 1);
        Assert.False(msg.HasAim);
        Assert.Equal(Vector3.Zero, msg.AimDirection);
        Assert.Equal(Vector3.Zero, msg.AimOrigin);
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(float.NaN, 0f, -1f)]
    [InlineData(float.PositiveInfinity, 0f, 0f)]
    public void InvalidAimMeansNoAim(float x, float y, float z)
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetAim(new Vector3(1, 2, 3), new Vector3(0, 0, -1));
        input.SetAim(new Vector3(1, 2, 3), new Vector3(x, y, z));
        var msg = TickOnce(input, bus);
        Assert.False(msg.HasAim);
        Assert.Equal(Vector3.Zero, msg.AimDirection);
        Assert.Equal(Vector3.Zero, msg.AimOrigin);
    }

    [Theory]
    [InlineData(float.NaN, 0f, 0f)]
    [InlineData(0f, float.PositiveInfinity, 0f)]
    [InlineData(0f, 0f, float.NegativeInfinity)]
    public void InvalidAimOriginMeansNoAim(float x, float y, float z)
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetAim(new Vector3(1, 2, 3), new Vector3(0, 0, -1));
        input.SetAim(new Vector3(x, y, z), new Vector3(0, 0, -1));
        var msg = TickOnce(input, bus);
        Assert.False(msg.HasAim);
        Assert.Equal(Vector3.Zero, msg.AimDirection);
        Assert.Equal(Vector3.Zero, msg.AimOrigin);
    }

    [Fact]
    public void OlderConstructorsLeaveAimOriginZero()
    {
        var aim = new Vector3(0, 0, -1);
        Assert.Equal(Vector3.Zero, new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, true, aim).AimOrigin);
        Assert.Equal(Vector3.Zero, new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, true, aim, true, true).AimOrigin);
    }

    [Fact]
    public void ConstructorKeepsAimOriginOnlyWithAim()
    {
        var origin = new Vector3(1, 2, 3);
        var withAim = new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, true, new Vector3(0, 0, -1), false, false, origin);
        Assert.Equal(origin, withAim.AimOrigin);
        var noAim = new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero, false, false, origin);
        Assert.Equal(Vector3.Zero, noAim.AimOrigin);
    }

    // --- view ---

    private static string ViewSource([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(here)!, "..", "Godot", "InputView.cs"));

    [Fact]
    public void ViewDrawsNoCrosshairDot()
    {
        // The HUD owns the crosshair; InputView only reads devices and casts the aim ray.
        var source = ViewSource();
        Assert.DoesNotContain("_Draw", source);
        Assert.DoesNotContain("DrawCircle", source);
        Assert.DoesNotContain("QueueRedraw", source);
        Assert.DoesNotContain("DotRadius", source);
    }

    [Fact]
    public void ViewStillAimsThroughTheScreenCenter()
    {
        var source = ViewSource();
        Assert.Contains("GetVisibleRect().Size * 0.5f", source);
        Assert.Contains("ProjectRayNormal(center)", source);
        Assert.Contains("ProjectRayOrigin(center)", source);
        Assert.Contains("_input.SetAim(", source);
        Assert.Contains("_input.ClearAim()", source);
    }

    [Theory]
    [InlineData("PhysicalKeycode: Key.Space, Pressed: true, Echo: false", "_input.PressJump();")]
    [InlineData("ButtonIndex: JoyButton.A, Pressed: true", "_input.PressGamepadA();")]
    [InlineData("PhysicalKeycode: Key.E, Pressed: true, Echo: false", "_input.PressInteract();")]
    [InlineData("ButtonIndex: JoyButton.X, Pressed: true", "_input.PressInteract();")]
    [InlineData("PhysicalKeycode: Key.F, Pressed: true, Echo: false", "_input.PressEat();")]
    [InlineData("ButtonIndex: JoyButton.DpadUp, Pressed: true", "_input.PressEat();")]
    [InlineData("PhysicalKeycode: Key.Key1, Pressed: true, Echo: false", "_input.PressFoodSelect(0);")]
    [InlineData("PhysicalKeycode: Key.Key2, Pressed: true, Echo: false", "_input.PressFoodSelect(1);")]
    [InlineData("PhysicalKeycode: Key.Key3, Pressed: true, Echo: false", "_input.PressFoodSelect(2);")]
    [InlineData("ButtonIndex: JoyButton.DpadLeft, Pressed: true", "_input.PressFoodCycle(-1);")]
    [InlineData("ButtonIndex: JoyButton.DpadRight, Pressed: true", "_input.PressFoodCycle(1);")]
    public void ViewMapsTheButtons(string pattern, string call)
    {
        // The first call after the case pattern is the one that button triggers.
        var source = ViewSource();
        int at = source.IndexOf(pattern, StringComparison.Ordinal);
        Assert.True(at >= 0, $"no case for {pattern}");
        int next = source.IndexOf("_input.", at, StringComparison.Ordinal);
        Assert.Equal(next, source.IndexOf(call, at, StringComparison.Ordinal));
    }

    // --- UiFocus: open menus take the controls away from walking ---

    private const int ShopMenu = 101;
    private const int Dialogue = 202;

    /// <summary>Holds every input and presses every button, then ticks once.</summary>
    private static PlayerInput TickWithEverything(InputModule input, FakeBus bus)
    {
        input.SetKeyboardMove(new Vector2(0, 1));
        input.SetLookStick(new Vector2(1, 0));
        input.AddMouseMotion(new Vector2(10, 0));
        input.SetFireButton(true);
        input.SetRunKey(true);
        input.SetRunTrigger(1f);
        input.PressJump();
        input.PressInteract();
        input.PressOpenMap();
        input.PressEat();
        input.PressFoodSelect(1);
        input.PressFoodCycle(1);
        return TickOnce(input, bus);
    }

    private static void AssertSuppressed(PlayerInput msg)
    {
        Assert.Equal(Vector2.Zero, msg.Move);
        Assert.Equal(Vector2.Zero, msg.Look);
        Assert.False(msg.Jump);
        Assert.False(msg.Fire);
        Assert.False(msg.Run);
        Assert.False(msg.Eat);
        Assert.Equal(-1, msg.FoodSelect);
        Assert.Equal(0, msg.FoodCycle);
        Assert.True(msg.Interact);
        Assert.True(msg.OpenMap);
    }

    private static void AssertLive(PlayerInput msg)
    {
        AssertNear(new Vector2(0, 1), msg.Move);
        Assert.True(msg.Look.X > 0);
        Assert.True(msg.Jump);
        Assert.True(msg.Fire);
        Assert.True(msg.Run);
        Assert.True(msg.Eat);
        Assert.Equal(1, msg.FoodSelect);
        Assert.Equal(1, msg.FoodCycle);
        Assert.True(msg.Interact);
        Assert.True(msg.OpenMap);
    }

    [Fact]
    public void SubscribesToUiFocusOnStart()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.Contains(input, bus.FocusHandlers);
    }

    // --- the mouse: a left click recaptures it only while no menu is open ---

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void ALeftClickRecapturesOnlyAFreeMouseWithNoMenuOpen(bool captured, bool menuOpen, bool expected) =>
        Assert.Equal(expected, InputModule.ShouldRecapture(captured, menuOpen));

    [Fact]
    public void NoRecaptureWhileAUiFocusOwnerIsOpenAndOneAfterTheLastCloses()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.True(input.RecaptureOnClick(captured: false));
        Assert.False(input.TakeRecapture());

        bus.Focus(ShopMenu, true);
        bus.Focus(Dialogue, true);
        TickOnce(input, bus);
        Assert.False(input.RecaptureOnClick(captured: false));
        Assert.False(input.TakeRecapture());

        // One of two closes: still a menu open, no recapture.
        bus.Focus(ShopMenu, false);
        TickOnce(input, bus);
        Assert.False(input.RecaptureOnClick(captured: false));
        Assert.False(input.TakeRecapture());

        // The last closes: one recapture request, then none; clicks recapture again.
        bus.Focus(Dialogue, false);
        TickOnce(input, bus);
        Assert.True(input.TakeRecapture());
        Assert.False(input.TakeRecapture());
        Assert.True(input.RecaptureOnClick(captured: false));
        Assert.False(input.RecaptureOnClick(captured: true));
    }

    [Fact]
    public void ClosingAnOwnerNotOpenOrReopeningBeforeTheRequestIsTakenDoesNotRecapture()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, false);
        Assert.False(input.TakeRecapture());

        // Closed and opened again (another menu) before the view takes it: nothing to recapture.
        bus.Focus(ShopMenu, true);
        bus.Focus(ShopMenu, false);
        bus.Focus(Dialogue, true);
        Assert.False(input.TakeRecapture());
        bus.Focus(Dialogue, false);
        Assert.True(input.TakeRecapture());
    }

    [Fact]
    public void NoMenuOpenLeavesInputsAsTheyAre()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.False(input.MenuOpen);
        AssertLive(TickWithEverything(input, bus));
    }

    [Fact]
    public void OneOpenMenuSuppressesMovementButNotInteractOrMap()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        Assert.True(input.MenuOpen);
        var msg = TickWithEverything(input, bus);
        AssertSuppressed(msg);
        Assert.Equal(msg.Move, input.Latest.Move);
    }

    [Fact]
    public void ClosingTheMenuGivesTheControlsBack()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        AssertSuppressed(TickWithEverything(input, bus));
        bus.Focus(ShopMenu, false);
        Assert.False(input.MenuOpen);
        AssertLive(TickWithEverything(input, bus));
    }

    // --- Smart A: the gamepad's bottom button interacts when there is something to interact with ---

    private const int Signpost = 303;

    [Fact]
    public void SubscribesToInteractAvailableOnStart()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.Contains(input, bus.AvailableHandlers);
    }

    [Fact]
    public void GamepadAJumpsWhenNothingCanBeInteractedWith()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.False(input.InteractOffered);
        input.PressGamepadA();
        var msg = TickOnce(input, bus);
        Assert.True(msg.Jump);
        Assert.False(msg.Interact);
        // Once only.
        msg = TickOnce(input, bus, 1);
        Assert.False(msg.Jump);
        Assert.False(msg.Interact);
    }

    [Fact]
    public void GamepadAInteractsAndDoesNotJumpWhileAnInteractionIsAvailable()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Available(Signpost, true);
        Assert.True(input.InteractOffered);
        input.PressGamepadA();
        var msg = TickOnce(input, bus);
        Assert.True(msg.Interact);
        Assert.False(msg.Jump);
        Assert.False(TickOnce(input, bus, 1).Interact);
        // The controls stay live: only the button's meaning changed.
        input.SetKeyboardMove(new Vector2(0, 1));
        AssertNear(new Vector2(0, 1), TickOnce(input, bus, 2).Move);
    }

    [Fact]
    public void GamepadAInteractsAndDoesNotJumpWhileAMenuIsOpen()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        Assert.False(input.InteractOffered);
        input.PressGamepadA();
        var msg = TickOnce(input, bus);
        Assert.True(msg.Interact);
        Assert.False(msg.Jump);
        bus.Focus(ShopMenu, false);
        input.PressGamepadA();
        msg = TickOnce(input, bus, 1);
        Assert.True(msg.Jump);
        Assert.False(msg.Interact);
    }

    [Fact]
    public void GamepadAJumpsAgainOnceTheLastAvailableOwnerEnds()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Available(Signpost, true);
        bus.Available(Signpost, true);
        bus.Available(Dialogue, true);
        bus.Available(Signpost, false);
        input.PressGamepadA();
        Assert.True(TickOnce(input, bus).Interact);
        // Ending an owner that is not available changes nothing.
        bus.Available(Signpost, false);
        bus.Available(ShopMenu, false);
        Assert.True(input.InteractOffered);
        bus.Available(Dialogue, false);
        Assert.False(input.InteractOffered);
        input.PressGamepadA();
        var msg = TickOnce(input, bus, 1);
        Assert.True(msg.Jump);
        Assert.False(msg.Interact);
    }

    [Fact]
    public void AvailabilityIsDecidedAtTheTickAfterThePress()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.PressGamepadA();
        bus.Available(Signpost, true);
        var msg = TickOnce(input, bus);
        Assert.True(msg.Interact);
        Assert.False(msg.Jump);
    }

    [Fact]
    public void InteractButtonAndKeyboardKeepTheirMeaning()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        // X / Square (and E) interacts with or without anything to interact with...
        input.PressInteract();
        var msg = TickOnce(input, bus);
        Assert.True(msg.Interact);
        Assert.False(msg.Jump);
        bus.Available(Signpost, true);
        input.PressInteract();
        Assert.True(TickOnce(input, bus, 1).Interact);
        // ... and Space jumps even next to a signpost.
        input.PressJump();
        msg = TickOnce(input, bus, 2);
        Assert.True(msg.Jump);
        Assert.False(msg.Interact);
    }

    [Fact]
    public void JumpPressedWhileMenuOpenIsNotReportedAfterItCloses()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        input.PressJump();
        Assert.False(TickOnce(input, bus).Jump);
        bus.Focus(ShopMenu, false);
        Assert.False(TickOnce(input, bus).Jump);
    }

    [Fact]
    public void FoodPressesWhileMenuOpenAreNotReportedAfterItCloses()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        input.PressEat();
        input.PressFoodSelect(2);
        input.PressFoodCycle(-1);
        AssertSuppressed(TickWithEverything(input, bus));
        bus.Focus(ShopMenu, false);
        var msg = TickOnce(input, bus);
        Assert.False(msg.Eat);
        Assert.Equal(-1, msg.FoodSelect);
        Assert.Equal(0, msg.FoodCycle);
    }

    [Theory]
    [InlineData(true, 0f)]
    [InlineData(false, 1f)]
    public void RunIsFalseWhileAMenuIsOpenAndComesBackAfter(bool shift, float trigger)
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.SetRunKey(shift);
        input.SetRunTrigger(trigger);
        Assert.True(TickOnce(input, bus).Run);
        bus.Focus(ShopMenu, true);
        Assert.False(TickOnce(input, bus, 1).Run);
        Assert.False(input.Latest.Run);
        bus.Focus(ShopMenu, false);
        Assert.True(TickOnce(input, bus, 2).Run);
    }

    [Fact]
    public void TwoMenusStaySuppressedUntilBothClose()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        bus.Focus(Dialogue, true);
        AssertSuppressed(TickWithEverything(input, bus));
        bus.Focus(ShopMenu, false);
        AssertSuppressed(TickWithEverything(input, bus));
        bus.Focus(Dialogue, false);
        AssertLive(TickWithEverything(input, bus));
    }

    [Fact]
    public void TwoMenusCanCloseInEitherOrder()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        bus.Focus(Dialogue, true);
        bus.Focus(Dialogue, false);
        AssertSuppressed(TickWithEverything(input, bus));
        bus.Focus(ShopMenu, false);
        AssertLive(TickWithEverything(input, bus));
    }

    [Fact]
    public void AnOwnerThatOpensTwiceNeedsOneClose()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(ShopMenu, true);
        bus.Focus(ShopMenu, true);
        AssertSuppressed(TickWithEverything(input, bus));
        bus.Focus(ShopMenu, false);
        AssertLive(TickWithEverything(input, bus));
    }

    [Fact]
    public void ClosingAnOwnerThatIsNotOpenChangesNothing()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(Dialogue, false);
        AssertLive(TickWithEverything(input, bus));
        bus.Focus(ShopMenu, true);
        bus.Focus(Dialogue, false);
        AssertSuppressed(TickWithEverything(input, bus));
    }

    [Fact]
    public void ManyMenusOpenAtOnceAllNeedClosing()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        for (int owner = 0; owner < 20; owner++)
            bus.Focus(owner, true);
        for (int owner = 0; owner < 19; owner++)
            bus.Focus(owner, false);
        AssertSuppressed(TickWithEverything(input, bus));
        bus.Focus(19, false);
        AssertLive(TickWithEverything(input, bus));
    }

    // --- active device ---

    private static DeviceKind TickDevice(InputModule input, FakeBus bus)
    {
        input.Tick(new TickContext(0, Step));
        Assert.Equal(input.Device.Kind, bus.Devices[^1].Kind);
        return bus.Devices[^1].Kind;
    }

    [Fact]
    public void KeyboardAndMouseIsActiveUntilAGamepadIsUsed()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.Equal(DeviceKind.KeyboardMouse, TickDevice(input, bus));
        Assert.False(bus.Devices[^1].IsGamepad);
        Assert.Equal(DeviceKind.KeyboardMouse, default(ActiveDevice).Kind);
    }

    [Fact]
    public void TheLastDeviceUsedIsActiveAndItSwitchesBothWays()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.NoteGamepadButton(DeviceKind.PlayStation);
        Assert.Equal(DeviceKind.PlayStation, TickDevice(input, bus));
        Assert.True(bus.Devices[^1].IsGamepad);
        // It stays until another device is used.
        Assert.Equal(DeviceKind.PlayStation, TickDevice(input, bus));
        input.NoteKeyboardMouse();
        Assert.Equal(DeviceKind.KeyboardMouse, TickDevice(input, bus));
        input.NoteGamepadAxis(DeviceKind.Nintendo, -0.9f);
        Assert.Equal(DeviceKind.Nintendo, TickDevice(input, bus));
        input.NoteMouseMotion(new Vector2(5, 0));
        Assert.Equal(DeviceKind.KeyboardMouse, TickDevice(input, bus));
        input.NoteGamepadButton(DeviceKind.Xbox);
        Assert.Equal(DeviceKind.Xbox, TickDevice(input, bus));
        // The last one between two ticks wins.
        input.NoteKeyboardMouse();
        input.NoteGamepadButton(DeviceKind.Generic);
        Assert.Equal(DeviceKind.Generic, TickDevice(input, bus));
    }

    [Fact]
    public void StickDriftInsideTheDeadZoneDoesNotSwitch()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.NoteGamepadAxis(DeviceKind.Xbox, 0.15f);
        input.NoteGamepadAxis(DeviceKind.Xbox, -0.2f);
        Assert.Equal(DeviceKind.KeyboardMouse, TickDevice(input, bus));
        input.NoteGamepadAxis(DeviceKind.Xbox, 0.25f);
        Assert.Equal(DeviceKind.Xbox, TickDevice(input, bus));
        input.NoteGamepadAxis(DeviceKind.PlayStation, float.NaN);
        Assert.Equal(DeviceKind.Xbox, TickDevice(input, bus));
    }

    [Fact]
    public void TinyMouseMotionDoesNotSwitch()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.NoteGamepadButton(DeviceKind.Nintendo);
        input.NoteMouseMotion(new Vector2(1, 1));
        Assert.Equal(DeviceKind.Nintendo, TickDevice(input, bus));
        input.NoteMouseMotion(new Vector2(0, -3));
        Assert.Equal(DeviceKind.KeyboardMouse, TickDevice(input, bus));
    }

    [Fact]
    public void AGamepadReportedAsKeyboardMouseCountsAsGeneric()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        input.NoteGamepadButton(DeviceKind.KeyboardMouse);
        Assert.Equal(DeviceKind.Generic, TickDevice(input, bus));
        input.NoteGamepadButton((DeviceKind)42);
        Assert.Equal(DeviceKind.Generic, TickDevice(input, bus));
    }

    [Fact]
    public void TheDeviceIsPublishedWhileAMenuIsOpen()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(7, true);
        input.NoteGamepadButton(DeviceKind.Xbox);
        Assert.Equal(DeviceKind.Xbox, TickDevice(input, bus));
    }

    [Theory]
    [InlineData("Xbox Series X Controller", "")]
    [InlineData("Xbox 360 Controller", "")]
    [InlineData("Xbox One Wireless Controller", "")]
    [InlineData("XInput Controller", "")]
    [InlineData("Controller", "030000005e040000130b000000010000")]
    public void XboxPadsAreDetected(string name, string guid) =>
        Assert.Equal(DeviceKind.Xbox, JoypadFamily.Detect(name, guid));

    [Theory]
    [InlineData("PS4 Controller", "")]
    [InlineData("PS5 Controller", "")]
    [InlineData("PS3 Controller", "")]
    [InlineData("DualSense Wireless Controller", "")]
    [InlineData("Sony Interactive Entertainment Wireless Controller", "")]
    [InlineData("PlayStation 5 DualSense Edge", "")]
    [InlineData("Wireless Controller", "030000004c050000e60c000000016800")]
    public void PlayStationPadsAreDetected(string name, string guid) =>
        Assert.Equal(DeviceKind.PlayStation, JoypadFamily.Detect(name, guid));

    [Theory]
    [InlineData("Nintendo Switch Pro Controller", "")]
    [InlineData("Switch Pro Controller", "")]
    [InlineData("Nintendo Switch Joy-Con (L)", "")]
    [InlineData("Joy-Con (R)", "")]
    [InlineData("Joy-Con (L/R)", "")]
    [InlineData("Pro Controller", "")]
    [InlineData("Wireless Gamepad", "030000007e0500000920000000026803")]
    public void NintendoPadsAreDetected(string name, string guid) =>
        Assert.Equal(DeviceKind.Nintendo, JoypadFamily.Detect(name, guid));

    [Theory]
    [InlineData("Logitech Dual Action", "030000006d04000016c2000000000000")]
    [InlineData("8BitDo SN30 Pro", "03000000c82d00000161000000000000")]
    [InlineData("Generic USB Joystick", "")]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("Wireless Controller", "short")]
    public void OtherPadsAreGeneric(string? name, string? guid) =>
        Assert.Equal(DeviceKind.Generic, JoypadFamily.Detect(name, guid));

    [Fact]
    public void TheNameWinsOverTheGuidAndCaseDoesNotMatter()
    {
        Assert.Equal(DeviceKind.PlayStation, JoypadFamily.Detect("DUALSHOCK 4", "030000005e040000130b000000010000"));
        Assert.Equal(DeviceKind.Xbox, JoypadFamily.Detect("xbox wireless controller", "030000004C050000E60C000000016800"));
        Assert.Equal(DeviceKind.PlayStation, JoypadFamily.Detect("Wireless Controller", "030000004C050000E60C000000016800"));
    }

    // --- prompts ---

    private static readonly DeviceKind[] AllDevices = Enum.GetValues<DeviceKind>();
    private static readonly PromptAction[] AllActions = Enum.GetValues<PromptAction>();

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(here)!, "..", "..", ".."));

    private static string FileOf(string resPath)
    {
        Assert.StartsWith("res://", resPath);
        return System.IO.Path.Combine(RepoRoot(), resPath["res://".Length..]);
    }

    [Fact]
    public void TheEnumsHaveTheAgreedMembers()
    {
        Assert.Equal(new[] { "KeyboardMouse", "Xbox", "PlayStation", "Nintendo", "Generic" }, Enum.GetNames<DeviceKind>());
        Assert.Equal(new[] { "Interact", "Jump", "Fire", "Back", "Confirm", "OpenMap", "Eat", "FoodPrev", "FoodNext",
            "Pause", "MenuUp", "MenuDown", "MenuLeft", "MenuRight", "TurnLeft", "TurnRight", "Move", "Look", "Run" },
            Enum.GetNames<PromptAction>());
    }

    [Fact]
    public void EveryActionHasAnIconFileAndATextOnEveryDevice()
    {
        foreach (var action in AllActions)
            foreach (var device in AllDevices)
            {
                string icon = Prompts.Icon(action, device);
                Assert.StartsWith(Prompts.GlyphDirectory, icon);
                Assert.EndsWith(".svg", icon);
                Assert.Equal(Prompts.GlyphDirectory + Prompts.Glyph(action, device) + ".svg", icon);
                Assert.True(System.IO.File.Exists(FileOf(icon)), $"{action} on {device}: {icon} is missing");
                Assert.False(string.IsNullOrWhiteSpace(Prompts.Text(action, device)), $"{action} on {device} has no text");
            }
    }

    [Fact]
    public void EachDeviceShowsOnlyItsOwnButtons()
    {
        foreach (var action in AllActions)
        {
            Assert.Matches("^(key|mouse)_", Prompts.Glyph(action, DeviceKind.KeyboardMouse));
            Assert.StartsWith("xbox_", Prompts.Glyph(action, DeviceKind.Xbox));
            Assert.StartsWith("ps_", Prompts.Glyph(action, DeviceKind.PlayStation));
            Assert.StartsWith("ns_", Prompts.Glyph(action, DeviceKind.Nintendo));
            // Generic pads use the Xbox-style buttons.
            Assert.Equal(Prompts.Icon(action, DeviceKind.Xbox), Prompts.Icon(action, DeviceKind.Generic));
            Assert.Equal(Prompts.Text(action, DeviceKind.Xbox), Prompts.Text(action, DeviceKind.Generic));
            foreach (var device in AllDevices)
                Assert.DoesNotContain("/", Prompts.Text(action, device));
        }
    }

    [Theory]
    [InlineData(PromptAction.Interact, "key_e", "xbox_a", "ps_cross", "ns_b")]
    [InlineData(PromptAction.Jump, "key_space", "xbox_a", "ps_cross", "ns_b")]
    [InlineData(PromptAction.Fire, "mouse_left", "xbox_rt", "ps_r2", "ns_zr")]
    [InlineData(PromptAction.Back, "key_esc", "xbox_b", "ps_circle", "ns_a")]
    [InlineData(PromptAction.Confirm, "key_enter", "xbox_a", "ps_cross", "ns_b")]
    [InlineData(PromptAction.OpenMap, "key_m", "xbox_view", "ps_share", "ns_minus")]
    [InlineData(PromptAction.Eat, "key_f", "xbox_dpad_up", "ps_dpad_up", "ns_dpad_up")]
    [InlineData(PromptAction.FoodPrev, "mouse_wheel_up", "xbox_dpad_left", "ps_dpad_left", "ns_dpad_left")]
    [InlineData(PromptAction.FoodNext, "mouse_wheel_down", "xbox_dpad_right", "ps_dpad_right", "ns_dpad_right")]
    [InlineData(PromptAction.Pause, "key_esc", "xbox_menu", "ps_options", "ns_plus")]
    [InlineData(PromptAction.MenuUp, "key_up", "xbox_dpad_up", "ps_dpad_up", "ns_dpad_up")]
    [InlineData(PromptAction.MenuDown, "key_down", "xbox_dpad_down", "ps_dpad_down", "ns_dpad_down")]
    [InlineData(PromptAction.MenuLeft, "key_left", "xbox_dpad_left", "ps_dpad_left", "ns_dpad_left")]
    [InlineData(PromptAction.MenuRight, "key_right", "xbox_dpad_right", "ps_dpad_right", "ns_dpad_right")]
    [InlineData(PromptAction.TurnLeft, "key_q", "xbox_lb", "ps_l1", "ns_l")]
    [InlineData(PromptAction.TurnRight, "key_e", "xbox_rb", "ps_r1", "ns_r")]
    [InlineData(PromptAction.Move, "key_wasd", "xbox_ls", "ps_ls", "ns_ls")]
    [InlineData(PromptAction.Look, "mouse_move", "xbox_rs", "ps_rs", "ns_rs")]
    [InlineData(PromptAction.Run, "key_shift", "xbox_lt", "ps_l2", "ns_zl")]
    public void BindingsMatchTheControls(PromptAction action, string keyboard, string xbox, string playStation, string nintendo)
    {
        Assert.Equal(keyboard, Prompts.Glyph(action, DeviceKind.KeyboardMouse));
        Assert.Equal(xbox, Prompts.Glyph(action, DeviceKind.Xbox));
        Assert.Equal(playStation, Prompts.Glyph(action, DeviceKind.PlayStation));
        Assert.Equal(nintendo, Prompts.Glyph(action, DeviceKind.Nintendo));
    }

    [Fact]
    public void TextNamesTheButton()
    {
        Assert.Equal("E", Prompts.Text(PromptAction.Interact, DeviceKind.KeyboardMouse));
        Assert.Equal("A", Prompts.Text(PromptAction.Interact, DeviceKind.Xbox));
        Assert.Equal("Cross", Prompts.Text(PromptAction.Interact, DeviceKind.PlayStation));
        Assert.Equal("B", Prompts.Text(PromptAction.Interact, DeviceKind.Nintendo));
        Assert.Equal("A", Prompts.Text(PromptAction.Confirm, DeviceKind.Xbox));
        Assert.Equal("Cross", Prompts.Text(PromptAction.Confirm, DeviceKind.PlayStation));
        Assert.Equal("B", Prompts.Text(PromptAction.Confirm, DeviceKind.Nintendo));
        Assert.Equal("Esc", Prompts.Text(PromptAction.Back, DeviceKind.KeyboardMouse));
        Assert.Equal("Circle", Prompts.Text(PromptAction.Back, DeviceKind.PlayStation));
        Assert.Equal("Options", Prompts.Text(PromptAction.Pause, DeviceKind.PlayStation));
        Assert.Equal("+", Prompts.Text(PromptAction.Pause, DeviceKind.Nintendo));
        Assert.Equal("Shift", Prompts.Text(PromptAction.Run, DeviceKind.KeyboardMouse));
        Assert.Equal("LT", Prompts.Text(PromptAction.Run, DeviceKind.Xbox));
        Assert.Equal("L2", Prompts.Text(PromptAction.Run, DeviceKind.PlayStation));
        Assert.Equal("ZL", Prompts.Text(PromptAction.Run, DeviceKind.Nintendo));
    }

    [Fact]
    public void RichIsAnImageTagOfTheIcon()
    {
        Assert.Equal("[img=28]res://modules/Input/Glyphs/key_e.svg[/img]", Prompts.Rich(PromptAction.Interact, DeviceKind.KeyboardMouse));
        Assert.Equal("[img=28]res://modules/Input/Glyphs/ps_cross.svg[/img]", Prompts.Rich(PromptAction.Jump, DeviceKind.PlayStation));
        Assert.Equal("[img=40]res://modules/Input/Glyphs/xbox_b.svg[/img]", Prompts.Rich(PromptAction.Back, DeviceKind.Generic, 40));
        foreach (var action in AllActions)
            foreach (var device in AllDevices)
                Assert.Equal($"[img=28]{Prompts.Icon(action, device)}[/img]", Prompts.Rich(action, device));
    }

    [Fact]
    public void UnknownActionsAndDevicesThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Prompts.Icon((PromptAction)99, DeviceKind.Xbox));
        Assert.Throws<ArgumentOutOfRangeException>(() => Prompts.Icon(PromptAction.Jump, (DeviceKind)99));
    }

    [Theory]
    [InlineData("key_e", "key_q", "key_f", "key_m", "key_r", "key_esc", "key_enter", "key_space", "key_shift",
        "key_1", "key_2", "key_3", "key_up", "key_down", "key_left", "key_right", "key_w", "key_a", "key_s", "key_d",
        "key_wasd", "key_arrows", "mouse_left", "mouse_right", "mouse_move", "mouse_wheel_up", "mouse_wheel_down")]
    [InlineData("xbox_a", "xbox_b", "xbox_x", "xbox_y", "xbox_lb", "xbox_rb", "xbox_lt", "xbox_rt", "xbox_ls", "xbox_rs",
        "xbox_dpad_up", "xbox_dpad_down", "xbox_dpad_left", "xbox_dpad_right", "xbox_view", "xbox_menu")]
    [InlineData("ps_cross", "ps_circle", "ps_square", "ps_triangle", "ps_l1", "ps_r1", "ps_l2", "ps_r2", "ps_ls", "ps_rs",
        "ps_dpad_up", "ps_dpad_down", "ps_dpad_left", "ps_dpad_right", "ps_share", "ps_options")]
    [InlineData("ns_a", "ns_b", "ns_x", "ns_y", "ns_l", "ns_r", "ns_zl", "ns_zr", "ns_ls", "ns_rs",
        "ns_dpad_up", "ns_dpad_down", "ns_dpad_left", "ns_dpad_right", "ns_minus", "ns_plus")]
    public void TheGlyphSetIsComplete(params string[] glyphs)
    {
        foreach (var glyph in glyphs)
            Assert.True(System.IO.File.Exists(FileOf(Prompts.GlyphDirectory + glyph + ".svg")), $"{glyph}.svg is missing");
    }

    [Fact]
    public void EveryGlyphIsASmall64PixelSvgWithoutText()
    {
        var files = System.IO.Directory.GetFiles(FileOf(Prompts.GlyphDirectory), "*.svg");
        Assert.True(files.Length >= 75, $"only {files.Length} glyphs");
        foreach (var file in files)
        {
            // Godot's SVG loader does not draw <text>: letters are paths.
            var svg = System.Xml.Linq.XDocument.Load(file).Root!;
            Assert.Equal("svg", svg.Name.LocalName);
            Assert.Equal("64", (string?)svg.Attribute("width"));
            Assert.Equal("64", (string?)svg.Attribute("height"));
            Assert.DoesNotContain(svg.Descendants(), e => e.Name.LocalName is "text" or "image");
            Assert.True(new System.IO.FileInfo(file).Length < 16_000, $"{file} is large");
        }
    }

    [Fact]
    public void ViewReportsDevicesAndMakesTheIconsLoadable()
    {
        var source = ViewSource();
        Assert.Contains("GlyphTextures.Register()", source);
        Assert.Contains("NoteKeyboardMouse()", source);
        Assert.Contains("NoteMouseMotion(", source);
        Assert.Contains("NoteGamepadButton(Family(button.Device))", source);
        Assert.Contains("NoteGamepadAxis(Family(motion.Device), motion.AxisValue)", source);
        Assert.Contains("JoypadFamily.Detect(GodotInput.GetJoyName(device), GodotInput.GetJoyGuid(device))", source);
        Assert.Contains("MouseButton.WheelUp", source);
        Assert.Contains("MouseButton.WheelDown", source);
    }

    // --- MenuNav: paced menu steps (12 ticks of 1/60 s = 0.2 s) ---

    /// <summary>Runs <paramref name="ticks"/> ticks with <paramref name="set"/> applied before each and returns the steps published.</summary>
    private static List<MenuNav> RunMenu(InputModule input, FakeBus bus, ref long tick, int ticks, Action set)
    {
        var steps = new List<MenuNav>();
        for (int i = 0; i < ticks; i++)
        {
            set();
            input.Tick(new TickContext(tick++, Step));
            steps.Add(bus.MenuNavs[^1]);
        }
        return steps;
    }

    private static int[] StepTicks(List<MenuNav> navs)
    {
        var ticks = new List<int>();
        for (int i = 0; i < navs.Count; i++)
            if (navs[i].Any)
                ticks.Add(i);
        return ticks.ToArray();
    }

    [Fact]
    public void MenuNavIsZeroWhileNothingIsPushed()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        var navs = RunMenu(input, bus, ref tick, 30, () => { });
        Assert.Empty(StepTicks(navs));
        Assert.Equal(default, input.LatestMenuNav);
    }

    [Fact]
    public void ShortStickFlickGivesOneStepOnTheFirstTick()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        // Pushed up for 3 ticks (50 ms), then released for a second.
        var navs = RunMenu(input, bus, ref tick, 3, () => input.SetMoveStick(new Vector2(0, 0.9f)));
        navs.AddRange(RunMenu(input, bus, ref tick, 60, () => input.SetMoveStick(Vector2.Zero)));
        Assert.Equal(new[] { 0 }, StepTicks(navs));
        Assert.Equal(-1, navs[0].Vertical);
        Assert.Equal(0, navs[0].Horizontal);
    }

    [Fact]
    public void HeldStickRepeatsEveryPointTwoSeconds()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        var navs = RunMenu(input, bus, ref tick, 61, () => input.SetMoveStick(new Vector2(0, -1)));
        Assert.Equal(new[] { 0, 12, 24, 36, 48, 60 }, StepTicks(navs));
        foreach (int i in StepTicks(navs))
            Assert.Equal(1, navs[i].Vertical);
    }

    [Fact]
    public void RepeatedFlicksWithinPointTwoSecondsGiveOneStep()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        var navs = new List<MenuNav>();
        // Three 2-tick flicks, each followed by a 2-tick release: all inside 12 ticks.
        for (int flick = 0; flick < 3; flick++)
        {
            navs.AddRange(RunMenu(input, bus, ref tick, 2, () => input.SetMoveStick(new Vector2(0, 1))));
            navs.AddRange(RunMenu(input, bus, ref tick, 2, () => input.SetMoveStick(Vector2.Zero)));
        }
        Assert.Equal(new[] { 0 }, StepTicks(navs));
    }

    [Fact]
    public void FlickAfterPointTwoSecondsStepsAtOnce()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        var navs = RunMenu(input, bus, ref tick, 2, () => input.SetMoveStick(new Vector2(0, 1)));
        navs.AddRange(RunMenu(input, bus, ref tick, 10, () => input.SetMoveStick(Vector2.Zero)));
        // Tick 12 is 0.2 s after the first step: the new push fires on its first tick.
        navs.AddRange(RunMenu(input, bus, ref tick, 2, () => input.SetMoveStick(new Vector2(0, -1))));
        Assert.Equal(new[] { 0, 12 }, StepTicks(navs));
        Assert.Equal(-1, navs[0].Vertical);
        Assert.Equal(1, navs[12].Vertical);
    }

    [Fact]
    public void FlickTooSoonWaitsUntilPointTwoSecondsIfStillHeld()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        var navs = RunMenu(input, bus, ref tick, 2, () => input.SetMoveStick(new Vector2(0, 1)));
        navs.AddRange(RunMenu(input, bus, ref tick, 4, () => input.SetMoveStick(Vector2.Zero)));
        // Pushed again at tick 6 and held: the next step is at tick 12, not 6.
        navs.AddRange(RunMenu(input, bus, ref tick, 10, () => input.SetMoveStick(new Vector2(0, 1))));
        Assert.Equal(new[] { 0, 12 }, StepTicks(navs));
    }

    [Fact]
    public void StickUsesItsLargerAxisAndIgnoresWobblesInsideTheThreshold()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        // Wobbles below 0.5 on both axes never fire.
        var wobble = new[] { new Vector2(0.4f, 0.3f), new Vector2(-0.45f, 0), new Vector2(0.1f, -0.49f), new Vector2(0.35f, 0.35f) };
        int w = 0;
        Assert.Empty(StepTicks(RunMenu(input, bus, ref tick, 40, () => input.SetMoveStick(wobble[w++ % wobble.Length]))));

        // Mostly right, a little down: one step right only.
        var navs = RunMenu(input, bus, ref tick, 1, () => input.SetMoveStick(new Vector2(0.8f, -0.6f)));
        Assert.Equal(new MenuNav(0, 1).Horizontal, navs[0].Horizontal);
        Assert.Equal(0, navs[0].Vertical);

        // Mostly down, a little left: one step down only.
        RunMenu(input, bus, ref tick, 12, () => input.SetMoveStick(Vector2.Zero));
        navs = RunMenu(input, bus, ref tick, 1, () => input.SetMoveStick(new Vector2(-0.55f, -0.7f)));
        Assert.Equal(1, navs[0].Vertical);
        Assert.Equal(0, navs[0].Horizontal);

        // Left past the threshold.
        RunMenu(input, bus, ref tick, 12, () => input.SetMoveStick(Vector2.Zero));
        navs = RunMenu(input, bus, ref tick, 1, () => input.SetMoveStick(new Vector2(-0.6f, 0.2f)));
        Assert.Equal(-1, navs[0].Horizontal);
        Assert.Equal(0, navs[0].Vertical);
    }

    [Theory]
    [InlineData(-1, 0)] // D-pad / arrow / W up
    [InlineData(1, 0)]  // down
    [InlineData(0, -1)] // left
    [InlineData(0, 1)]  // right
    public void ButtonsStepOnceForATapAndRepeatWhileHeld(int vertical, int horizontal)
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        // A 2-tick tap, released, tapped again within 0.2 s: one step.
        var navs = RunMenu(input, bus, ref tick, 2, () => input.SetMenuButtons(vertical, horizontal));
        navs.AddRange(RunMenu(input, bus, ref tick, 3, () => input.SetMenuButtons(0, 0)));
        navs.AddRange(RunMenu(input, bus, ref tick, 2, () => input.SetMenuButtons(vertical, horizontal)));
        navs.AddRange(RunMenu(input, bus, ref tick, 20, () => input.SetMenuButtons(0, 0)));
        Assert.Equal(new[] { 0 }, StepTicks(navs));
        Assert.Equal(vertical, navs[0].Vertical);
        Assert.Equal(horizontal, navs[0].Horizontal);

        // Held: steps at once and every 12 ticks.
        navs = RunMenu(input, bus, ref tick, 37, () => input.SetMenuButtons(vertical, horizontal));
        Assert.Equal(new[] { 0, 12, 24, 36 }, StepTicks(navs));
        foreach (int i in StepTicks(navs))
        {
            Assert.Equal(vertical, navs[i].Vertical);
            Assert.Equal(horizontal, navs[i].Horizontal);
        }
    }

    [Fact]
    public void ButtonsWinOverTheStickAndShareItsPacing()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        long tick = 0;
        // Stick flicked up, then the D-pad pressed down 3 ticks later: still one step in 0.2 s.
        var navs = RunMenu(input, bus, ref tick, 2, () => input.SetMoveStick(new Vector2(0, 1)));
        navs.AddRange(RunMenu(input, bus, ref tick, 1, () => input.SetMoveStick(Vector2.Zero)));
        navs.AddRange(RunMenu(input, bus, ref tick, 9, () => input.SetMenuButtons(1, 0)));
        navs.AddRange(RunMenu(input, bus, ref tick, 1, () => input.SetMoveStick(new Vector2(-1, 0))));
        Assert.Equal(new[] { 0, 12 }, StepTicks(navs));
        Assert.Equal(-1, navs[0].Vertical);
        Assert.Equal(1, navs[12].Vertical);
        Assert.Equal(0, navs[12].Horizontal);
    }

    [Fact]
    public void MenuNavIsPublishedWhileAMenuIsOpen()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        bus.Focus(7, true);
        long tick = 0;
        var navs = RunMenu(input, bus, ref tick, 1, () => input.SetMenuButtons(0, 1));
        Assert.Equal(1, navs[0].Horizontal);
        Assert.Equal(navs[0], input.LatestMenuNav);
    }
}
