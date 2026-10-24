using System;
using Godot;

namespace Fungtopia.Wallet.View;

/// <summary>
/// A stand-alone scene showing coin bursts on a patch of grass, for screenshots:
/// <c>./screenshot.sh modules/Wallet/Godot/Preview.tscn</c>. Three bursts start a moment apart
/// (a 50 M¢, a 200 M¢ and a 50 M¢), so the default 90-frame screenshot catches them at
/// different points of their flight. The environment variable WALLET_PREVIEW = "close" shows one
/// coin up close instead.
/// </summary>
public partial class WalletPreview : Node3D
{
    private static readonly float[] Starts = { 0.6f, 0.9f, 1.15f };
    private static readonly int[] Amounts = { 50, 200, 50 };
    private WalletView _view = null!;
    private float _time;
    private int _started;
    private bool _close;

    public override void _Ready()
    {
        _close = OS.GetEnvironment("WALLET_PREVIEW").Equals("close", StringComparison.OrdinalIgnoreCase);
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.62f, 0.78f, 0.95f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.62f, 0.55f, 0.72f),
                AmbientLightEnergy = 0.9f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapWhite = 6f,
            },
        });
        AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1.0f, 0.88f, 0.7f),
            LightEnergy = 1.3f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-40f, -30f, 0f),
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 7f, BottomRadius = 7f, Height = 0.1f },
            Position = new Vector3(0f, -0.05f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.74f, 0.33f) },
        });

        var camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(camera);
        _view = new WalletView();
        AddChild(_view);
        if (_close)
        {
            var coin = CoinBurst.CreateCoin();
            AddChild(coin);
            coin.Transform = new Transform3D(new Basis(Vector3.Up, 0.35f).Scaled(new Vector3(3f, 3f, 3f)), new Vector3(0f, 0.6f, 0f));
            camera.LookAtFromPosition(new Vector3(0f, 0.9f, 2.2f), new Vector3(0f, 0.6f, 0f), Vector3.Up);
        }
        else
        {
            camera.LookAtFromPosition(new Vector3(0f, 2.2f, 7.4f), new Vector3(0f, 1.2f, 0f), Vector3.Up);
        }
    }

    public override void _Process(double delta)
    {
        if (_close)
            return;
        _time += (float)delta;
        while (_started < Starts.Length && _time >= Starts[_started])
        {
            _view.Play(new Vector3((_started - 1) * 2.4f, 0f, 0f), Amounts[_started], _started);
            _started++;
        }
    }
}
