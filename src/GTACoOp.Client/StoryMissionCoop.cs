using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using GTA;
using GTA.Native;
using GTA.UI;
using LemonUI;
using LemonUI.Elements;
using LemonUI.Menus;

namespace GTACoOp
{
    /// <summary>
    /// Host-led coordination for GTA V's own story missions.
    ///
    /// GTA V story missions are single-player scripts: their objectives, cutscenes
    /// and mission state live only on the host's game. This menu therefore does
    /// NOT start a second copy of the mission on every client and does NOT write
    /// save data. The host starts the real mission from Pause > Game > Replay
    /// Mission; friends stay in the same world and help with the synced action.
    /// </summary>
    public sealed class StoryMissionCoop
    {
        private const byte KindStoryMission = 2;
        private const byte ActionInvite = 1;
        private const byte ActionStarted = 2;
        private const byte ActionEnded = 3;
        private const byte ActionCancelled = 4;

        private readonly NativeMenu _menu;
        private readonly ObjectPool _menuPool;
        private readonly NativeItem _statusItem;

        private string _selectedMission;
        private string _hostName;
        private string _activeMission;
        private bool _remoteMission;
        private bool _lastLocalMissionFlag;
        private int _lastMissionCheck;

        private static readonly string[] Missions =
        {
            "Prologue",
            "Franklin and Lamar",
            "Repossession",
            "Complications",
            "Father/Son",
            "Daddy's Little Girl",
            "Marriage Counseling",
            "Fame or Shame",
            "Did Somebody Say Yoga?",
            "Reuniting the Family",
            "Friend Request",
            "Casing the Jewel Store",
            "The Jewel Store Job",
            "The Long Stretch",
            "Mr. Philips",
            "Nervous Ron",
            "Friends Reunited",
            "Hang Ten",
            "Trevor Philips Industries",
            "Crystal Maze",
            "Chop",
            "Hood Safari",
            "Lamar Down",
            "Dead Man Walking",
            "Three's Company",
            "By The Book",
            "Blitz Play",
            "Monkey Business",
            "Scouting the Port",
            "The Merryweather Heist",
            "The Paleto Score",
            "Predator",
            "The Bureau Raid",
            "The Wrap Up",
            "Bury the Hatchet",
            "Fresh Meat",
            "The Big Score",
            "Something Sensible",
            "The Time's Come",
            "The Third Way"
        };

        public StoryMissionCoop(NativeMenu mainMenu, ObjectPool menuPool)
        {
            _menuPool = menuPool;
            _menu = new NativeMenu("GTA CooP", "Story Mission Co-op (Host-led)");
            _menuPool.Add(_menu);
            mainMenu.AddSubMenu(_menu);

            var info = new NativeItem("How it works",
                "Host starts the real GTA V mission from Pause > Game > Replay Mission. Friends help in the same world.");
            info.Activated += (sender, args) =>
            {
                Notification.Show("~b~Host-led story co-op~w~: GTA V story scripts are single-player. The host starts the mission from ~y~Pause > Game > Replay Mission~w~. Friends join as helpers. Objectives and cutscenes stay host-only; no save files are changed.");
            };
            _menu.Add(info);

            _statusItem = new NativeItem("Status", "No mission selected");
            _menu.Add(_statusItem);

            foreach (string mission in Missions)
            {
                string captured = mission;
                var item = new NativeItem(captured, "Invite friends to this story mission");
                item.Activated += (sender, args) => SelectMission(captured);
                _menu.Add(item);
            }

            var cancel = new NativeItem("Cancel Invite", "Clear the current story mission invite");
            cancel.Activated += (sender, args) => CancelInvite();
            _menu.Add(cancel);
        }

        private void SelectMission(string mission)
        {
            _selectedMission = mission;
            _remoteMission = false;
            _hostName = Main.LocalDisplayName;
            _statusItem.AltTitle = mission + " (host: " + _hostName + ")";

            Send(ActionInvite, mission);
            Notification.Show("~y~" + mission + "~w~ selected. Open ~b~Pause > Game > Replay Mission~w~ and start it. Friends have been invited.");

            if (Function.Call<bool>(Hash.GET_MISSION_FLAG))
            {
                _activeMission = mission;
                Send(ActionStarted, mission);
            }
        }

