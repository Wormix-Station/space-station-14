using Content.Client.Chat.TypingIndicator;
using Content.Shared._Orion.Chat.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Orion.Chat.Systems.DirectionalEmote;

[UsedImplicitly]
public sealed partial class DirectionalEmoteUIController : UIController
{
    [UISystemDependency] private readonly DirectionalEmoteSystem _directionalEmoteSystem = default!;
    [UISystemDependency] private readonly TypingIndicatorSystem _typingIndicator = default!;

    private DirectionalEmoteWindow? _emoteWindow;

    /// <summary>
    /// The last emote successfully sent from this client.
    /// Kept on the client so the text is never networked to anyone else.
    /// </summary>
    private string _lastEmote = string.Empty;

    public void OpenWindow(NetEntity source, NetEntity target)
    {
        var window = EnsureWindow();

        window.Source = source;
        window.Target = target;
        window.SetText(string.Empty);

        var targetUid = EntityManager.GetEntity(target);
        var targetName = EntityManager.TryGetComponent<MetaDataComponent>(targetUid, out var targetMeta)
            ? targetMeta.EntityName
            : Loc.GetString("directional-emote-unknown-target");
        window.Title = Loc.GetString("directional-emote-title", ("target", targetName));

        EntityManager.TryGetComponent<DirectionalEmoteTargetComponent>(EntityManager.GetEntity(source), out var sourceEmote);
        window.UpdateHideNameVisibility(sourceEmote?.CanHideName ?? false);

        window.OpenCentered();
        window.MoveToFront();

        _typingIndicator.ClientChangedWindowStatus(true);
    }

    public void ToggleWindow(NetEntity source, NetEntity emoteTarget)
    {
        if (_emoteWindow is { IsOpen: true })
        {
            _emoteWindow.Close();
            return;
        }

        OpenWindow(source, emoteTarget);
    }

    private DirectionalEmoteWindow EnsureWindow()
    {
        if (_emoteWindow is { Disposed: false })
            return _emoteWindow;

        var window = UIManager.CreateWindow<DirectionalEmoteWindow>();

        window.MessageChanged += () =>
        {
            _typingIndicator.ClientChangedChatText();
        };

        window.AcceptPressed += () =>
        {
            if (!_directionalEmoteSystem.ShowMessage(window.Source, window.Target, window.Text, window.HideName))
                return;

            _lastEmote = window.Text;
            _typingIndicator.ClientSubmittedChatText();
            window.Close();
        };

        window.LastEmotePressed += () =>
        {
            window.SetText(_lastEmote);
        };

        // Wormix-start
        // The typing indicator is reset on any close of the window, not only on the close button.
        window.OnClose += () =>
        {
            _typingIndicator.ClientChangedWindowStatus(false);
        };
        // Wormix-end

        _emoteWindow = window;
        return window;
    }
}
