using Content.Server._TP.Falling.Components;
using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._TP.Maps;
using Content.Shared.Pinpointer;
using Content.Shared.Radio.Components;

namespace Content.Server._TP.Falling.FallMonitor;

/// <summary>
/// Broadcasts a radio alert when a fall is detected by suit sensors.
/// </summary>
public sealed partial class FallMonitorSystem : EntitySystem
{
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private NavMapSystem _navMap = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FallSystemComponent, FallDetectedEvent>(OnFallDetected);
    }

    private void OnFallDetected(Entity<FallSystemComponent> ent, ref FallDetectedEvent args)
    {
        var monitors = EntityQueryEnumerator<FallMonitorComponent, ApcPowerReceiverComponent>();
        while (monitors.MoveNext(out var uid, out _, out var power))
        {
            if (!power.Powered || !HasComp<TriesteComponent>(Transform(uid).ParentUid))
                continue;

            var positionName = _navMap.GetNearestBeaconString(args.DropLocation, true);
            if (!TryComp<EncryptionKeyHolderComponent>(uid, out var keys))
                continue;

            foreach (var channel in keys.Channels)
            {
                _radio.SendRadioMessage(uid,
                    Loc.GetString("fall-monitor-radio-alert", ("ent", ent), ("location", positionName)),
                    channel,
                    uid);
            }
        }
    }
}