        public void Tick()
        {
            if (Game.GameTime - _lastMissionCheck < 500)
                return;

            _lastMissionCheck = Game.GameTime;

            bool missionFlag = Function.Call<bool>(Hash.GET_MISSION_FLAG);
            if (missionFlag == _lastLocalMissionFlag)
                return;

            _lastLocalMissionFlag = missionFlag;

            if (!Main.IsOnServer())
                return;

            if (missionFlag)
            {
                if (string.IsNullOrWhiteSpace(_selectedMission) && string.IsNullOrWhiteSpace(_activeMission))
                    return;

                string mission = string.IsNullOrWhiteSpace(_selectedMission) ? _activeMission : _selectedMission;
                if (string.IsNullOrWhiteSpace(mission))
                    return;

                _activeMission = mission;
                _remoteMission = false;
                _hostName = Main.LocalDisplayName;
                _statusItem.AltTitle = "In mission: " + mission;
                Send(ActionStarted, mission);
            }
            else if (!string.IsNullOrWhiteSpace(_activeMission))
            {
                string mission = _activeMission;
                _activeMission = null;
                _selectedMission = null;
                _remoteMission = false;
                _statusItem.AltTitle = "Mission ended: " + mission;
                Send(ActionEnded, mission);
            }
        }

        public void Draw()
        {
            if (!_remoteMission || string.IsNullOrWhiteSpace(_activeMission))
                return;

            string host = string.IsNullOrWhiteSpace(_hostName) ? "Host" : _hostName;
            new ScaledText(new Point(20, 80),
                "Story co-op: " + _activeMission + "  |  help " + host,
                0.35f, GTA.UI.Font.ChaletLondon)
            {
                Color = Color.WhiteSmoke,
                Outline = true
            }.Draw();
        }

        public void OnNetworkMessage(long senderId, string senderName, byte[] payload)
        {
            if (senderId == Main.LocalId || payload == null || payload.Length < 1)
                return;

            try
            {
                using (var stream = new MemoryStream(payload))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    byte action = reader.ReadByte();
                    string mission = reader.ReadString();
                    if (string.IsNullOrWhiteSpace(mission))
                        mission = "Story mission";

                    string host = string.IsNullOrWhiteSpace(senderName) ? "Host" : senderName;

                    switch (action)
                    {
                        case ActionInvite:
                            _hostName = host;
                            _remoteMission = false;
                            Notification.Show("~b~" + host + "~w~ invites you to story mission: ~y~" + mission +
                                              "~w~. Stay near the host; do not start your own copy.");
                            break;

                        case ActionStarted:
                            _hostName = host;
                            _activeMission = mission;
                            _remoteMission = true;
                            Notification.Show("Host ~b~" + host + "~w~ started ~y~" + mission +
                                              "~w~. Help them; cutscenes and objectives are host-only.");
                            break;

                        case ActionEnded:
                            if (string.Equals(_hostName, host, StringComparison.OrdinalIgnoreCase))
                            {
                                _activeMission = null;
                                _remoteMission = false;
                            }
                            Notification.Show("Host ~b~" + host + "~w~ ended ~y~" + mission + "~w~.");
                            break;

                        case ActionCancelled:
                            if (string.Equals(_hostName, host, StringComparison.OrdinalIgnoreCase))
                            {
                                _activeMission = null;
                                _remoteMission = false;
                                _hostName = null;
                            }
                            Notification.Show("Story mission invite cancelled.");
                            break;
                    }
                }
            }
            catch
            {
                // Malformed or outdated messages must never break the client.
            }
        }

        public void OnRemoteDisconnected(string playerName)
        {
            if (!_remoteMission || string.IsNullOrWhiteSpace(playerName))
                return;

            if (!string.Equals(_hostName, playerName, StringComparison.OrdinalIgnoreCase))
                return;

            _activeMission = null;
            _hostName = null;
            _remoteMission = false;
            Notification.Show("Story mission host disconnected.");
        }

        public void OnDisconnected()
        {
            _activeMission = null;
            _hostName = null;
            _remoteMission = false;
            _lastLocalMissionFlag = false;
            _statusItem.AltTitle = string.IsNullOrWhiteSpace(_selectedMission)
                ? "No mission selected"
                : _selectedMission;
        }

        private void CancelInvite()
        {
            _selectedMission = null;
            _statusItem.AltTitle = "No mission selected";
            Send(ActionCancelled, "");
            Notification.Show("Story mission invite cancelled.");
        }

        private static void Send(byte action, string mission)
        {
            if (!Main.IsOnServer())
                return;

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(KindStoryMission);
                    writer.Write(action);
                    writer.Write(mission ?? "");
                }

                Main.SendPluginMessage("gtacoopremake", stream.ToArray());
            }
        }
    }
}
