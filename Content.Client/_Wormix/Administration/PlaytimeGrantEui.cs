using Content.Client.Eui;
using Content.Shared._Wormix.Administration;
using Content.Shared.Eui;

namespace Content.Client._Wormix.Administration.UI;

public sealed class PlaytimeGrantEui : BaseEui
{
    private readonly PlaytimeGrantWindow _window;

    public PlaytimeGrantEui()
    {
        _window = new PlaytimeGrantWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.Submitted += (ckey, mode, overall, entries) =>
            SendMessage(new PlaytimeGrantRequest
            {
                Ckey = ckey,
                Mode = mode,
                OverallMinutes = overall,
                Entries = entries,
            });
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is PlaytimeGrantEuiState grantState)
            _window.SetStatus(grantState.Status, grantState.Success);
    }
}
