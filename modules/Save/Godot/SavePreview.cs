using System;
using System.IO;
using System.Collections.Generic;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Godot;

namespace Fungtopia.Save.View;

/// <summary>
/// A stand-alone scene showing the pause menu, for screenshots:
/// <c>./screenshot.sh modules/Save/Godot/Preview.tscn</c>. The environment variable
/// SAVE_PREVIEW picks the page: "pause" (default), "saved" (with the "Saved!" toast), "slots"
/// (the slot list: a game, a damaged file and an empty slot) or "choice" (slot 1's manual save
/// and autosave). SAVE_DEVICE picks the device whose button icons the hints show: "keyboard"
/// (default), "xbox", "playstation", "nintendo" or "generic". The saves are made up in a temp folder.
/// </summary>
public partial class SavePreview : Node
{
    /// <summary>A bus nobody listens to.</summary>
    private sealed class QuietBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }

        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly SaveModule _save = new();
    private static readonly List<Texture2D> s_icons = new();
    private long _tick;

    public override void _Ready()
    {
        string page = OS.GetEnvironment("SAVE_PREVIEW").ToLowerInvariant();
        var backdrop = new ColorRect { Color = new Color(0.55f, 0.78f, 0.62f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var layer = new CanvasLayer { Layer = 0 };
        layer.AddChild(backdrop);
        AddChild(layer);

        _save.Start(new QuietBus());
        var view = (SaveView)GD.Load<PackedScene>("res://modules/Save/Godot/Main.tscn").Instantiate();
        view.Bind(_save);
        AddChild(view);

        string folder = Path.Combine(Path.GetTempPath(), "fungtopia-save-preview");
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        _save.SaveFolder = folder;
        var now = DateTime.UtcNow;
        Write(new SaveData { Slot = 1, Auto = false, SavedAt = now.AddMinutes(-42), CharacterName = "Girl", Character = CharacterKind.Girl, Day = 3, Coins = 120, Area = Area.Town });
        Write(new SaveData { Slot = 1, Auto = true, SavedAt = now.AddMinutes(-2), CharacterName = "Girl", Character = CharacterKind.Girl, Day = 4, Coins = 185, Area = Area.Forest });
        File.WriteAllText(_save.Files!.PathOf(2, false), "{ \"version\": 1, \"parts\": { \"Pl");
        _save.Handle(new StartScreenState(false));
        _save.Handle(new GameStart(GameStart.Girl, GameStart.Cat));
        _save.Handle(new ActiveDevice(OS.GetEnvironment("SAVE_DEVICE").ToLowerInvariant() switch
        {
            "xbox" => DeviceKind.Xbox,
            "playstation" or "ps" => DeviceKind.PlayStation,
            "nintendo" => DeviceKind.Nintendo,
            "generic" => DeviceKind.Generic,
            _ => DeviceKind.KeyboardMouse,
        }));
        LoadIcons();
        _save.Tick(new TickContext(_tick++, 1.0 / 60.0));

        view.KeepPage = true;
        view.ChosenSlot = 1;
        _save.OpenMenu();
        switch (page)
        {
            case "slots":
                view.Show(MenuPage.Slots, 0);
                break;
            case "choice":
                view.Show(MenuPage.Choice, 1);
                break;
            case "saved":
                view.Show(MenuPage.Main, 1);
                break;
            default:
                view.Show(MenuPage.Main, 0);
                break;
        }
    }

    /// <summary>
    /// In the game the Input view makes the icons loadable; this scene has no Input view, so it
    /// rasterizes the icons Godot has not imported itself.
    /// </summary>
    private static void LoadIcons()
    {
        foreach (var file in DirAccess.GetFilesAt(Prompts.GlyphDirectory))
        {
            string path = Prompts.GlyphDirectory + file;
            if (!file.EndsWith(".svg") || ResourceLoader.Exists(path))
                continue;
            var image = new Image();
            if (image.LoadSvgFromString(Godot.FileAccess.GetFileAsString(path), 2f) != Error.Ok)
                continue;
            var texture = ImageTexture.CreateFromImage(image);
            texture.TakeOverPath(path);
            s_icons.Add(texture);
        }
    }

    private void Write(SaveData data) => _save.Files!.Write(data);

    public override void _PhysicsProcess(double delta)
    {
        // The toast would fade before the screenshot: save again just before it is taken.
        if (OS.GetEnvironment("SAVE_PREVIEW").ToLowerInvariant() == "saved" && _tick == 60)
            _save.RequestSave(0, false);
        _save.Tick(new TickContext(_tick++, delta));
    }
}
