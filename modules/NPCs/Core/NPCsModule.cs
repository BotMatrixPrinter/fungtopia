using System;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Dialogue;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;

namespace Fungtopia.NPCs;

/// <summary>
/// The townsfolk: Dr. Jeremy at his clinic, Millie next door and Lucy at the Pet Store. Each stands
/// at its <see cref="Places"/> spot and turns to face a near player; within <see cref="PromptRadius"/>
/// a talk prompt shows and Interact starts a conversation, one <see cref="SayLine"/> at a time (the
/// next on <see cref="DialogueAdvanced"/>). Jeremy greets the player on <see cref="WokeUp"/>, Millie
/// gives quests (<see cref="QuestState"/> every tick) and pays them with <see cref="GrantCoins"/>,
/// and Lucy sells the Mushroom Crow (<see cref="SpendCoins"/>, then <see cref="AdoptPet"/>).
/// </summary>
public sealed class NPCsModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<PlayerState>,
    IMessageHandler<DialogueState>, IMessageHandler<DialogueAdvanced>, IMessageHandler<DialogueClosed>,
    IMessageHandler<WokeUp>, IMessageHandler<MonsterKilled>, IMessageHandler<CoinsEarned>,
    IMessageHandler<SpendResult>, IMessageHandler<OwnedPets>
{
    public const int Jeremy = 0;
    public const int Millie = 1;
    public const int Lucy = 2;
    public const int Count = 3;

    /// <summary>The <see cref="SayLine.SpeakerId"/> of NPC i is <c>SpeakerIdBase + i</c>.</summary>
    public const int SpeakerIdBase = 7100;

    /// <summary>How near (horizontal meters) the prompt shows and Interact talks.</summary>
    public const float PromptRadius = 2.5f;

    /// <summary>How near (horizontal meters) an NPC turns to face the player.</summary>
    public const float FaceRadius = 6f;

    /// <summary>How fast an NPC turns, radians per second.</summary>
    public const float TurnSpeed = 5f;

    public const string TalkPrompt = "E / X: Talk";
    public const string BuyPrompt = "E / X: Buy the Mushroom Crow (3,000 M¢)";

    public const int CrowPrice = 3000;

    /// <summary>The <see cref="SpendCoins.RequestId"/> of the first purchase; each one after counts up.</summary>
    public const int FirstRequestId = 71_000;

    public const string WakeGreeting = "Good morning! You fainted in the forest, but you're all patched up now.";
    public const string CrowCongrats = "Congrats. It bites.";
    public const string CrowTooPoor = "Come back with 3,000 M¢.";
    public const string CrowAskAgain = "Press E / X again to buy.";

    /// <summary>Millie's quest kinds, given in this order and then again with bigger numbers.</summary>
    public enum QuestKind
    {
        Caterpillars,
        Ants,
        ForestCoins,
    }

    private static readonly string[] s_names = { "Dr. Jeremy", "Millie", "Lucy" };
    private static readonly int[] s_voices = { Voices.Kind, Voices.Bubbly, Voices.Deadpan };
    private static readonly Place[] s_spots = { Places.ClinicDoor, Places.MillieSpot, Places.LucySpot };

    private static readonly string[] s_healthTips =
    {
        "Eat your mushrooms and get plenty of sleep. Doctor's orders!",
        "If a monster hits you, back off for a moment. Running away is a fine medical treatment.",
        "Fainting never costs you coins. I'll always patch you up, free of charge.",
        "The forest gets dark at night. Healthy adventurers rest when the stars come out!",
        "Drink water, stretch your legs, and smile. Laughing is good for the heart!",
    };

    private static readonly string[] s_millieTips =
    {
        "Tip! Mushroom Ants are super fast, so don't let them sneak up on you!",
        "Tip! Poison keeps working after you hit, so one arrow does lots of damage!",
        "Tip! Pets fight on their own. Your little buddy's got your back!",
        "Tip! Caterpillars are slow. Shoot them from far away, hee hee!",
        "Tip! If you faint, Dr. Jeremy patches you up and you keep all your coins!",
    };

    private const int MaxLines = 4;

    private IMessageBus? _bus;
    private PlayerInput _input;
    private bool _haveInput;
    private PlayerState _player;
    private bool _havePlayer;
    private bool _dialogueOpen;
    private int _dialogueSpeaker = -1;
    private bool _closedThisTick;
    private int _advanced = -1;
    private int _closed = -1;
    private bool _wokeUp;

    private readonly float[] _yaw = new float[Count];
    private readonly int[] _tipIndex = new int[Count];
    private readonly string[] _lines = new string[MaxLines];
    private int _lineCount;
    private int _lineIndex;

    // Millie's quest.
    private int _questNumber;
    private string _questTitle = "";

    // Lucy's crow.
    private bool _ownsCrow;
    private int _pendingRequest = -1;
    private int _nextRequestId = FirstRequestId;
    private bool _spendAnswered;
    private bool _spendOk;
    private bool _offerOnClose;

    public NPCsModule()
    {
        for (int i = 0; i < Count; i++)
            _yaw[i] = s_spots[i].Yaw;
    }

    /// <summary>Each NPC's spot, by index (<see cref="Jeremy"/>, <see cref="Millie"/>, <see cref="Lucy"/>).</summary>
    public static ReadOnlySpan<Place> Spots => s_spots;

    public static string Name(int npc) => s_names[npc];

    public static int Voice(int npc) => s_voices[npc];

    public static int SpeakerId(int npc) => SpeakerIdBase + npc;

    /// <summary>Each NPC's current facing (yaw, Godot's convention).</summary>
    public ReadOnlySpan<float> Yaw => _yaw;

    /// <summary>The NPC whose prompt shows, or -1.</summary>
    public int PromptNpc { get; private set; } = -1;

    /// <summary>The prompt's text (<see cref="TalkPrompt"/>, or <see cref="BuyPrompt"/> while Lucy offers the crow).</summary>
    public string PromptText { get; private set; } = TalkPrompt;

    /// <summary>The NPC in a conversation, or -1.</summary>
    public int Talking { get; private set; } = -1;

    /// <summary>Lucy has offered the crow: Interact near her buys it; walking away says no.</summary>
    public bool OfferingCrow { get; private set; }

    public bool QuestActive { get; private set; }
    public bool QuestComplete { get; private set; }
    public int QuestProgress { get; private set; }
    public int QuestGoal { get; private set; }
    public int QuestReward { get; private set; }
    public QuestKind CurrentQuestKind { get; private set; }
    public string QuestTitle => _questTitle;

    /// <summary>Quests paid so far.</summary>
    public int QuestsDone => _questNumber;

    public long Ticks { get; private set; }

    /// <summary>The goal and reward of Millie's quest number <paramref name="number"/> (0 is the first).</summary>
    public static (QuestKind Kind, int Goal, int Reward) Quest(int number)
    {
        var kind = (QuestKind)(number % 3);
        int scale = number / 3 + 1;
        return kind switch
        {
            QuestKind.Caterpillars => (kind, 5 * scale, 150 * scale),
            QuestKind.Ants => (kind, 3 * scale, 150 * scale),
            _ => (kind, 500 * scale, 200 * scale),
        };
    }

    public static string QuestTitleOf(QuestKind kind, int goal) => kind switch
    {
        QuestKind.Caterpillars => $"Defeat {goal} Mushroom Caterpillars",
        QuestKind.Ants => $"Defeat {goal} Mushroom Ants",
        _ => $"Bring me... earn {goal} M¢ in the forest",
    };

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerInput>(this);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<DialogueState>(this);
        bus.Subscribe<DialogueAdvanced>(this);
        bus.Subscribe<DialogueClosed>(this);
        bus.Subscribe<WokeUp>(this);
        bus.Subscribe<MonsterKilled>(this);
        bus.Subscribe<CoinsEarned>(this);
        bus.Subscribe<SpendResult>(this);
        bus.Subscribe<OwnedPets>(this);
    }

    public void Handle(in PlayerInput message)
    {
        _input = message;
        _haveInput = true;
    }

    public void Handle(in PlayerState message)
    {
        _player = message;
        _havePlayer = true;
    }

    public void Handle(in DialogueState message)
    {
        _dialogueOpen = message.Open;
        _dialogueSpeaker = message.SpeakerId;
    }

    public void Handle(in DialogueAdvanced message)
    {
        if (Talking >= 0 && message.SpeakerId == SpeakerId(Talking))
            _advanced = message.SpeakerId;
    }

    public void Handle(in DialogueClosed message)
    {
        _closedThisTick = true;
        _closed = message.SpeakerId;
    }

    public void Handle(in WokeUp message) => _wokeUp = true;

    public void Handle(in MonsterKilled message)
    {
        if (!QuestActive || QuestComplete)
            return;
        if ((CurrentQuestKind == QuestKind.Caterpillars && message.Kind == MonsterKind.Caterpillar)
            || (CurrentQuestKind == QuestKind.Ants && message.Kind == MonsterKind.Ant))
            AddProgress(1);
    }

    public void Handle(in CoinsEarned message)
    {
        if (!QuestActive || QuestComplete || CurrentQuestKind != QuestKind.ForestCoins || message.Amount <= 0)
            return;
        var area = Areas.At(message.Position.X, message.Position.Z);
        if (area is Area.Forest or Area.DeepForest)
            AddProgress(message.Amount);
    }

    public void Handle(in SpendResult message)
    {
        if (message.RequestId != _pendingRequest)
            return;
        _pendingRequest = -1;
        _spendAnswered = true;
        _spendOk = message.Ok;
    }

    public void Handle(in OwnedPets message) => _ownsCrow |= message.Crow;

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("NPCsModule.Tick before Start.");
        Ticks++;
        float dt = (float)context.StepSeconds;

        // Conversation bookkeeping: our line was dismissed, or someone else took the bubble.
        if (_closed >= 0 && Talking >= 0 && _closed == SpeakerId(Talking))
            EndTalk();
        if (Talking >= 0 && _dialogueOpen && _dialogueSpeaker != SpeakerId(Talking))
            EndTalk(ended: false);
        if (_advanced >= 0 && Talking >= 0)
            SayNext();

        if (_spendAnswered)
        {
            _spendAnswered = false;
            if (_spendOk)
            {
                _ownsCrow = true;
                _bus.Publish(new AdoptPet(PetKind.Crow));
                Begin(Lucy, CrowCongrats);
            }
            else
                Begin(Lucy, CrowTooPoor);
        }

        if (_wokeUp)
        {
            _wokeUp = false;
            Begin(Jeremy, WakeGreeting,
                "Fainting never costs you any coins. Take it easy out there, okay?");
        }

        bool alive = _havePlayer && _player.Alive;
        int near = alive ? Nearest(PromptRadius) : -1;
        if (OfferingCrow && near != Lucy)
            OfferingCrow = false;

        TurnAll(dt, alive);

        bool free = !_dialogueOpen && !_closedThisTick && Talking < 0 && _pendingRequest < 0;
        if (free && near >= 0 && _haveInput && _input.Interact)
            Interact(near);

        free = !_dialogueOpen && !_closedThisTick && Talking < 0 && _pendingRequest < 0;
        PromptNpc = free ? near : -1;
        PromptText = PromptNpc == Lucy && OfferingCrow ? BuyPrompt : TalkPrompt;

        _bus.Publish(new QuestState(QuestActive, _questTitle, QuestProgress, QuestGoal, QuestComplete));

        _haveInput = false;
        _closedThisTick = false;
        _advanced = -1;
        _closed = -1;
    }

    private void Interact(int npc)
    {
        switch (npc)
        {
            case Jeremy:
                Begin(Jeremy, "Hello there! I'm Dr. Jeremy. How are you feeling today?", NextTip(Jeremy, s_healthTips));
                break;
            case Millie:
                TalkToMillie();
                break;
            default:
                TalkToLucy();
                break;
        }
    }

    private void TalkToMillie()
    {
        if (!QuestActive)
        {
            GiveQuest();
            Begin(Millie, _questNumber == 0 ? "Hiii, bestie! I'm so happy you're here!" : "Hiii again, bestie!",
                "Can you help me? " + _questTitle + "! I'll give you " + Format(QuestReward) + " M¢!",
                NextTip(Millie, s_millieTips));
        }
        else if (QuestComplete)
        {
            int reward = QuestReward;
            _bus!.Publish(new GrantCoins(reward, "Millie's quest"));
            _questNumber++;
            GiveQuest();
            Begin(Millie, "Yay, you did it! You're the best! Here's " + Format(reward) + " M¢!",
                "Ooh, next one: " + _questTitle + "! That's " + Format(QuestReward) + " M¢!");
        }
        else
        {
            Begin(Millie, "Hi bestie! " + _questTitle + ": " + Format(QuestProgress) + " / " + Format(QuestGoal) + " so far!",
                NextTip(Millie, s_millieTips));
        }
    }

    private void TalkToLucy()
    {
        if (_ownsCrow)
        {
            Begin(Lucy, "Welcome to Lucy's Pet Store.", "You already have the crow. It still bites.");
            return;
        }
        if (OfferingCrow)
        {
            OfferingCrow = false;
            _pendingRequest = _nextRequestId++;
            _bus!.Publish(new SpendCoins(_pendingRequest, CrowPrice, "Mushroom Crow"));
            return;
        }
        Begin(Lucy, "Welcome to Lucy's Pet Store. Yay.",
            "That's the Mushroom Crow. 3,000 M¢. Want it?", CrowAskAgain);
        _offerOnClose = true;
    }

    private void GiveQuest()
    {
        var (kind, goal, reward) = Quest(_questNumber);
        QuestActive = true;
        QuestComplete = false;
        QuestProgress = 0;
        QuestGoal = goal;
        QuestReward = reward;
        CurrentQuestKind = kind;
        _questTitle = QuestTitleOf(kind, goal);
    }

    private void AddProgress(int amount)
    {
        QuestProgress = Math.Min(QuestGoal, QuestProgress + amount);
        if (QuestProgress >= QuestGoal)
            QuestComplete = true;
    }

    private string NextTip(int npc, string[] tips)
    {
        string tip = tips[_tipIndex[npc] % tips.Length];
        _tipIndex[npc]++;
        return tip;
    }

    private void Begin(int npc, string a, string? b = null, string? c = null, string? d = null)
    {
        _lineCount = 0;
        _lines[_lineCount++] = a;
        if (b != null)
            _lines[_lineCount++] = b;
        if (c != null)
            _lines[_lineCount++] = c;
        if (d != null)
            _lines[_lineCount++] = d;
        Talking = npc;
        _lineIndex = 0;
        _offerOnClose = false;
        Publish();
    }

    private void SayNext()
    {
        if (_lineIndex + 1 >= _lineCount)
            return;
        _lineIndex++;
        Publish();
    }

    private void Publish()
    {
        int npc = Talking;
        _bus!.Publish(new SayLine(SpeakerId(npc), s_names[npc], _lines[_lineIndex], s_voices[npc],
            _lineIndex == _lineCount - 1));
    }

    private void EndTalk(bool ended = true)
    {
        // Lucy's offer stands once its last line ("Press E / X again to buy.") is dismissed.
        if (ended && Talking == Lucy && _offerOnClose && !_ownsCrow)
            OfferingCrow = true;
        _offerOnClose = false;
        Talking = -1;
        _lineCount = 0;
        _lineIndex = 0;
    }

    private int Nearest(float radius)
    {
        int best = -1;
        float bestSq = radius * radius;
        for (int i = 0; i < Count; i++)
        {
            float sq = HorizontalDistanceSq(i);
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }
        return best;
    }

    private float HorizontalDistanceSq(int npc)
    {
        float dx = _player.Position.X - s_spots[npc].Position.X;
        float dz = _player.Position.Z - s_spots[npc].Position.Z;
        return dx * dx + dz * dz;
    }

    private void TurnAll(float dt, bool alive)
    {
        for (int i = 0; i < Count; i++)
        {
            float target = s_spots[i].Yaw;
            if (_havePlayer && (alive || Talking == i))
            {
                float sq = HorizontalDistanceSq(i);
                if (sq <= FaceRadius * FaceRadius && sq > 1e-6f)
                {
                    float dx = _player.Position.X - s_spots[i].Position.X;
                    float dz = _player.Position.Z - s_spots[i].Position.Z;
                    // The direction of yaw a is (-sin a, 0, -cos a).
                    target = MathF.Atan2(-dx, -dz);
                }
            }
            _yaw[i] = TurnToward(_yaw[i], target, TurnSpeed * dt);
        }
    }

    /// <summary>Turns <paramref name="from"/> toward <paramref name="to"/> the short way, at most <paramref name="step"/>; wrapped to (-π, π].</summary>
    public static float TurnToward(float from, float to, float step)
    {
        float diff = Wrap(to - from);
        float turned = MathF.Abs(diff) <= step ? to : from + MathF.Sign(diff) * step;
        return Wrap(turned);
    }

    private static float Wrap(float angle)
    {
        angle %= MathF.Tau;
        if (angle > MathF.PI)
            angle -= MathF.Tau;
        else if (angle <= -MathF.PI)
            angle += MathF.Tau;
        return angle;
    }

    private static string Format(int amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The position of NPC <paramref name="npc"/>'s feet.</summary>
    public static Vector3 Position(int npc) => s_spots[npc].Position;
}
