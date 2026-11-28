using Robust.Shared.Prototypes;

namespace Content.Shared._Wormix.Administration;

/// <summary>
/// Admin-only preset for pre-filling the central command announcement window.
/// </summary>
[Prototype]
public sealed partial class AnnouncePresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Text = string.Empty;
}
