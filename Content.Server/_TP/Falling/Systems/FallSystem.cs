using System.Linq;
using Content.Server._TP.Falling.Components;
using Content.Server._TP.Falling.Events;
using Content.Server._TP.Ladder;
using Content.Server.Popups;
using Content.Shared._TP.Maps;
using Content.Shared._TP.Movement.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Revenant.Components;
using Content.Shared.Salvage.Fulton;
using Content.Shared.Shuttles.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Stunnable;
using Robust.Shared.Map;

namespace Content.Server._TP.Falling.Systems;

/// <summary>
/// Makes entities fall to the seafloor destination when they leave a grid in
/// Trieste's open airspace: stepping off, jump-landing, spawning in the air,
/// or losing a fulton exemption.
/// </summary>
public sealed partial class FallSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private readonly List<(EntityUid, FallSystemComponent)> _toFall = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FallSystemComponent, EntParentChangedMessage>(OnEntParentChanged);
        SubscribeLocalEvent<FallCheckEvent>(OnFallCheck);
        SubscribeLocalEvent<FallSystemComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FultonedComponent, ComponentRemove>(OnFultonedRemove);
    }

    private void OnFallCheck(FallCheckEvent args)
    {
        if (!TryComp<FallSystemComponent>(args.Entity, out var fallComp) || !ShouldFall(args.Entity))
            return;

        if (TryComp<ClimbingComponent>(args.Entity, out var climbComp) && climbComp.IsClimbing)
            _climb.StopClimb(args.Entity, climbComp);

        HandleFall(args.Entity, fallComp);
    }

    /// <summary>
    /// Covers entities spawned or loaded straight into the air, which the parent change skips.
    /// </summary>
    private void OnMapInit(EntityUid uid, FallSystemComponent component, MapInitEvent args)
    {
        var xform = Transform(uid);
        if (xform.GridUid == null && HasComp<TriesteComponent>(xform.ParentUid))
            QueueLocalEvent(new FallCheckEvent(uid));
    }

    private void OnFultonedRemove(EntityUid uid, FultonedComponent component, ComponentRemove args) => QueueFallCheck(uid);

    /// <summary>
    /// Re-checks at the end of the tick, once the fulton exemption is fully gone.
    /// </summary>
    private void QueueFallCheck(EntityUid uid)
    {
        if (HasComp<FallSystemComponent>(uid) && !TerminatingOrDeleted(uid))
            QueueLocalEvent(new FallCheckEvent(uid));
    }

    private bool ShouldFall(EntityUid uid)
    {
        return !TerminatingOrDeleted(uid) &&
               _transformSystem.GetGrid(uid) == null &&
               HasComp<TriesteComponent>(_transformSystem.GetParentUid(uid)) &&
               !ExemptFromFalling(uid);
    }

    public override void Update(float frameTime)
    {
        // Catch jump-landings: a JumpingComponent holder that stopped jumping
        // while still parented to the Trieste map (i.e. in open airspace) falls.
        var jumpingQuery = EntityQueryEnumerator<JumpingComponent>();
        while (jumpingQuery.MoveNext(out var uid, out var jumpComp))
        {
            if (_transformSystem.GetGrid(uid) != null)
                continue;

            if (HasComp<TriesteComponent>(_transformSystem.GetParentUid(uid)) &&
                jumpComp is { IsJumping: false, WasJumping: true } &&
                TryComp<FallSystemComponent>(uid, out var fallSystemComponent))
            {
                _toFall.Add((uid, fallSystemComponent));
            }
        }

        foreach (var (uid, fallComp) in _toFall)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            // Force stop climbing if they're in airspace
            if (TryComp<ClimbingComponent>(uid, out var climbComp) && climbComp.IsClimbing)
                _climb.StopClimb(uid, climbComp);

            HandleFall(uid, fallComp);
        }

        _toFall.Clear();
    }

    private void OnEntParentChanged(EntityUid owner, FallSystemComponent component, EntParentChangedMessage args)
    {
        // Skip entities that are not fully initialized yet, they can't be parented to the map properly.
        if (MetaData(owner).EntityLifeStage < EntityLifeStage.MapInitialized)
        {
            return;
        }

        // A check to see if the player jumped from one grid to
        // another, and if so, return so they don't fall.
        if (args.OldParent == null || args.Transform.GridUid != null ||
            TerminatingOrDeleted(owner))
        {
            return;
        }

        if (ExemptFromFalling(owner))
            return;

        var ownerParent = Transform(owner).ParentUid;
        if (!HasComp<TriesteComponent>(ownerParent))
        {
            return;
        }

        // Force stop climbing when entering airspace via parent change
        if (TryComp<ClimbingComponent>(owner, out var climbComp) && climbComp.IsClimbing)
        {
            _climb.StopClimb(owner, climbComp);
        }

        HandleFall(owner, component);
    }

    /// <summary>
    ///     A method of different components that are exempt from falling.
    /// </summary>
    /// <param name="owner">Entity UID</param>
    /// <returns>Returns true if the comp exists, otherwise returns false</returns>
    private bool ExemptFromFalling(EntityUid owner)
    {
        return HasComp<FultonedComponent>(owner)
               || HasComp<GhostComponent>(owner)
               || HasComp<NoFTLComponent>(owner)
               || HasComp<CanMoveInAirComponent>(owner)
               || HasComp<RevenantComponent>(owner)
               || HasComp<JumpingComponent>(owner)
               || HasComp<StationAiHeldComponent>(owner);
    }

    private void HandleFall(EntityUid owner, FallSystemComponent component)
    {
        var destination = EntityQuery<FallingDestinationComponent>().FirstOrDefault();
        if (destination == null)
        {
            // If there's no destination, something broke
            Log.Error("No valid falling sites available!");
            return;
        }

        // Trieste and the destination map are offset in any given direction, so
        // we use the ladders on both maps as reference points and carry the
        // entity's offset from the source ladder over to the destination ladder.
        var sourceLadder = EntityQuery<LadderComponent>()
            .FirstOrDefault(ladder => _transformSystem.GetMapCoordinates(ladder.Owner).MapId ==
                                      _transformSystem.GetMapCoordinates(owner).MapId);

        var destLadder = EntityQuery<LadderComponent>()
            .FirstOrDefault(ladder => _transformSystem.GetMapCoordinates(ladder.Owner).MapId ==
                                      _transformSystem.GetMapCoordinates(destination.Owner).MapId);

        if (sourceLadder == null || destLadder == null)
        {
            Log.Error("Could not find ladders for fall calculation!");
            return;
        }

        var ownerCoords = _transformSystem.GetMapCoordinates(owner);
        var sourceLadderCoords = _transformSystem.GetMapCoordinates(sourceLadder.Owner);
        var destLadderCoords = _transformSystem.GetMapCoordinates(destLadder.Owner);

        var ladderOffset = destLadderCoords.Position - sourceLadderCoords.Position;
        var newPosition = ownerCoords.Position + ladderOffset;
        var newCoords = new MapCoordinates(newPosition, destLadderCoords.MapId);
        _transformSystem.SetMapCoordinates(owner, newCoords);

        // Stun the fall-ee for five seconds and apply EIGHTY blunt damage,
        // then announce that something fell.
        var stunTime = TimeSpan.FromSeconds(5);
        _stun.TryKnockdown(owner, stunTime, refresh: true);
        _stun.TryAddStunDuration(owner, stunTime);

        var damage = new DamageSpecifier
        {
            DamageDict = { ["Blunt"] = 80f }
        };
        _damageable.TryChangeDamage(owner, damage, origin: owner);
        _popup.PopupEntity(Loc.GetString("fell-to-seafloor"), owner, PopupType.LargeCaution);

        // Raise event for fall monitoring systems if player has sensors on.
        if (_inventory.TryGetSlotEntity(owner, "jumpsuit", out var suitEntity)
            && TryComp<SuitSensorComponent>(suitEntity, out var suitSensorComponent)
            && suitSensorComponent.Mode >= SuitSensorMode.SensorVitals)
        {
            var fallDetectedEv = new FallDetectedEvent(ownerCoords);
            RaiseLocalEvent(owner, ref fallDetectedEv);
        }
    }
}
