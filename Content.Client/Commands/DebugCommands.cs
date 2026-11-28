using Content.Client.Markers;
using Content.Client.Popups;
using Content.Client.SubFloor;
using Content.Shared.Eye; // Wormix
using Content.Shared.SubFloor;
using Robust.Shared.Console;

namespace Content.Client.Commands;

internal sealed partial class ShowMarkersCommand : LocalizedEntityCommands
{
    [Dependency] private MarkerSystem _markerSystem = default!;

    public override string Command => "showmarkers";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _markerSystem.MarkersVisible ^= true;
    }
}

internal sealed partial class ShowSubFloor : LocalizedEntityCommands
{
    [Dependency] private SubFloorHideSystem _subfloorSystem = default!;

    public override string Command => "showsubfloor";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // Wormix-start: no argument toggles everything, otherwise one category.
        if (args.Length == 0)
        {
            _subfloorSystem.ShowAll = !_subfloorSystem.ShowAll;
            return;
        }

        var layers = args[0].ToLowerInvariant() switch
        {
            "wires" => VisibilityFlags.SubfloorWires,
            "pipes" => VisibilityFlags.SubfloorPipes,
            "disposals" => VisibilityFlags.SubfloorDisposals,
            "all" => SubfloorCategories.All,
            _ => VisibilityFlags.None,
        };

        if (layers == VisibilityFlags.None)
        {
            shell.WriteError(Help);
            return;
        }

        if (layers == SubfloorCategories.All)
            _subfloorSystem.ShowAll = !_subfloorSystem.ShowAll;
        else
            _subfloorSystem.ToggleLayers(layers);
        // Wormix-end
    }

    // Wormix-start
    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return CompletionResult.FromOptions(new[] { "wires", "pipes", "disposals", "all" });
    }
    // Wormix-end
}

internal sealed partial class NotifyCommand : LocalizedEntityCommands
{
    [Dependency] private PopupSystem _popupSystem = default!;

    public override string Command => "notify";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _popupSystem.PopupCursor(args[0]);
    }
}
