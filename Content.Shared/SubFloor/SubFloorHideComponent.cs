using Content.Shared.Eye; // Wormix
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.SubFloor
{
    /// <summary>
    /// Simple component that automatically hides the sibling
    /// <see cref="SpriteComponent" /> when the tile it's on is not a sub floor
    /// (plating).
    /// </summary>
    /// <seealso cref="P:Content.Shared.Maps.ContentTileDefinition.IsSubFloor" />
    [NetworkedComponent]
    [RegisterComponent]
    [Access(typeof(SharedSubFloorHideSystem))]
    public sealed partial class SubFloorHideComponent : Component
    {
        /// <summary>
        ///     Whether the entity's current position has a "Floor-type" tile above its current position.
        /// </summary>
        [ViewVariables]
        public bool IsUnderCover { get; set; } = false;

        /// <summary>
        ///     Whether interactions with this entity should be blocked while it is under floor tiles.
        /// </summary>
        /// <remarks>
        ///     Useful for entities like vents, which are only partially hidden. Anchor attempts will still be blocked.
        /// </remarks>
        [DataField]
        public bool BlockInteractions { get; set; } = true;

        /// <summary>
        /// Whether this entity's ambience should be disabled when underneath the floor.
        /// </summary>
        /// <remarks>
        /// Useful for cables and piping, gives maint it's distinct noise.
        /// </remarks>
        [DataField]
        public bool BlockAmbience { get; set; } = true;

        /// <summary>
        ///     Sprite layer keys for the layers that are always visible, even if the entity is below a floor tile. E.g.,
        ///     the vent part of a vent is always visible, even though the piping is hidden.
        /// </summary>
        [DataField]
        public HashSet<Enum> VisibleLayers = new();

        /// <summary>
        /// This is used for storing the original draw depth of a t-ray revealed entity.
        /// e.g. when a t-ray revealed cable is drawn above a carpet.
        /// </summary>
        [DataField]
        public int? OriginalDrawDepth;

        /// <summary>
        /// Which mapper category this entity belongs to. While hidden, it is drawn on
        /// that category's visibility layer instead of the generic subfloor one.
        /// </summary>
        // Wormix
        [DataField]
        public SubfloorCategory Category = SubfloorCategory.None;
    }

    // Wormix-start
    /// <summary>
    /// Mapper-facing categories of things that live under the floor.
    /// </summary>
    [Serializable, NetSerializable]
    public enum SubfloorCategory : byte
    {
        None,
        Wires,
        Pipes,
        Disposals,
    }

    public static class SubfloorCategories
    {
        public const VisibilityFlags All =
            VisibilityFlags.Subfloor
            | VisibilityFlags.SubfloorWires
            | VisibilityFlags.SubfloorPipes
            | VisibilityFlags.SubfloorDisposals;

        public static VisibilityFlags Mask(SubfloorCategory category)
        {
            return category switch
            {
                SubfloorCategory.Wires => VisibilityFlags.SubfloorWires,
                SubfloorCategory.Pipes => VisibilityFlags.SubfloorPipes,
                SubfloorCategory.Disposals => VisibilityFlags.SubfloorDisposals,
                _ => VisibilityFlags.Subfloor,
            };
        }
    }
    // Wormix-end
}
