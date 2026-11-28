using System.Numerics;
using Content.Shared._Wormix.DeviceLinks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._Wormix.DeviceLinks;

/// <summary>
/// Draws a colored outline around every linked device and lines between
/// sources and their sinks.
/// </summary>
public sealed class DeviceLinksOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private const float MinSpriteSize = 0.05f;
    private const float FallbackSize = 0.8f;

    private readonly DeviceLinksClientSystem _system;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;
    private readonly HashSet<NetEntity> _outlined = new();

    public DeviceLinksOverlay(DeviceLinksClientSystem system, IEntityManager entities)
    {
        _system = system;
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _sprites = entities.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var links = _system.Links;
        if (links.Count == 0)
            return;

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        _outlined.Clear();

        foreach (var link in links)
        {
            if (!TryWorldPosition(link.Source, link.SourcePos, args.MapId, out var sourcePos) ||
                !TryWorldPosition(link.Sink, link.SinkPos, args.MapId, out var sinkPos))
                continue;

            // Cheap culling: build the box from the pair's center and span and
            // drop it when it cannot touch the viewport.
            var center = (sourcePos + sinkPos) / 2f;
            var half = Vector2.Abs(sourcePos - sinkPos) / 2f + Vector2.One * 2f;
            if (!args.WorldAABB.Intersects(Box2.CenteredAround(center, half * 2f)))
                continue;

            var color = LinkColor(link.Source);

            if (_outlined.Add(link.Source))
                DrawOutline(handle, link.Source, sourcePos, args, color);

            DrawOutline(handle, link.Sink, sinkPos, args, color);
            handle.DrawLine(sourcePos, sinkPos, color.WithAlpha(0.7f));
        }
    }

    private bool TryWorldPosition(NetEntity net, NetCoordinates fallback, MapId map, out Vector2 world)
    {
        world = default;

        // A live entity on the target map is authoritative; the stored
        // coordinates cover devices the client cannot see (under subfloor,
        // deleted between updates and so on).
        if (TryLivePosition(net, map, out world))
            return true;

        var coords = _entities.GetCoordinates(fallback);
        if (!coords.IsValid(_entities) ||
            !_entities.TryGetComponent(coords.EntityId, out TransformComponent? holder) ||
            !DeviceLinksMaps.OnMap(_entities, coords.EntityId, holder, map))
            return false;

        // The holder may be a nullspace-parented grid, so compute the world
        // position from the holder directly.
        world = holder.WorldPosition + holder.WorldRotation.ToVec() * coords.Position;
        return true;
    }

    private bool TryLivePosition(NetEntity net, MapId map, out Vector2 world)
    {
        world = default;

        if (!_entities.TryGetEntity(net, out var uid) ||
            !_entities.TryGetComponent(uid.Value, out TransformComponent? xform) ||
            !DeviceLinksMaps.OnMap(_entities, uid.Value, xform, map))
            return false;

        world = _transform.GetWorldPosition(uid.Value);
        return true;
    }

    private void DrawOutline(DrawingHandleWorld handle, NetEntity net, Vector2 position, in OverlayDrawArgs args, Color color)
    {
        var box = OutlineBox(net, position, args);
        handle.DrawRect(box, color.WithAlpha(0.2f), false);
    }

    private Box2Rotated OutlineBox(NetEntity net, Vector2 position, in OverlayDrawArgs args)
    {
        if (!_entities.TryGetEntity(net, out var uid) ||
            !_entities.TryGetComponent(uid.Value, out SpriteComponent? sprite) ||
            !sprite.Visible)
            return FallbackBox(position);

        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(uid.Value);
        var local = _sprites.GetLocalBounds((uid.Value, sprite));

        if (local.Size.X < MinSpriteSize || local.Size.Y < MinSpriteSize)
            return FallbackBox(position);

        return new Box2Rotated(local.Enlarged(1 / 32f), worldRot, worldPos);
    }

    private static Box2Rotated FallbackBox(Vector2 position)
    {
        return new Box2Rotated(Box2.CenteredAround(position, new Vector2(FallbackSize, FallbackSize)), Angle.Zero, position);
    }

    private static Color LinkColor(NetEntity source)
    {
        // Stable per-source hue so a device keeps its color between updates.
        var hue = source.Id % 100 / 100f;
        return Color.FromHsv(new Vector4(hue, 0.8f, 1f, 1f));
    }
}
