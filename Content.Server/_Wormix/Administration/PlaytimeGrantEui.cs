using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Wormix.Administration;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Wormix.Administration;

/// <summary>
/// Server side of the playtime grant window. Validates every entry and applies
/// deltas through the playtime manager.
/// </summary>
public sealed partial class PlaytimeGrantEui : BaseEui
{
    private const int MaxMinutes = 1_000_000;

    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PlayTimeTrackingManager _playtime = default!;

    private PlaytimeGrantEuiState _state = new();

    public PlaytimeGrantEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not PlaytimeGrantRequest request)
            return;

        if (!_admin.HasAdminFlag(Player, AdminFlags.Playtime))
        {
            Close();
            return;
        }

        if (!_players.TryGetSessionByUsername(request.Ckey.Trim(), out var session))
        {
            Reply(Loc.GetString("playtime-grant-no-player", ("ckey", request.Ckey)), false);
            return;
        }

        var set = request.Mode == PlaytimeGrantMode.Set;
        var applied = 0;
        var rejected = 0;
        var handled = new HashSet<string>();
        var overall = 0;

        // In add mode an explicit zero is a no-op, in set mode zero is a valid reset.
        if (request.OverallMinutes is { } overallMinutes && (set || overallMinutes != 0))
        {
            if (IsAcceptable(overallMinutes, set))
            {
                AdjustOverall(session, overallMinutes, set);
                applied++;
                overall = overallMinutes;
            }
            else
            {
                rejected++;
            }
        }

        foreach (var entry in request.Entries)
        {
            if (entry.Minutes == 0 && !set)
                continue;

            if (!IsAcceptable(entry.Minutes, set) ||
                !handled.Add(entry.Tracker) ||
                !_prototypes.HasIndex<PlayTimeTrackerPrototype>(entry.Tracker))
            {
                rejected++;
                continue;
            }

            AdjustTracker(session, entry.Tracker, entry.Minutes, set);
            applied++;
        }

        if (applied == 0)
        {
            Reply(Loc.GetString("playtime-grant-nothing"), false);
            return;
        }

        Reply(Loc.GetString(set ? "playtime-grant-status-set" : "playtime-grant-status-add",
                ("player", session.Name),
                ("overall", overall),
                ("count", applied),
                ("rejected", rejected)),
            true);
    }

    private static bool IsAcceptable(int minutes, bool set)
    {
        return set
            ? minutes is >= 0 and <= MaxMinutes
            : minutes is >= -MaxMinutes and <= MaxMinutes;
    }

    private void AdjustOverall(ICommonSession session, int minutes, bool set)
    {
        var delta = set
            ? TimeSpan.FromMinutes(minutes) - _playtime.GetOverallPlaytime(session)
            : TimeSpan.FromMinutes(minutes);

        if (delta != TimeSpan.Zero)
            _playtime.AddTimeToOverallPlaytime(session, delta);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{Player:actor} {(set ? "set" : "added")} {minutes} minutes of overall playtime for {session:subject} via playtime grant UI");
    }

    private void AdjustTracker(ICommonSession session, string tracker, int minutes, bool set)
    {
        var delta = set
            ? TimeSpan.FromMinutes(minutes) - _playtime.GetPlayTimeForTracker(session, tracker)
            : TimeSpan.FromMinutes(minutes);

        if (delta != TimeSpan.Zero)
            _playtime.AddTimeToTracker(session, tracker, delta);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{Player:actor} {(set ? "set" : "added")} {minutes} minutes of tracker {tracker} for {session:subject} via playtime grant UI");
    }

    private void Reply(string status, bool success)
    {
        _state = new PlaytimeGrantEuiState(status, success);
        StateDirty();
    }
}
