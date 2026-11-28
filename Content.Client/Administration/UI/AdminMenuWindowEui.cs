using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        private readonly AdminAnnounceWindow _window;

        public AdminAnnounceEui()
        {
            _window = new AdminAnnounceWindow();
            _window.OnClose += () => SendMessage(new CloseEuiMessage());
            _window.AnnounceButton.OnPressed += OnAnnouncePressed;
        }

        // Wormix: send the extended announce data (target, color, sound, sender signature).
        private void OnAnnouncePressed(BaseButton.ButtonEventArgs args)
        {
            var destination = _window.Destination;

            SendMessage(new AdminAnnounceEuiMsg.DoAnnounce
            {
                Announcement = _window.AnnouncementText,
                Announcer = _window.AnnouncerText,
                AnnounceType = destination.Type,
                TargetGrid = destination.Grid,
                ColorHex = _window.ColorHex,
                SoundPath = _window.SoundPath,
                SoundVolume = _window.Volume,
                Sender = _window.SenderText,
                CloseAfter = !_window.KeepOpen,
            });
        }

        public override void Opened()
        {
            _window.OpenCentered();
        }

        public override void Closed()
        {
            _window.Close();
        }

        public override void HandleState(EuiStateBase state)
        {
            if (state is AdminAnnounceEuiState announceState)
                _window.SetTargets(announceState.Targets);
        }
    }
}
