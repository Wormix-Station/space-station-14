using System.Linq;
using Content.Server.Jittering;
using Content.Server.Power.EntitySystems;
using Content.Shared.Bed.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Jittering;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Morgue.Components;
using Content.Shared.Pinpointer;
using Content.Shared.PowerCell;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    // Orion-Edit-Start
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private JitteringSystem _jitter = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private ContainerSystem _containerSystem = default!;
    // Orion-Edit-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
    }

    // Orion-Edit-Start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var component in EntityQuery<CrewMonitoringConsoleComponent>())
        {
            if (_gameTiming.CurTime < component.NextAlertTime)
                continue;

            if (!component.DoAlert)
                continue;

            var uid = component.Owner;

            if (!this.IsPowered(uid, EntityManager))
            {
                var hasCorpse = HasUnsecuredCorpse(component);
                if (hasCorpse)
                    RemCompDeferred<JitteringComponent>(uid);

                continue;
            }

            var hasUnsecuredCorpse = HasUnsecuredCorpse(component);
            TriggerAlert(uid, component, hasUnsecuredCorpse);
        }
    }

    private void TriggerAlert(EntityUid uid, CrewMonitoringConsoleComponent component, bool hasCorpse)
    {
        component.NextAlertTime = _gameTiming.CurTime + TimeSpan.FromSeconds(component.AlertTime);

        if (hasCorpse)
        {
            if (_light.TryGetLight(uid, out var light))
            {
                component.NormalLightColor ??= light.Color;
                component.NormalLightEnergy ??= light.Energy;
                component.NormalLightRadius ??= light.Radius;

                _light.SetColor(uid, Color.Red, light);
                _light.SetEnergy(uid, 40, light);
                _light.SetRadius(uid, 1.5f, light);
            }

            _audio.PlayPvs(component.AlertSound, uid, component.AlertAudioParams);
            _jitter.AddJitter(uid, 10, 15);
        }
        else
        {
            if (_light.TryGetLight(uid, out var light))
            {
                if (component.NormalLightColor != null)
                    _light.SetColor(uid, component.NormalLightColor.Value, light);
                if (component.NormalLightEnergy != null)
                    _light.SetEnergy(uid, component.NormalLightEnergy.Value, light);
                if (component.NormalLightRadius != null)
                    _light.SetRadius(uid, component.NormalLightRadius.Value, light);
            }

            RemCompDeferred<JitteringComponent>(uid);
        }
    }

    private bool HasUnsecuredCorpse(CrewMonitoringConsoleComponent component)
    {
        // Check for corpses with coordinates sensor mode
        foreach (var sensor in component.ConnectedSensors.Values.Where(sensor => sensor is { IsAlive: false, Coordinates: not null }))
        {
            if (!TryGetEntity(sensor.OwnerUid, out var corpse) || Deleted(corpse.Value))
                continue;

            if (!IsCorpseSecured(corpse.Value))
                return true;
        }

        return false;
    }

    private bool IsCorpseSecured(EntityUid entity)
    {
        // If secured in a morgue - secured
        if (_containerSystem.TryGetContainingContainer(entity, out var container) && HasComp<MorgueComponent>(container.Owner))
            return true;

        // If buckled in a stasis bed - secured
        if (HasComp<StasisBedBuckledComponent>(entity))
            return true;

        return false;
    }
    // Orion-Edit-End

    private void OnRemove(EntityUid uid, CrewMonitoringConsoleComponent component, ComponentRemove args)
    {
        component.ConnectedSensors.Clear();
    }

    [SubscribeLocalEvent]
    private void OnSuitSensorBroadcast(Entity<CrewMonitoringConsoleComponent> ent, ref DeviceNetworkPacketEvent<BroadcastSuitSensorStatePayload> args)
    {
        ent.Comp.ConnectedSensors = args.Data.SensorStatus;
        UpdateUserInterface(ent, ent.Comp);
    }

    private void OnUIOpened(EntityUid uid, CrewMonitoringConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, CrewMonitoringConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_uiSystem.IsUiOpen(uid, CrewMonitoringUIKey.Key))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        var xform = Transform(uid);

        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        // Update all sensors info
        var allSensors = component.ConnectedSensors.Values.ToList();
        _uiSystem.SetUiState(uid, CrewMonitoringUIKey.Key, new CrewMonitoringState(allSensors));
    }
}
