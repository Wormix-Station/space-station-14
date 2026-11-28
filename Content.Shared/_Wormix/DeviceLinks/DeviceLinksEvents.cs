using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Wormix.DeviceLinks;

/// <summary>
/// Client asks the server to start or stop streaming device link data.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeviceLinksToggleEvent : EntityEventArgs
{
    public bool Enabled;
}

/// <summary>
/// Server push with the current links on the viewer's map.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeviceLinksStateEvent : EntityEventArgs
{
    public List<DeviceLinkInfo> Links = new();
}

[Serializable, NetSerializable]
public struct DeviceLinkInfo
{
    public NetEntity Source;
    public NetEntity Sink;

    // Stored positions cover devices that are hidden from the client (e.g. under subfloor).
    public NetCoordinates SourcePos;
    public NetCoordinates SinkPos;
}

public static class DeviceLinksMaps
{
    /// <summary>
    /// Mapping preview grids hang off nullspace maps; climb the parent chain
    /// until an ancestor carries its own map.
    /// </summary>
    public static MapId FindMap(IEntityManager entities, EntityUid uid, TransformComponent xform)
    {
        var mapId = xform.MapID;
        if (mapId != MapId.Nullspace)
            return mapId;

        var parent = xform.ParentUid;
        while (parent.IsValid())
        {
            if (entities.TryGetComponent(parent, out MapComponent? map))
                return map.MapId;

            if (!entities.TryGetComponent(parent, out TransformComponent? parentXform))
                break;

            parent = parentXform.ParentUid;
        }

        return MapId.Nullspace;
    }

    public static bool OnMap(IEntityManager entities, EntityUid uid, TransformComponent xform, MapId map)
    {
        if (map == MapId.Nullspace)
            return false;

        if (xform.MapID == map)
            return true;

        return xform.MapID == MapId.Nullspace && FindMap(entities, uid, xform) == map;
    }
}
