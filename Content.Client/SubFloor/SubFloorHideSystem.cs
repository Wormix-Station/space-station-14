using Content.Client.UserInterface.Systems.Sandbox;
using Content.Shared.Eye;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.SubFloor;

public sealed partial class SubFloorHideSystem : SharedSubFloorHideSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private VisibilityFlags _shownLayers; // Wormix

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ShowAll
    {
        get => (_shownLayers & SubfloorCategories.All) == SubfloorCategories.All;
        // Wormix: the panel and the command work with individual category layers.
        set => SetLayers(value ? SubfloorCategories.All : VisibilityFlags.None);
    }

    // Wormix-start
    public VisibilityFlags ShownLayers => _shownLayers;

    public void ToggleLayers(VisibilityFlags layers)
    {
        layers &= SubfloorCategories.All;
        if (layers == VisibilityFlags.None)
            return;

        SetLayers((_shownLayers & layers) == layers
            ? _shownLayers & ~layers
            : _shownLayers | layers);
    }

    private void SetLayers(VisibilityFlags value)
    {
        // A category bit without its generic subfloor bit draws nothing, so keep them paired.
        value &= SubfloorCategories.All;
        var categories = VisibilityFlags.SubfloorWires | VisibilityFlags.SubfloorPipes | VisibilityFlags.SubfloorDisposals;
        if ((value & categories) == 0)
            value &= ~VisibilityFlags.Subfloor;

        if (_shownLayers == value)
            return;

        _shownLayers = value;
        _ui.GetUIController<SandboxUIController>().SetSubfloorState(value);

        RaiseNetworkEvent(new ShowSubfloorRequestEvent { Mask = (int) value });
    }

    private bool IsRevealed(SubFloorHideComponent component)
    {
        return (_shownLayers & SubfloorCategories.Mask(component.Category)) != 0;
    }
    // Wormix-end

    [SubscribeLocalEvent]
    private void OnPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        // Vismask resets so need to reset this.
        ShowAll = false;
    }

    [SubscribeNetworkEvent]
    private void OnRequestReceived(ShowSubfloorRequestEvent ev)
    {
        // When client receives request Queue an update on all vis.
        UpdateAll();
    }

    [SubscribeLocalEvent]
    private void OnAppearanceChanged(EntityUid uid, SubFloorHideComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        args.TryGetData<bool>(SubFloorVisuals.Covered, out var covered);
        args.TryGetData<bool>(SubFloorVisuals.ScannerRevealed, out var scannerRevealed);

        var shown = IsRevealed(component); // Wormix
        scannerRevealed &= !shown; // no transparency for show-subfloor mode. // Wormix

        var revealed = !covered || shown || scannerRevealed;

        // set visibility & color of each layer
        foreach (var layer in args.Sprite.AllLayers)
        {
            // pipe connection visuals are updated AFTER this, and may re-hide some layers
            layer.Visible = revealed;
        }

        // Is there some layer that is always visible?
        var hasVisibleLayer = false;
        foreach (var layerKey in component.VisibleLayers)
        {
            if (!_sprite.LayerMapTryGet((uid, args.Sprite), layerKey, out var layerIndex, false))
                continue;

            var layer = args.Sprite[layerIndex];
            layer.Visible = true;
            layer.Color = layer.Color.WithAlpha(1f);
            hasVisibleLayer = true;
        }

        _sprite.SetVisible((uid, args.Sprite), hasVisibleLayer || revealed);

        if (shown) // Wormix
        {
            // Allows sandbox mode to make wires visible over other stuff.
            component.OriginalDrawDepth ??= args.Sprite.DrawDepth;
            var drawDepthDifference = Shared.DrawDepth.DrawDepth.ThickPipe - (Shared.DrawDepth.DrawDepth.Overdoors + 1);
            _sprite.SetDrawDepth((uid, args.Sprite), args.Sprite.DrawDepth - drawDepthDifference);
        }
        else if (scannerRevealed)
        {
            // Allows a t-ray to show wires/pipes above walls.
            if (component.OriginalDrawDepth is not null)
                return;
            component.OriginalDrawDepth = args.Sprite.DrawDepth;
            var drawDepthDifference = Shared.DrawDepth.DrawDepth.ThickPipe - (Shared.DrawDepth.DrawDepth.Walls + 1);
            _sprite.SetDrawDepth((uid, args.Sprite), args.Sprite.DrawDepth - drawDepthDifference);
        }
        else if (component.OriginalDrawDepth.HasValue)
        {
            _sprite.SetDrawDepth((uid, args.Sprite), component.OriginalDrawDepth.Value);
            component.OriginalDrawDepth = null;
        }
    }

    private void UpdateAll()
    {
        var query = AllEntityQuery<SubFloorHideComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance))
        {
            _appearance.QueueUpdate(uid, appearance);
        }
    }
}
