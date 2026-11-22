using Robust.Shared.Prototypes;

namespace Content.Shared._TP.Maps
{
    /// <summary>
    /// Marks a grid as managing its roofs via roof marker entities (see TP14RoofCacheSystem)
    /// instead of an implicit roof. Ported from Trieste Port-14.
    /// </summary>
    [RegisterComponent]
    public sealed partial class TriesteComponent : Component
    {
    }
}
