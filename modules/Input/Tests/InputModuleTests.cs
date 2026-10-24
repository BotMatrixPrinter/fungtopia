using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Input.Tests;

public class InputModuleTests
{
    private const double Step = 1.0 / 60;

    /// <summary>Fake of the shell's bus: records every PlayerInput published.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<PlayerInput> Published = new();
        public int OtherMessages;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is PlayerInput input)
                Published.Add(input);
            else
                OtherMessages++;
        }
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
    public void PublishesExactlyOnePlayerInputPerTick()
    {
        var bus = new FakeBus();
        var input = Started(bus);
        Assert.Empty(bus.Published);
        for (int i = 0; i < 5; i++)
            input.Tick(new TickContext(i, Step));
        Assert.Equal(5, bus.Published.Count);
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

    // --- allocations ---

    [Fact]
    public void TickDoesNotAllocate()
    {
        var input = new InputModule();
        input.Start(new NullBus());
        input.SetKeyboardMove(new Vector2(1, 1));
        input.SetLookStick(new Vector2(0.5f, 0.5f));
        input.SetAim(new Vector3(2, 3, 4), new Vector3(1, 1, 1));
        for (int i = 0; i < 10; i++)
            input.Tick(new TickContext(i, Step));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            input.AddMouseMotion(new Vector2(1, 1));
            input.PressJump();
            input.PressInteract();
            input.PressOpenMap();
            input.Tick(new TickContext(i, Step));
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private sealed class NullBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }
}
