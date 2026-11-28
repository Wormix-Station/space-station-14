using System.Numerics;
using Content.Client._ADT.SpawnMenu; // ADT-Tweak
using Content.Client.Administration.Managers;
using Content.Client.Gameplay;
using Content.Client.Sandbox;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.DecalPlacer;
using Content.Client.UserInterface.Systems.Sandbox.Windows;
using Content.Shared.Eye; // Wormix
using Content.Shared.SubFloor; // Wormix
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controllers.Implementations;
using Robust.Shared.Console;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.UserInterface.Systems.Sandbox;

// TODO hud refactor should part of this be in engine?
[UsedImplicitly]
public sealed partial class SandboxUIController : UIController, IOnStateChanged<GameplayState>, IOnSystemChanged<SandboxSystem>
{
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IPlayerManager _player = default!;

    [UISystemDependency] private readonly SandboxSystem _sandbox = default!;

    private SandboxWindow? _window;

    // TODO hud refactor cache
    private EntitySpawningUIController EntitySpawningController => UIManager.GetUIController<EntitySpawningUIController>();
    private TileSpawningUIController TileSpawningController => UIManager.GetUIController<TileSpawningUIController>();
    private DecalPlacerUIController DecalPlacerController => UIManager.GetUIController<DecalPlacerUIController>();
    // ADT-Tweak-Start
    private ADTEntitySpawningUIController ADTEntitySpawningController => UIManager.GetUIController<ADTEntitySpawningUIController>();
    private ADTTileSpawningUIController ADTTileSpawningController => UIManager.GetUIController<ADTTileSpawningUIController>();
    private ADTDecalSpawningUIController ADTDecalSpawningController => UIManager.GetUIController<ADTDecalSpawningUIController>();
    // ADT-Tweak-End

