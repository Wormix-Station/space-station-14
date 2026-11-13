using Content.Client.Examine;
using Content.Client.Popups;
using Content.Shared._Orion.Chat.Components;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._Orion.Chat.Systems.DirectionalEmote;

public sealed partial class DirectionalEmoteSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ExamineSystem _examineSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;

    private bool _directionalEmotesEnabled;
    private int _directionalEmoteRange;
    private int _maxEmoteLength;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.DirectionalEmotesEnabled, v => _directionalEmotesEnabled = v, true);
        Subs.CVar(_cfg, CCVars.DirectionalEmoteRange, v => _directionalEmoteRange = v, true);
        Subs.CVar(_cfg, CCVars.ChatMaxEmoteLength, v => _maxEmoteLength = v, true);

        SubscribeLocalEvent<DirectionalEmoteTargetComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    /// <summary>
    /// Checks the emote on the client and sends it to the server.
    /// </summary>
    /// <returns>False if the emote did not pass the checks and was not sent.</returns>
    public bool ShowMessage(NetEntity source, NetEntity target, string text, bool hideName = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.Length > _maxEmoteLength)
        {
            _popupSystem.PopupCursor(Loc.GetString("directional-emote-length-popup", ("maxLength", _maxEmoteLength)), PopupType.MediumCaution);
            return false;
        }

        if (!_examineSystem.InRangeUnOccluded(GetEntity(source), GetEntity(target), _directionalEmoteRange))
        {
            _popupSystem.PopupCursor(Loc.GetString("directional-emote-range-popup", ("range", _directionalEmoteRange)), PopupType.MediumCaution);
            return false;
        }

        RaiseNetworkEvent(new SendDirectionalEmoteEvent(source, target, text, hideName));
        return true;
    }

    private void OnGetVerbs(EntityUid uid, DirectionalEmoteTargetComponent component, GetVerbsEvent<Verb> args)
    {
        if (!_directionalEmotesEnabled)
            return;

        if (args.Target == args.User ||
            !component.CanReceiveEmotes ||
            !TryComp<DirectionalEmoteTargetComponent>(args.User, out var userEmote) ||
            !userEmote.CanSendEmotes)
            return;

        if (HasComp<GhostComponent>(args.User) && !HasComp<InventoryComponent>(args.User)) // Wormix edit
            return; // Who we lost can't speak to us.. But admin can

        if (!_examineSystem.InRangeUnOccluded(args.User, args.Target, _directionalEmoteRange))
            return;

        args.Verbs.Add(new Verb
        {
            Act = () => OpenWindow(GetNetEntity(args.User), GetNetEntity(args.Target)),
            Priority = 15,
            Text = Loc.GetString("directional-emote-verb-name"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/emotes.svg.192dpi.png")),
            ClientExclusive = true,
        });
    }

    private void OpenWindow(NetEntity source, NetEntity target)
    {
        _userInterfaceManager.GetUIController<DirectionalEmoteUIController>().ToggleWindow(source, target);
    }
}
