using Robust.Shared.Map;

namespace Content.Server._TP.Falling.Components;

/// <summary>
/// Marks an entity as able to fall off the station map into open air.
/// Attached by the base mob/structure/item prototypes.
/// </summary>
[RegisterComponent]
public sealed partial class FallSystemComponent : Component;

/// <summary>
/// Raised directed at an entity that just fell, with the location it fell from.
/// Used by the fall monitor to radio out the last known position.
/// </summary>
[ByRefEvent]
public record struct FallDetectedEvent(MapCoordinates DropLocation);
