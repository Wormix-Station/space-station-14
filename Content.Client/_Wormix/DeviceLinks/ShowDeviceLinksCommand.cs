using Content.Shared._Wormix.DeviceLinks;
using Robust.Shared.Console;

namespace Content.Client._Wormix.DeviceLinks;

public sealed partial class ShowDeviceLinksCommand : LocalizedEntityCommands
{
    [Dependency] private DeviceLinksClientSystem _deviceLinks = default!;

    public override string Command => "showdevicelinks";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_deviceLinks.CanUse())
        {
            shell.WriteError(Loc.GetString("cmd-showdevicelinks-denied"));
            return;
        }

        _deviceLinks.Toggle();
        shell.WriteLine(Loc.GetString(_deviceLinks.Enabled
            ? "cmd-showdevicelinks-on"
            : "cmd-showdevicelinks-off"));
    }
}
