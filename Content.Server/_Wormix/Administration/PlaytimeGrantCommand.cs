using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Wormix.Administration.Commands;

[AdminCommand(AdminFlags.Playtime)]
public sealed partial class PlaytimeGrantCommand : LocalizedEntityCommands
{
    [Dependency] private EuiManager _eui = default!;

    public override string Command => "playtimegrantui";

    public override string Description => Loc.GetString("playtime-grant-command-description");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("playtime-grant-command-server-only"));
            return;
        }

        _eui.OpenEui(new PlaytimeGrantEui(), player);
    }
}
