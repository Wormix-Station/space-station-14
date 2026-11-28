using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Wormix.Administration;

[Serializable, NetSerializable]
public enum PlaytimeGrantMode : byte
{
    Add,
    Set,
}

[Serializable, NetSerializable]
public readonly record struct PlaytimeGrantEntry(string Tracker, int Minutes);

[Serializable, NetSerializable]
public sealed class PlaytimeGrantEuiState : EuiStateBase
{
    public readonly string? Status;
    public readonly bool Success;

    public PlaytimeGrantEuiState(string? status = null, bool success = false)
    {
        Status = status;
        Success = success;
    }
}

[Serializable, NetSerializable]
public sealed class PlaytimeGrantRequest : EuiMessageBase
{
    public string Ckey = default!;
    public PlaytimeGrantMode Mode;
    public int? OverallMinutes;
    public List<PlaytimeGrantEntry> Entries = new();
}
