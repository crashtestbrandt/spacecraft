// Lobby panel (host/join/ready/stop) and the in-match top bar (phase, per-player ship counts,
// result). Built in code; Main wires the button events.
using System;
using global::Godot;
using Spacecraft.Sim;

namespace Spacecraft.View
{
    public partial class Hud : Control
    {
        public event Action HostClicked, JoinClicked, ReadyClicked, StopClicked;

        public string Address => _addr.Text.Trim();
        public int    Port    => int.TryParse(_port.Text.Trim(), out int p) ? p : 7777;

        public void SetAddress(string address) => _addr.Text = address;
        public void SetPort(int port) => _port.Text = port.ToString();

        PanelContainer _lobby;
        LineEdit _addr, _port;
        Button _host, _join, _ready, _stop;
        Label _status, _hint;
        Label[] _armies;
        Label _result;
        readonly int[] _counts = new int[Rules.MaxPlayers];

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.FullRect);

            // Top bar.
            var bar = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) };
            bar.SetAnchorsPreset(LayoutPreset.TopWide);
            bar.OffsetBottom = 24f;
            bar.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(bar);

            var row = new HBoxContainer { Position = new Vector2(6f, 4f) };
            row.AddThemeConstantOverride("separation", 14);
            row.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(row);
            _status = Small(row, "Idle", new Color(0.85f, 0.85f, 0.9f));
            _armies = new Label[Rules.MaxPlayers];
            for (int i = 0; i < Rules.MaxPlayers; i++)
            {
                _armies[i] = Small(row, "", Palette.Of(i));
                _armies[i].Visible = false;
            }

            _hint = Small(this, "drag: select   right-click: move   ctrl+a: all", new Color(1f, 1f, 1f, 0.35f));
            _hint.Position = new Vector2(6f, Rules.ArenaHeight - 16f);
            _hint.Visible = false;

            _result = Small(this, "", new Color(1f, 1f, 1f));
            _result.AddThemeFontSizeOverride("font_size", 20);
            _result.SetAnchorsPreset(LayoutPreset.Center);
            _result.GrowHorizontal = GrowDirection.Both;
            _result.GrowVertical = GrowDirection.Both;
            _result.HorizontalAlignment = HorizontalAlignment.Center;
            _result.Visible = false;

            // Lobby panel.
            _lobby = new PanelContainer();
            _lobby.SetAnchorsPreset(LayoutPreset.Center);
            _lobby.GrowHorizontal = GrowDirection.Both;
            _lobby.GrowVertical = GrowDirection.Both;
            AddChild(_lobby);
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(180f, 0f) };
            box.AddThemeConstantOverride("separation", 4);
            _lobby.AddChild(box);

            var title = Small(box, "SPACECRAFT", new Color(1f, 1f, 1f));
            title.AddThemeFontSizeOverride("font_size", 14);
            Small(box, "2-4 players, P2P lockstep (Klotho over ENet)", new Color(1f, 1f, 1f, 0.5f));

            _addr = Field(box, "127.0.0.1");
            _port = Field(box, "7777");
            _host  = Btn(box, "Host",  () => HostClicked?.Invoke());
            _join  = Btn(box, "Join",  () => JoinClicked?.Invoke());
            _ready = Btn(box, "Ready", () => ReadyClicked?.Invoke());
            _stop  = Btn(box, "Stop",  () => StopClicked?.Invoke());
            SetLobbyMode(LobbyMode.Idle);
        }

        public enum LobbyMode { Idle, Connecting, InSession, Playing }

        public void SetLobbyMode(LobbyMode mode)
        {
            bool idle = mode == LobbyMode.Idle;
            _addr.Editable = idle;
            _port.Editable = idle;
            _host.Disabled = !idle;
            _join.Disabled = !idle;
            _ready.Disabled = mode != LobbyMode.InSession;
            _stop.Disabled = idle;
            _lobby.Visible = mode != LobbyMode.Playing;
            _hint.Visible = mode == LobbyMode.Playing;
            if (idle) { _result.Visible = false; foreach (var a in _armies) a.Visible = false; }
        }

        public void SetStatus(string text) => _status.Text = text;

        public void SetReadyPressed(bool ready) => _ready.Text = ready ? "Ready (sent)" : "Ready";

        public void SetArmies(ArenaView arena, int localPlayerId)
        {
            arena.CountShips(_counts);
            for (int i = 0; i < _counts.Length; i++)
            {
                bool present = _counts[i] > 0 || _armies[i].Visible;
                _armies[i].Visible = present;
                _armies[i].Text = $"P{i + 1}{(i == localPlayerId ? "*" : "")}: {_counts[i]}";
            }
        }

        public void ShowResult(int winnerPlayerId, int localPlayerId)
        {
            _result.Visible = true;
            if (winnerPlayerId < 0) _result.Text = "DRAW";
            else if (winnerPlayerId == localPlayerId) _result.Text = "VICTORY";
            else _result.Text = $"P{winnerPlayerId + 1} WINS";
            _result.AddThemeColorOverride("font_color", winnerPlayerId < 0 ? new Color(1f, 1f, 1f) : Palette.Of(winnerPlayerId));
        }

        static Label Small(Node parent, string text, Color color)
        {
            var l = new Label { Text = text };
            l.AddThemeFontSizeOverride("font_size", 10);
            l.AddThemeColorOverride("font_color", color);
            l.MouseFilter = MouseFilterEnum.Ignore;
            parent.AddChild(l);
            return l;
        }

        static LineEdit Field(Node parent, string text)
        {
            var f = new LineEdit { Text = text };
            f.AddThemeFontSizeOverride("font_size", 10);
            parent.AddChild(f);
            return f;
        }

        static Button Btn(Node parent, string text, Action onPressed)
        {
            var b = new Button { Text = text };
            b.AddThemeFontSizeOverride("font_size", 10);
            b.Pressed += onPressed;
            parent.AddChild(b);
            return b;
        }
    }
}
