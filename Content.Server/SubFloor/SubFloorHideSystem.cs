using Content.Shared.Eye;
using Content.Shared.SubFloor;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.SubFloor;

public sealed partial class SubFloorHideSystem : SharedSubFloorHideSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    private readonly Dictionary<ICommonSession, VisibilityFlags> _shownLayers = new(); // Wormix

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ShowSubfloorRequestEvent>(OnShowSubfloor);
        SubscribeLocalEvent<GetVisMaskEvent>(OnGetVisibility);

        _player.PlayerStatusChanged += OnPlayerStatus;
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected)
            return;

        _shownLayers.Remove(e.Session); // Wormix

        if (e.Session.AttachedEntity != null)
            _eye.RefreshVisibilityMask(e.Session.AttachedEntity.Value);
    }

    private void OnGetVisibility(ref GetVisMaskEvent ev)
    {
        if (!TryComp(ev.Entity, out ActorComponent? actor))
            return;

        if (_shownLayers.TryGetValue(actor.PlayerSession, out var layers)) // Wormix
        {
            ev.VisibilityMask |= (int) layers;
        }
    }

    private void OnShowSubfloor(ShowSubfloorRequestEvent ev, EntitySessionEventArgs args)
    {
        // TODO: Commands are a bit of an eh? for client-only but checking shared perms
        var ent = args.SenderSession.AttachedEntity;

        if (!TryComp(ent, out EyeComponent? eyeComp))
            return;

        // Wormix-start: store the requested category mask per player.
        var mask = (VisibilityFlags) ev.Mask & SubfloorCategories.All;
        if (mask == VisibilityFlags.None)
            _shownLayers.Remove(args.SenderSession);
        else
            _shownLayers[args.SenderSession] = mask;
        // Wormix-end

        _eye.RefreshVisibilityMask((ent.Value, eyeComp));

        RaiseNetworkEvent(new ShowSubfloorRequestEvent()
        {
            Mask = (int) mask, // Wormix
        }, args.SenderSession);
    }
}
