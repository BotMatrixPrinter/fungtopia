using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Dialogue;
using Fungtopia.Food;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Fungtopia.Wardrobe;

namespace Fungtopia.NPCs;

/// <summary>
/// The townsfolk: Dr. Jeremy at his clinic, Millie next door, Lucy at the Pet Store, Abby at her
/// clothing shop and Pip, the cook behind the Mushroom Munchies food stall. Each stands
/// at its <see cref="Places"/> spot and turns to face a near player; within <see cref="PromptRadius"/>
/// a talk prompt shows and Interact starts a conversation, one <see cref="SayLine"/> at a time (the
/// next on <see cref="DialogueAdvanced"/>). Jeremy greets the player on <see cref="WokeUp"/>, Millie
/// gives quests (<see cref="QuestState"/> every tick) and pays them with <see cref="GrantCoins"/> (some
/// also with clothes, <see cref="GrantItem"/>), Lucy sells the Mushroom Crow (<see cref="SpendCoins"/>,
/// then <see cref="AdoptPet"/>), Abby greets you and opens her shop (<see cref="OpenShop"/>), and Pip
/// greets you and opens the stall's menu (<see cref="OpenFoodMenu"/>).
/// Quest progress and the crow purchase are saved (<see cref="CollectSave"/>) and restored
/// (<see cref="RestorePart"/>) as the "NPCs" part.
/// </summary>
public sealed class NPCsModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<PlayerState>,
    IMessageHandler<DialogueState>, IMessageHandler<DialogueAdvanced>, IMessageHandler<DialogueClosed>,
    IMessageHandler<WokeUp>, IMessageHandler<MonsterKilled>, IMessageHandler<CoinsEarned>,
    IMessageHandler<SpendResult>, IMessageHandler<OwnedPets>, IMessageHandler<WardrobeState>,
    IMessageHandler<CollectSave>, IMessageHandler<RestorePart>, IMessageHandler<UiFocus>, IMessageHandler<ActiveDevice>
{
    public const int Jeremy = 0;
    public const int Millie = 1;
    public const int Lucy = 2;
    public const int Abby = 3;
    public const int Pip = 4;
    public const int Count = 5;

    /// <summary>The <see cref="SayLine.SpeakerId"/> of NPC i is <c>SpeakerIdBase + i</c>.</summary>
    public const int SpeakerIdBase = 7100;

    /// <summary>How near (horizontal meters) the prompt shows and Interact talks.</summary>
    public const float PromptRadius = 2.5f;

    /// <summary>How near (horizontal meters) an NPC turns to face the player.</summary>
    public const float FaceRadius = 6f;

    /// <summary>How fast an NPC turns, radians per second.</summary>
    public const float TurnSpeed = 5f;

    /// <summary>What the prompt offers; it shows the Interact button's icon of the device in use, then the word.</summary>
    public enum PromptKind { Talk, Food, Buy }

    public const string TalkWord = "Talk";
    public const string FoodWord = "Food";
    public const string BuyWord = "Buy the Mushroom Crow (1,500 M¢)";

    /// <summary>The prompt's icon size in pixels, next to its 26-pixel text.</summary>
    public const int PromptIconSize = 36;

    /// <summary>The <see cref="InteractAvailable.Owner"/> published while a prompt shows (so the gamepad's A talks instead of jumping).</summary>
    public const int InteractOwner = 0x6E70_6373; // "npcs"

    public const int CrowPrice = 1500;

    /// <summary>The <see cref="SpendCoins.RequestId"/> of the first purchase; each one after counts up.</summary>
    public const int FirstRequestId = 71_000;

    public const string WakeGreeting = "Good morning! You fainted in the forest, but you're all patched up now.";
    public const string CrowCongrats = "Congrats. It bites.";
    public const string CrowTooPoor = "Come back with 1,500 M¢.";
    public const string CrowAskAgain = "Ask me again to buy it.";
    public const string AbbyGreeting = "Ooh, you'd look so cute in something new!";

    /// <summary>How far behind <see cref="Places.FoodSpot"/> Pip stands (just behind the stall's counter, meters).</summary>
    public const float PipBehindCounter = 2.6f;

    /// <summary>How far to the left of the counter's middle (as Pip sees it) Pip stands, beside the soup pot (meters).</summary>
    public const float PipBesidePot = 0.4f;

    /// <summary>How big the view draws each model (by NPC index): all about 1.25 to 1.35 m tall, Jeremy the tallest.</summary>
    public static readonly float[] FigureScales = { 1.02f, 1.0f, 0.97f, 0.99f, 0.96f };

    /// <summary>
    /// The top of each model (hair, ears, bun or hat; meters above its origin, before <see cref="FigureScales"/>;
    /// Pip on the step stool): the view floats the prompt over it.
    /// </summary>
    public static readonly float[] HeadTops = { 1.32f, 1.38f, 1.32f, 1.47f, 2.0f };

    /// <summary>The <see cref="SaveData.Version"/> this module writes and reads.</summary>
    public const int SaveVersion = 1;

    /// <summary>
    /// The saved state (the "NPCs" <see cref="SavePart"/>): Millie's quests and whether Lucy's crow
    /// was bought.
    /// </summary>
    /// <param name="Version">Always <see cref="SaveVersion"/>; other versions are not read.</param>
    /// <param name="QuestsDone">Quests paid so far (also the number of the current quest).</param>
    /// <param name="QuestActive">Millie has given the current quest.</param>
    /// <param name="QuestProgress">The current quest's count.</param>
    /// <param name="QuestComplete">The current quest's goal is reached (not yet paid).</param>
    /// <param name="RewardsGiven">The <see cref="Catalog"/> ids of the clothes Millie has given.</param>
    /// <param name="CrowBought">The Mushroom Crow was bought from Lucy.</param>
    public sealed record SaveData(int Version, int QuestsDone, bool QuestActive, int QuestProgress, bool QuestComplete,
        string[] RewardsGiven, bool CrowBought);

    /// <summary>Millie's quest kinds, given in this order and then again with bigger numbers.</summary>
    public enum QuestKind
    {
        Caterpillars,
        Ants,
        ForestCoins,
    }

    private static readonly string[] s_names = { "Dr. Jeremy", "Millie", "Lucy", "Abby", "Pip" };
    private static readonly int[] s_voices = { Voices.Kind, Voices.Bubbly, Voices.Deadpan, Voices.Bubbly, Voices.Bubbly };
    private static readonly Place[] s_spots =
        { Places.ClinicDoor, Places.MillieSpot, Places.LucySpot, Places.AbbySpot, BehindCounter(Places.FoodSpot) };

    /// <summary>Where the player stands to talk to each NPC: their own spot, but Pip's customers stand at the counter.</summary>
    private static readonly Place[] s_talkSpots =
        { Places.ClinicDoor, Places.MillieSpot, Places.LucySpot, Places.AbbySpot, Places.FoodSpot };

    /// <summary>Pip's greetings, one per talk, in turn.</summary>
    private static readonly string[] s_pipGreetings =
    {
        "Fresh from the oven!",
        "Hungry from the forest? I've got you!",
        "A pie a day keeps the doctor away... sorry, Dr. Jeremy!",
    };

    /// <summary>The clothes Millie gives with her quests, by quest number (the first round only); empty for none.</summary>
    // Every prompt for every device, built once so the per-tick prompt choice allocates nothing.
    private static readonly string[,] s_prompts = BuildPrompts();

    private static string[,] BuildPrompts()
    {
        string[] words = { TalkWord, FoodWord, BuyWord };
        var devices = Enum.GetValues<DeviceKind>();
        var prompts = new string[words.Length, devices.Length];
        for (int k = 0; k < words.Length; k++)
            foreach (var device in devices)
                prompts[k, (int)device] = $"{Prompts.Rich(PromptAction.Interact, device, PromptIconSize)} {words[k]}";
        return prompts;
    }

    private static readonly string[] s_questItems = { "", "flower_clip", "", "mushroom_backpack" };

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

    private readonly List<string> _rewardsGiven = new();

    // Lucy's crow.
    private bool _ownsCrow;
    private bool _crowBought;
    private int _pendingRequest = -1;
    private int _nextRequestId = FirstRequestId;
    private bool _spendAnswered;
    private bool _spendOk;
    private bool _offerOnClose;

    // Abby's shop.
    private bool _shopOnClose;
    private bool _wardrobeOpen;

    // Pip's menu.
    private bool _menuOnClose;

    // The owners of the menus holding UiFocus open (the food menu, the start screen...).
    private readonly int[] _focusOwners = new int[MaxFocusOwners];
    private int _focusCount;
    private bool _offered;
    private const int MaxFocusOwners = 16;

    public NPCsModule()
    {
        for (int i = 0; i < Count; i++)
            _yaw[i] = s_spots[i].Yaw;
    }

    /// <summary>Each NPC's spot, by index (<see cref="Jeremy"/>, <see cref="Millie"/>, <see cref="Lucy"/>, <see cref="Abby"/>, <see cref="Pip"/>).</summary>
    public static ReadOnlySpan<Place> Spots => s_spots;

    /// <summary>
    /// Where the player stands to talk to each NPC (the prompt shows within <see cref="PromptRadius"/>
    /// of it): the NPC's own spot, except Pip's, which is <see cref="Places.FoodSpot"/> in front of the counter.
    /// </summary>
    public static ReadOnlySpan<Place> TalkSpots => s_talkSpots;

    /// <summary>Pip's greetings, in the order they are said.</summary>
    public static ReadOnlySpan<string> PipGreetings => s_pipGreetings;

    public static string Name(int npc) => s_names[npc];

    public static int Voice(int npc) => s_voices[npc];

    public static int SpeakerId(int npc) => SpeakerIdBase + npc;

    /// <summary>Each NPC's current facing (yaw, Godot's convention).</summary>
    public ReadOnlySpan<float> Yaw => _yaw;

    /// <summary>The NPC whose prompt shows, or -1.</summary>
    public int PromptNpc { get; private set; } = -1;

    /// <summary>What the prompt offers: <see cref="PromptKind.Food"/> for Pip, <see cref="PromptKind.Buy"/> while Lucy offers the crow, else <see cref="PromptKind.Talk"/>.</summary>
    public PromptKind Prompt { get; private set; }

    /// <summary>The device used last (the latest <see cref="ActiveDevice"/>): the prompt shows its button only.</summary>
    public DeviceKind Device { get; private set; }

    /// <summary>The prompt as RichTextLabel BBCode for <see cref="Device"/> (built once, at startup): see <see cref="PromptBbcode"/>.</summary>
    public string PromptText => PromptBbcode(Prompt, Device);

    /// <summary>The prompt <paramref name="kind"/> as RichTextLabel BBCode: <paramref name="device"/>'s Interact icon and the word ("[E key icon] Talk").</summary>
    public static string PromptBbcode(PromptKind kind, DeviceKind device) =>
        s_prompts[(int)kind, (uint)device < (uint)s_prompts.GetLength(1) ? (int)device : 0];

    /// <summary>The NPC in a conversation, or -1.</summary>
    public int Talking { get; private set; } = -1;

    /// <summary>The NPC the living player is within <see cref="FaceRadius"/> of, or -1 (the nearest when several); Pip waves when you come near.</summary>
    public int Noticing { get; private set; } = -1;

    /// <summary>Lucy has offered the crow: Interact near her buys it; walking away or <see cref="DeclineCrow"/> says no.</summary>
    public bool OfferingCrow { get; private set; }

    /// <summary>The view's Back button (gamepad B) on Lucy's buy choice: says no to the crow, as walking away does.</summary>
    public void DeclineCrow() => OfferingCrow = false;

    public bool QuestActive { get; private set; }
    public bool QuestComplete { get; private set; }
    public int QuestProgress { get; private set; }
    public int QuestGoal { get; private set; }
    public int QuestReward { get; private set; }
    public QuestKind CurrentQuestKind { get; private set; }
    public string QuestTitle => _questTitle;

    /// <summary>Quests paid so far.</summary>
    public int QuestsDone => _questNumber;

    /// <summary>The <see cref="Catalog"/> ids of the clothes Millie has given, in order.</summary>
    public IReadOnlyList<string> RewardsGiven => _rewardsGiven;

    /// <summary>The Mushroom Crow was bought from Lucy (in this game, saved and restored).</summary>
    public bool CrowBought => _crowBought;

    /// <summary>Where a save that cannot be restored is reported (one line per <see cref="RestorePart"/>).</summary>
    public Action<string> Log { get; set; } = Console.Error.WriteLine;

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

    /// <summary>The <see cref="Catalog"/> id of the clothes Millie gives with quest <paramref name="number"/> besides coins, or empty.</summary>
    public static string QuestItem(int number) =>
        number >= 0 && number < s_questItems.Length ? s_questItems[number] : "";

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
        bus.Subscribe<WardrobeState>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
        bus.Subscribe<UiFocus>(this);
        bus.Subscribe<ActiveDevice>(this);
    }

    public void Handle(in ActiveDevice message) => Device = message.Kind;

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

    public void Handle(in WardrobeState message) => _wardrobeOpen = message.Open;

    public void Handle(in UiFocus message)
    {
        int at = Array.IndexOf(_focusOwners, message.Owner, 0, _focusCount);
        if (message.Open && at < 0 && _focusCount < MaxFocusOwners)
            _focusOwners[_focusCount++] = message.Owner;
        else if (!message.Open && at >= 0)
            _focusOwners[at] = _focusOwners[--_focusCount];
    }

    public void Handle(in CollectSave message) =>
        _bus?.Publish(new SavePart(message.RequestId, SaveModules.NPCs, SaveJson()));

    public void Handle(in RestorePart message)
    {
        if (message.Module == SaveModules.NPCs)
            Restore(message.Json);
    }

    /// <summary>The saved state as compact JSON (what <see cref="CollectSave"/> is answered with).</summary>
    public string SaveJson() => JsonSerializer.Serialize(new SaveData(SaveVersion, _questNumber, QuestActive,
        QuestProgress, QuestComplete, _rewardsGiven.ToArray(), _crowBought));

    /// <summary>
    /// Restores the state <see cref="SaveJson"/> wrote, quietly: no lines, coins, items or pets are
    /// published. Missing, unreadable or unknown-version JSON keeps the defaults (a new game's state)
    /// and logs one line.
    /// </summary>
    public void Restore(string? json)
    {
        ResetSaved();
        SaveData? data = null;
        string problem = "";
        if (string.IsNullOrWhiteSpace(json))
            problem = "it is empty";
        else
        {
            try
            {
                data = JsonSerializer.Deserialize<SaveData>(json);
                if (data == null)
                    problem = "it is null";
                else if (data.Version != SaveVersion)
                    problem = $"version {data.Version} is unknown";
                else if (data.QuestsDone < 0 || data.QuestProgress < 0)
                    problem = "it has negative counts";
            }
            catch (JsonException e)
            {
                problem = "it is not readable: " + e.Message;
            }
        }
        if (problem.Length > 0 || data == null)
        {
            Log($"WARNING: Fungtopia: NPCs: saved state not restored ({problem}); keeping the defaults.");
            return;
        }

        _questNumber = data.QuestsDone;
        if (data.QuestActive)
        {
            GiveQuest();
            QuestProgress = Math.Min(QuestGoal, data.QuestProgress);
            QuestComplete = QuestProgress >= QuestGoal;
        }
        if (data.RewardsGiven != null)
            foreach (var id in data.RewardsGiven)
                if (!string.IsNullOrEmpty(id) && !_rewardsGiven.Contains(id))
                    _rewardsGiven.Add(id);
        _crowBought = data.CrowBought;
        _ownsCrow = data.CrowBought;
    }

    /// <summary>Back to a new game's quests and crow, and no conversation, offer or purchase under way.</summary>
    private void ResetSaved()
    {
        _questNumber = 0;
        _questTitle = "";
        QuestActive = false;
        QuestComplete = false;
        QuestProgress = 0;
        QuestGoal = 0;
        QuestReward = 0;
        CurrentQuestKind = QuestKind.Caterpillars;
        _rewardsGiven.Clear();
        _crowBought = false;
        _ownsCrow = false;
        OfferingCrow = false;
        _pendingRequest = -1;
        _spendAnswered = false;
        _offerOnClose = false;
        _shopOnClose = false;
        _menuOnClose = false;
        Talking = -1;
        _lineCount = 0;
        _lineIndex = 0;
        _advanced = -1;
        _closed = -1;
        _wokeUp = false;
    }

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
                _crowBought = true;
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
        int near = alive ? Nearest(s_talkSpots, PromptRadius) : -1;
        Noticing = alive ? Nearest(s_spots, FaceRadius) : -1;
        if (OfferingCrow && near != Lucy)
            OfferingCrow = false;

        TurnAll(dt, alive);

        bool free = !_dialogueOpen && !_closedThisTick && Talking < 0 && _pendingRequest < 0 && !_wardrobeOpen
            && _focusCount == 0;
        if (free && near >= 0 && _haveInput && _input.Interact)
            Interact(near);

        free = !_dialogueOpen && !_closedThisTick && Talking < 0 && _pendingRequest < 0 && !_wardrobeOpen
            && _focusCount == 0;
        PromptNpc = free ? near : -1;
        Prompt = PromptNpc == Lucy && OfferingCrow ? PromptKind.Buy : PromptNpc == Pip ? PromptKind.Food : PromptKind.Talk;
        if ((PromptNpc >= 0) != _offered)
        {
            _offered = PromptNpc >= 0;
            _bus.Publish(new InteractAvailable(InteractOwner, _offered));
        }

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
            case Lucy:
                TalkToLucy();
                break;
            case Abby:
                // The shop opens once her greeting is dismissed.
                Begin(Abby, AbbyGreeting);
                _shopOnClose = true;
                break;
            default:
                // The stall's menu opens once Pip's greeting is dismissed.
                Begin(Pip, NextTip(Pip, s_pipGreetings));
                _menuOnClose = true;
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
            string thanks = "Yay, you did it! You're the best! Here's " + Format(reward) + " M¢!";
            if (Catalog.TryGet(QuestItem(_questNumber), out var item) && !_rewardsGiven.Contains(item.Id))
            {
                _rewardsGiven.Add(item.Id);
                _bus.Publish(new GrantItem(item.Id, "Millie's quest"));
                thanks = "Yay, you did it! You're the best! Here's " + Format(reward) + " M¢ and my " + item.Name + "!";
            }
            _questNumber++;
            GiveQuest();
            Begin(Millie, thanks, "Ooh, next one: " + _questTitle + "! That's " + Format(QuestReward) + " M¢!");
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
            "That's the Mushroom Crow. 1,500 M¢. Want it?", CrowAskAgain);
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
        _shopOnClose = false;
        _menuOnClose = false;
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
        // Lucy's offer stands once its last line ("Ask me again to buy it.") is dismissed.
        if (ended && Talking == Lucy && _offerOnClose && !_ownsCrow)
            OfferingCrow = true;
        // Abby opens her shop once her greeting is dismissed.
        if (ended && Talking == Abby && _shopOnClose)
            _bus!.Publish(new OpenShop());
        // Pip opens the stall's menu once their greeting is dismissed.
        if (ended && Talking == Pip && _menuOnClose)
            _bus!.Publish(new OpenFoodMenu());
        _offerOnClose = false;
        _shopOnClose = false;
        _menuOnClose = false;
        Talking = -1;
        _lineCount = 0;
        _lineIndex = 0;
    }

    private int Nearest(Place[] spots, float radius)
    {
        int best = -1;
        float bestSq = radius * radius;
        for (int i = 0; i < Count; i++)
        {
            float sq = HorizontalDistanceSq(spots[i]);
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }
        return best;
    }

    private float HorizontalDistanceSq(in Place spot)
    {
        float dx = _player.Position.X - spot.Position.X;
        float dz = _player.Position.Z - spot.Position.Z;
        return dx * dx + dz * dz;
    }

    /// <summary>
    /// Pip's spot: <see cref="PipBehindCounter"/> behind the stall's <paramref name="counter"/> spot and
    /// <see cref="PipBesidePot"/> to the left, facing the same way as it (out over the counter, at the customer).
    /// </summary>
    private static Place BehindCounter(in Place counter)
    {
        // Pip's right (the facing turned a quarter clockwise seen from above).
        var right = new Vector3(-counter.Facing.Z, 0f, counter.Facing.X);
        var p = counter.Position - counter.Facing * PipBehindCounter - right * PipBesidePot;
        return new Place(p.X, p.Z, counter.Yaw);
    }

    private void TurnAll(float dt, bool alive)
    {
        for (int i = 0; i < Count; i++)
        {
            float target = s_spots[i].Yaw;
            // Pip keeps facing the counter (and stirring the pot on it).
            if (i != Pip && _havePlayer && (alive || Talking == i))
            {
                float sq = HorizontalDistanceSq(s_spots[i]);
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
