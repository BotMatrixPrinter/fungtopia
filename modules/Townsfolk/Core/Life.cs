using System.Numerics;

namespace Fungtopia.Townsfolk;

/// <summary>What a townsperson is doing.</summary>
public enum FolkState : byte
{
    /// <summary>Walking to <see cref="Folk.Target"/>.</summary>
    Walking,
    /// <summary>Standing a moment, looking about (or waiting for a friend to arrive for a chat).</summary>
    Waiting,
    /// <summary>Chatting with <see cref="Folk.Partner"/>, facing them.</summary>
    Chatting,
    /// <summary>Sitting on a bench seat (<see cref="Folk.Seat"/>).</summary>
    Sitting,
    /// <summary>Looking into a shop window.</summary>
    Browsing,
    /// <summary>Inside a building (not drawn); comes out at its door later.</summary>
    Inside,
}

/// <summary>What a walk is for: what happens on arriving.</summary>
public enum Errand : byte { Stroll, Chat, Sit, Shop, Browse, Home }

/// <summary>One townsperson. Positions are on the ground (Y = <c>Ground.HeightAt</c>).</summary>
public struct Folk
{
    public Vector3 Position;
    /// <summary>Position at the end of the tick before (the view draws between the two).</summary>
    public Vector3 Previous;
    /// <summary>Yaw (0 faces -Z) and its value the tick before.</summary>
    public float Yaw, PreviousYaw;
    /// <summary>Speed this tick (meters per second): the view swings hands and feet with it.</summary>
    public float Speed;
    public FolkState State;
    public Errand Errand;
    /// <summary>Seconds left in the current state (standing, chatting, sitting, inside...), or the walk's patience.</summary>
    public float Timer;
    /// <summary>Where the walk goes, and the yaw to face on arriving.</summary>
    public Vector2 Target;
    public float TargetYaw;
    /// <summary>The chat partner (index), or -1.</summary>
    public int Partner;
    /// <summary>Whether this one is talking (the other listens) in a chat; they take turns.</summary>
    public bool Speaking;
    /// <summary>The <see cref="CityLife.Seats"/> seat walked to or sat on, or -1.</summary>
    public int Seat;
    /// <summary>The <see cref="CityLife.Doors"/> door gone to or in, or -1.</summary>
    public int Door;
    /// <summary>One of the third who stay out at night.</summary>
    public bool NightOwl;
    /// <summary>Inside for the night (comes out after <see cref="WakeHour"/>).</summary>
    public bool InForNight;
    /// <summary>When a non-owl heads in (in the evening, before dusk) and comes out again (after dawn).</summary>
    public float HomeHour, WakeHour;
    /// <summary>Walking speed (meters per second).</summary>
    public float WalkSpeed;
    /// <summary>Which way to try first round an obstacle (+1 left, -1 right).</summary>
    public float Side;
    /// <summary>The pet (index into the pets) walking with this one, or -1.</summary>
    public int Pet;
    public Look Look;

    /// <summary>Out and drawn (not inside).</summary>
    public readonly bool Visible => State != FolkState.Inside;
}

/// <summary>A background pet's kind.</summary>
public enum CritterKind : byte { Cat, Dog, Crow }

/// <summary>What a pet is doing.</summary>
public enum CritterState : byte
{
    /// <summary>Walking or trotting to <see cref="Critter.Target"/>.</summary>
    Wander,
    /// <summary>Nose to the ground, sniffing about.</summary>
    Sniff,
    /// <summary>Curled up asleep.</summary>
    Nap,
    /// <summary>Running after <see cref="Critter.Other"/>.</summary>
    Chase,
    /// <summary>Running away from <see cref="Critter.Other"/>.</summary>
    Flee,
    /// <summary>At its owner's side.</summary>
    Follow,
    /// <summary>Sitting by its owner while the owner stands, chats or sits.</summary>
    Sit,
    /// <summary>Inside with its owner (not drawn).</summary>
    Inside,
    /// <summary>A crow hopping on the ground.</summary>
    Hop,
    /// <summary>A crow pecking at the ground.</summary>
    Peck,
    /// <summary>A crow flying from <see cref="Critter.From"/> to <see cref="Critter.To"/>.</summary>
    Fly,
    /// <summary>A crow perched on a rooftop.</summary>
    Perch,
}

/// <summary>A background Mushroom Cat, Dog or Crow, roaming or walking with its owner.</summary>
public struct Critter
{
    public CritterKind Kind;
    public CritterState State;
    public Vector3 Position, Previous;
    public float Yaw, PreviousYaw;
    /// <summary>Speed this tick (meters per second).</summary>
    public float Speed;
    public float Timer;
    public Vector2 Target;
    /// <summary>The owner (folk index), or -1 for a roaming pet.</summary>
    public int Owner;
    /// <summary>The pet chased or fled from, or -1.</summary>
    public int Other;
    /// <summary>A crow's flight: from, to and progress (0..1) over <see cref="FlightSeconds"/>.</summary>
    public Vector3 From, To;
    public float Progress, FlightSeconds;
    /// <summary>A crow's rooftop (<see cref="CityLife.Roofs"/> index) while perched or flying to one, else -1.</summary>
    public int Roof;
    /// <summary>Its color (sRGB): fur, or feathers for a crow.</summary>
    public Vector3 Color;
    /// <summary>Drawn size (1 = the model's size).</summary>
    public float Scale;

    public readonly bool Visible => State != CritterState.Inside;
}

/// <summary>A butterfly fluttering round one of <see cref="CityLife.Flowers"/> by day.</summary>
public struct Butterfly
{
    public Vector3 Position, Previous, Velocity;
    public Vector3 Target;
    public int Flower;
    public float Timer;
    public Vector3 Color;
}
