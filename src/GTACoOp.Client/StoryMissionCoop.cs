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
        private const byte ActionState = 5;

        // Aktif mission durumu periyodik olarak yayınlanır. Böylece sonradan
        // katılan oyuncular da mission'ı öğrenir ve kaybolan paketler kendini onarır.
        private const int HeartbeatInterval = 5000;

        private readonly NativeMenu _menu;
        private readonly ObjectPool _menuPool;
        private readonly NativeItem _statusItem;

        private string _selectedMission;
        private string _hostName;
        private string _activeMission;
        private bool _remoteMission;
        private bool _lastLocalMissionFlag;
        private int _lastMissionCheck;
        private int _lastHeartbeat;
        private int _lastPlayerCount;
        private int _lastRemoteState;
        private bool _wasOnServer;

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
                _lastHeartbeat = Game.GameTime;
                Send(ActionStarted, mission);
            }
        }

        public void Tick()
        {
            if (Game.GameTime - _lastMissionCheck < 500)
                return;

            _lastMissionCheck = Game.GameTime;

            bool onServer = Main.IsOnServer();
            bool missionFlag = Function.Call<bool>(Hash.GET_MISSION_FLAG);

            // Sunucuda değilken yalnızca yerel durumu izle. Bağlanınca mission
            // hâlâ sürüyorsa aşağıdaki "justConnected" yolu devreye girer.
            if (!onServer)
            {
                _wasOnServer = false;
                _lastLocalMissionFlag = missionFlag;
                return;
            }

            // Başka birinin mission'ına yardım ediyorsak kendi mission flag'imizi
            // yayınlamayız; aksi hâlde iki taraf birbirinin durumunu ezer.
            if (_remoteMission)
            {
                // Bitiş paketi kaybolursa HUD sonsuza kadar takılı kalmasın.
                if (_lastRemoteState != 0 && Game.GameTime - _lastRemoteState > 30000)
                {
                    _activeMission = null;
                    _hostName = null;
                    _remoteMission = false;
                    _lastRemoteState = 0;
                    Notification.Show("Story mission state timed out; the host may have left the mission.");
                }

                _lastLocalMissionFlag = missionFlag;
                return;
            }

            bool justConnected = !_wasOnServer;
            _wasOnServer = true;

            if (missionFlag && (justConnected || missionFlag != _lastLocalMissionFlag))
            {
                // Menüden mission seçilmediyse (ör. Pause > Replay Mission ile
                // başlatıldıysa) genel bir ad kullan. Böylece host'un seçim
                // yapmayı unutması davetlerin kaybolmasına yol açmaz.
                string mission = !string.IsNullOrWhiteSpace(_selectedMission)
                    ? _selectedMission
                    : _activeMission;
                if (string.IsNullOrWhiteSpace(mission))
                    mission = "Host mission";

                _activeMission = mission;
                _remoteMission = false;
                _hostName = Main.LocalDisplayName;
                _statusItem.AltTitle = "In mission: " + mission;
                _lastHeartbeat = Game.GameTime;
                Send(ActionStarted, mission);
            }
            else if (!missionFlag && !justConnected && _lastLocalMissionFlag &&
                     !string.IsNullOrWhiteSpace(_activeMission))
            {
                string mission = _activeMission;
                _activeMission = null;
                _selectedMission = null;
                _remoteMission = false;
                _statusItem.AltTitle = "Mission ended: " + mission;
                _lastHeartbeat = 0;
                Send(ActionEnded, mission);
            }

            _lastLocalMissionFlag = missionFlag;

            // Mission sürerken durum paketini periyodik olarak gönder. Yeni bir
            // oyuncu katıldığında da hemen gönder ki geç katılanlar geride kalmasın.
            if (missionFlag && !string.IsNullOrWhiteSpace(_activeMission))
            {
                int players = Main.PlayerCount;
                bool playerListChanged = players != _lastPlayerCount;
                _lastPlayerCount = players;

                if (players > 1 && playerListChanged)
                {
                    _lastHeartbeat = Game.GameTime;
                    Send(ActionState, _activeMission);
                }
                else if (Game.GameTime - _lastHeartbeat >= HeartbeatInterval)
                {
                    _lastHeartbeat = Game.GameTime;
                    Send(ActionState, _activeMission);
                }
            }
            else
            {
                _lastPlayerCount = Main.PlayerCount;
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

                    // Biz aktif olarak mission yönetiyorsak başka birinden gelen
                    // davet/durum paketleri kendi durumumuzu ezmesin.
                    bool localHosting = !_remoteMission && !string.IsNullOrWhiteSpace(_activeMission);
                    if (localHosting &&
                        (action == ActionInvite || action == ActionStarted || action == ActionState))
                        return;

                    switch (action)
                    {
                        case ActionInvite:
                            _hostName = host;
                            _activeMission = null;
                            _remoteMission = false;
                            Notification.Show("~b~" + host + "~w~ invites you to story mission: ~y~" + mission +
                                              "~w~. Stay near the host; do not start your own copy.");
                            break;

                        case ActionStarted:
                            {
                                bool alreadyActive = _remoteMission &&
                                    string.Equals(_activeMission, mission, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(_hostName, host, StringComparison.OrdinalIgnoreCase);
                                _hostName = host;
                                _activeMission = mission;
                                _remoteMission = true;
                                _lastRemoteState = Game.GameTime;
                                if (!alreadyActive)
                                    Notification.Show("Host ~b~" + host + "~w~ started ~y~" + mission +
                                                      "~w~. Help them; cutscenes and objectives are host-only.");
                            }
                            break;

                        case ActionState:
                            {
                                // Periyodik durum paketi: aynı mission için sessizce
                                // güncelle, yalnızca ilk öğrenildiğinde bildir.
                                bool alreadyActive = _remoteMission &&
                                    string.Equals(_activeMission, mission, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(_hostName, host, StringComparison.OrdinalIgnoreCase);
                                _hostName = host;
                                _activeMission = mission;
                                _remoteMission = true;
                                _lastRemoteState = Game.GameTime;
                                if (!alreadyActive)
                                    Notification.Show("Host ~b~" + host + "~w~ is running ~y~" + mission +
                                                      "~w~. Join them; cutscenes and objectives are host-only.");
                            }
                            break;

                        case ActionEnded:
                            if (string.Equals(_hostName, host, StringComparison.OrdinalIgnoreCase))
                            {
                                _activeMission = null;
                                _remoteMission = false;
                                _hostName = null;
                                _lastRemoteState = 0;
                                Notification.Show("Host ~b~" + host + "~w~ ended ~y~" + mission + "~w~.");
                            }
                            break;

                        case ActionCancelled:
                            if (string.Equals(_hostName, host, StringComparison.OrdinalIgnoreCase))
                            {
                                _activeMission = null;
                                _remoteMission = false;
                                _hostName = null;
                                _lastRemoteState = 0;
                                Notification.Show("Story mission invite cancelled.");
                            }
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
            _lastRemoteState = 0;
            Notification.Show("Story mission host disconnected.");
        }

        public void OnDisconnected()
        {
            _activeMission = null;
            _hostName = null;
            _remoteMission = false;
            _lastLocalMissionFlag = false;
            _lastHeartbeat = 0;
            _lastPlayerCount = 0;
            _lastRemoteState = 0;
            _wasOnServer = false;
            _statusItem.AltTitle = string.IsNullOrWhiteSpace(_selectedMission)
                ? "No mission selected"
                : _selectedMission;
        }

        private void CancelInvite()
        {
            // Menüden iptal edilirse hem bekleyen daveti hem de varsa aktif
            // host mission durumunu temizle; arkadaşlar HUD'da takılı kalmasın.
            if (!_remoteMission && !string.IsNullOrWhiteSpace(_activeMission))
                Send(ActionEnded, _activeMission);

            _selectedMission = null;
            _activeMission = null;
            _lastHeartbeat = 0;
            _lastRemoteState = 0;
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
