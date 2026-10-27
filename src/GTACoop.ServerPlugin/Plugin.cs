using System;
using System.IO;
using System.Text;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.PluginAPI.Entities;

namespace GTACoop.Remake.Plugin
{
    /// <summary>
    /// Relays GTACoop Remake client messages through the normal game server.
    /// The client sends a PluginMessage named "gtacoopremake"; this bridge adds
    /// the sender id/name and broadcasts it to every connected player.
    /// </summary>
    public sealed class RemakeBridgePlugin : IPlugin
    {
        public string Name => "GTACoop Remake Bridge";

        public string Description => "Relays voice speaking state and host-led story mission events for GTACoop Remake.";

        public string Author => "GTACoop Remake";

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            PacketEvents.OnPluginMessage.Add((client, msg) =>
            {
                var response = new PluginResponse
                {
                    ContinuePluginProc = true,
                    ContinueServerProc = true
                };

                if (msg == null || msg.Name != "gtacoopremake" || msg.Data == null || msg.Data.Length == 0)
                    return response;

                byte kind = msg.Data[0];
                if (kind != 1 && kind != 2)
                    return response;

                try
                {
                    using (var stream = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                        {
                            writer.Write(kind);
                            writer.Write(client.NetConnection.RemoteUniqueIdentifier);
                            writer.Write(client.DisplayName ?? client.Name ?? "Player");
                            writer.Write(msg.Data.Length - 1);
                            if (msg.Data.Length > 1)
                                writer.Write(msg.Data, 1, msg.Data.Length - 1);
                        }

                        gameServer.SendPluginMessageToAll("gtacoopremake", stream.ToArray(), true);
                    }

                    response.ContinuePluginProc = false;
                }
                catch
                {
                    // Never let a malformed client message take down the server.
                }

                return response;
            });

            return true;
        }
    }
}
