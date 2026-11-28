using Content.Server.Administration.Managers;
using Content.Server.Atmos.Monitor.Components;
using Content.Shared.Administration;
using Content.Shared._Wormix.DeviceLinks;
using Content.Shared.DeviceConfigurator.Components;
using Content.Shared.DeviceLinking.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Wormix.DeviceLinks;

/// <summary>
/// Streams the device links of a viewer's map to the overlay. Rebuilt on a
/// short timer and cached per map so several viewers share one build.
/// </summary>
public sealed partial class DeviceLinksServerSystem : EntitySystem
{
    private const float SendInterval = 0.5f;

    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IPlayerManager _players = default!;

    private readonly HashSet<ICommonSession> _watchers = new();
    private readonly Dictionary<MapId, List<DeviceLinkInfo>> _cache = new();
    private readonly HashSet<(NetEntity Source, NetEntity Sink)> _seen = new();
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DeviceLinksToggleEvent>(OnToggle);
        _players.PlayerStatusChanged += OnPlayerStatus;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatus;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_watchers.Count == 0)
            return;

        _accumulator += frameTime;
        if (_accumulator < SendInterval)
            return;

        _accumulator = 0f;
        SendToAll();
    }

    private void OnToggle(DeviceLinksToggleEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (!ev.Enabled || !CanUse(session))
        {
            _watchers.Remove(session);
            return;
        }

        _watchers.Add(session);
        Send(session, MapOf(session));
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs ev)
    {
        if (ev.NewStatus == SessionStatus.Disconnected)
            _watchers.Remove(ev.Session);
    }

    private void SendToAll()
    {
        _cache.Clear();
        var stale = new List<ICommonSession>();

        foreach (var watcher in _watchers)
        {
            if (!CanUse(watcher))
            {
                stale.Add(watcher);
                continue;
            }

            var map = MapOf(watcher);
            if (!_cache.TryGetValue(map, out var links))
                _cache[map] = links = Build(map);

            Send(watcher, links);
        }

        foreach (var session in stale)
            _watchers.Remove(session);
    }

    private void Send(ICommonSession session, MapId map)
    {
        if (!_cache.TryGetValue(map, out var links))
            _cache[map] = links = Build(map);

        RaiseNetworkEvent(new DeviceLinksStateEvent { Links = links }, session);
    }

    private void Send(ICommonSession session, List<DeviceLinkInfo> links)
    {
        RaiseNetworkEvent(new DeviceLinksStateEvent { Links = links }, session);
    }

    private List<DeviceLinkInfo> Build(MapId map)
    {
        var links = new List<DeviceLinkInfo>();
        if (map == MapId.Nullspace)
            return links;

        _seen.Clear();

        var sources = EntityQueryEnumerator<DeviceLinkSourceComponent, TransformComponent>();
        while (sources.MoveNext(out var uid, out var source, out var xform))
        {
            if (!DeviceLinksMaps.OnMap(EntityManager, uid, xform, map))
                continue;

            var sourceNet = GetNetEntity(uid);
            var sourcePos = GetNetCoordinates(xform.Coordinates);

            foreach (var sink in source.LinkedPorts.Keys)
            {
                AddLink(links, sourceNet, sourcePos, sink, map, _seen);
            }
        }

        AddDeviceLists<AirAlarmComponent>(map, links);
        AddDeviceLists<FireAlarmComponent>(map, links);
        return links;
    }

    private void AddDeviceLists<TComponent>(MapId map, List<DeviceLinkInfo> links)
        where TComponent : IComponent
    {
        // Air and fire alarms keep their controlled devices in a device list;
        // only allow-lists describe an actual connection.
        var query = EntityQueryEnumerator<TComponent, DeviceListComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var list, out var xform))
        {
            if (!list.IsAllowList || list.Devices.Count == 0)
                continue;

            if (!DeviceLinksMaps.OnMap(EntityManager, uid, xform, map))
                continue;

            var sourceNet = GetNetEntity(uid);
            var sourcePos = GetNetCoordinates(xform.Coordinates);

            foreach (var device in list.Devices)
            {
                AddLink(links, sourceNet, sourcePos, device, map, _seen);
            }
        }
    }

    private void AddLink(
        List<DeviceLinkInfo> links,
        NetEntity sourceNet,
        NetCoordinates sourcePos,
        EntityUid sink,
        MapId map,
        HashSet<(NetEntity, NetEntity)> seen)
    {
        if (sink == EntityUid.Invalid ||
            !TryComp(sink, out TransformComponent? sinkXform) ||
            !DeviceLinksMaps.OnMap(EntityManager, sink, sinkXform, map))
            return;

        var sinkNet = GetNetEntity(sink);
        if (!seen.Add((sourceNet, sinkNet)))
            return;

        links.Add(new DeviceLinkInfo
        {
            Source = sourceNet,
            Sink = sinkNet,
            SourcePos = sourcePos,
            SinkPos = GetNetCoordinates(sinkXform.Coordinates),
        });
    }

    private MapId MapOf(ICommonSession session)
    {
        if (session.AttachedEntity is not { } uid ||
            !TryComp(uid, out TransformComponent? xform))
            return MapId.Nullspace;

        return DeviceLinksMaps.FindMap(EntityManager, uid, xform);
    }

    private bool CanUse(ICommonSession session)
    {
        return _admin.HasAdminFlag(session, AdminFlags.Admin)
               || _admin.HasAdminFlag(session, AdminFlags.Mapping)
               || _admin.HasAdminFlag(session, AdminFlags.Debug);
    }
}