    private MenuButton? SandboxButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.SandboxButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);
        EnsureWindow();

        CheckSandboxVisibility();

        _input.SetInputCommand(ContentKeyFunctions.OpenEntitySpawnWindow,
            InputCmdHandler.FromDelegate(_ =>
            {
                if (!_admin.CanAdminPlace())
                    return;
                ADTEntitySpawningController.ToggleWindow(); // ADT-Tweak
            }));
        _input.SetInputCommand(ContentKeyFunctions.OpenSandboxWindow,
            InputCmdHandler.FromDelegate(_ => ToggleWindow()));
        _input.SetInputCommand(ContentKeyFunctions.OpenTileSpawnWindow,
            InputCmdHandler.FromDelegate(_ =>
            {
                if (!_admin.CanAdminPlace())
                    return;
                ADTTileSpawningController.ToggleWindow(); // ADT-Tweak
            }));
        _input.SetInputCommand(ContentKeyFunctions.OpenDecalSpawnWindow,
            InputCmdHandler.FromDelegate(_ =>
            {
                if (!_admin.CanAdminPlace())
                    return;
                ADTDecalSpawningController.ToggleWindow(); // ADT-Tweak
            }));

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.EditorCopyObject, new PointerInputCmdHandler(Copy))
            .Register<SandboxSystem>();
    }

    public void UnloadButton()
    {
        if (SandboxButton == null)
        {
            return;
        }

        SandboxButton.OnPressed -= SandboxButtonPressed;
    }

    public void LoadButton()
    {
        if (SandboxButton == null)
        {
            return;
        }

        SandboxButton.OnPressed += SandboxButtonPressed;
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;
        _window = UIManager.CreateWindow<SandboxWindow>();
        // Pre-center the window without forcing it to the center every time.
        _window.OpenCentered();
        _window.Close();

        _window.OnOpen += () => { SandboxButton!.Pressed = true; };
        _window.OnClose += () => { SandboxButton!.Pressed = false; };

        _window.AiOverlayButton.OnPressed += args =>
        {
            var player = _player.LocalEntity;

            if (player == null)
                return;

            var pnent = EntityManager.GetNetEntity(player.Value);

            // Need NetworkedAddComponent but engine PR.
            if (args.Button.Pressed)
                _console.ExecuteCommand($"addcomp {pnent.Id} StationAiOverlay");
            else
                _console.ExecuteCommand($"rmcomp {pnent.Id} StationAiOverlay");
        };
        _window.RespawnButton.OnPressed += _ => _sandbox.Respawn();
        // ADT-Tweak-Start
        _window.SpawnTilesButton.OnPressed += _ => ADTTileSpawningController.ToggleWindow();
        _window.SpawnEntitiesButton.OnPressed += _ => ADTEntitySpawningController.ToggleWindow();
        _window.SpawnDecalsButton.OnPressed += _ => ADTDecalSpawningController.ToggleWindow();
        // ADT-Tweak-End
        _window.GiveFullAccessButton.OnPressed += _ => _sandbox.GiveAdminAccess();
        _window.GiveAghostButton.OnPressed += _ => _sandbox.GiveAGhost();
        _window.ToggleLightButton.OnToggled += _ => _sandbox.ToggleLight();
        _window.ToggleFovButton.OnToggled += _ => _sandbox.ToggleFov();
        _window.ToggleShadowsButton.OnToggled += _ => _sandbox.ToggleShadows();
        _window.SuicideButton.OnPressed += _ => _sandbox.Suicide();
        _window.ToggleSubfloorButton.OnPressed += _ => _sandbox.ToggleSubFloor();
        // Wormix-start
        _window.ShowWiresButton.OnPressed += _ => _sandbox.ToggleSubfloorCategory("wires");
        _window.ShowPipesButton.OnPressed += _ => _sandbox.ToggleSubfloorCategory("pipes");
        _window.ShowDisposalsButton.OnPressed += _ => _sandbox.ToggleSubfloorCategory("disposals");
        _window.ShowDeviceLinksButton.OnPressed += _ =>
        {
            _window.ShowDeviceLinksButton.Pressed = _sandbox.ToggleDeviceLinks();
        };
        // Wormix-end
        _window.ShowMarkersButton.OnPressed += _ => _sandbox.ShowMarkers();
        _window.ShowBbButton.OnPressed += _ => _sandbox.ShowBb();
        _window.ToggleThermalVisionButton.OnToggled += _ => _sandbox.ToggleThermalVision();
    }

    private void CheckSandboxVisibility()
    {
        if (SandboxButton == null)
            return;

        SandboxButton.Visible = _sandbox.SandboxAllowed;
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.Close();
            _window = null;
        }

        CommandBinds.Unregister<SandboxSystem>();
    }

    public void OnSystemLoaded(SandboxSystem system)
    {
        system.SandboxDisabled += CloseAll;
        system.SandboxEnabled += CheckSandboxVisibility;
        system.SandboxDisabled += CheckSandboxVisibility;
    }

    public void OnSystemUnloaded(SandboxSystem system)
    {
        system.SandboxDisabled -= CloseAll;
        system.SandboxEnabled -= CheckSandboxVisibility;
        system.SandboxDisabled -= CheckSandboxVisibility;
    }

    private void SandboxButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void CloseAll()
    {
        _window?.Close();
        EntitySpawningController.CloseWindow();
        // ADT-Tweak-Start
        ADTEntitySpawningController.CloseWindow();
        ADTTileSpawningController.CloseWindow();
        ADTDecalSpawningController.CloseWindow();
        // ADT-Tweak-End
        TileSpawningController.CloseWindow();
    }

    private bool Copy(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        return _sandbox.Copy(session, coords, uid);
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;
        if (_sandbox.SandboxAllowed && _window.IsOpen != true)
        {
            UIManager.ClickSound();
            _window.Open();
        }
        else
        {
            UIManager.ClickSound();
            _window.Close();
        }
    }

    #region Buttons

    // Wormix-start
    public void SetSubfloorState(VisibilityFlags layers)
    {
        if (_window == null)
            return;

        _window.ToggleSubfloorButton.Pressed = (layers & SubfloorCategories.All) == SubfloorCategories.All;
        _window.ShowWiresButton.Pressed = (layers & VisibilityFlags.SubfloorWires) != 0;
        _window.ShowPipesButton.Pressed = (layers & VisibilityFlags.SubfloorPipes) != 0;
        _window.ShowDisposalsButton.Pressed = (layers & VisibilityFlags.SubfloorDisposals) != 0;
    }
    // Wormix-end

    #endregion
}
