using Content.Shared.Eui;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Wormix.Administration;

/// <summary>
/// Admin-only preset for pre-filling the ban panel reason field.
/// </summary>
[Prototype]
public sealed partial class BanPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Reason = string.Empty;

    /// <summary>
    /// When true, choosing this preset submits an admin watchlist remark instead of a ban.
    /// </summary>
    [DataField]
    public bool Watchlist;
}

/// <summary>
/// Client asks the server to add a watchlist remark (upstream note API) from the ban panel.
/// </summary>
[Serializable, NetSerializable]
public sealed class CreateWatchlistBanRequest(string playerUsername, string reason) : EuiMessageBase
{
    public string PlayerUsername { get; } = playerUsername;
    public string Reason { get; } = reason;
}
