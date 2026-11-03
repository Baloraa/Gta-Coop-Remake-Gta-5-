using System;
using System.Drawing;
using System.Linq;
using GTA;
using LemonUI.Elements;

namespace GTACoOp
{
    /// <summary>
    /// Köşedeki küçük gösterge: FPS, ortalama ping ve oyuncu sayısı.
    /// Ayarlar menüsünden (HUD) açılıp kapatılır; çizim hatası oyunu etkilemez.
    /// </summary>
    internal static class Hud
    {
        /// <summary>Menüdeki HUD anahtarı tarafından ayarlanır.</summary>
        public static bool Enabled = true;

        private static int _fps;
        private static int _ping;
        private static int _players = 1;

        public static void Draw()
        {
            if (!Enabled) return;

            try
            {
                var fps = (int)Game.FPS;
                if (fps > 0) _fps = fps;

                var players = 1;
                var ping = 0;

                var opponents = Main.Opponents;
                if (opponents != null && opponents.Count > 0)
                {
                    var latencies = opponents.Values
                        .Where(o => o != null && o.Latency > 0)
                        .Select(o => o.Latency)
                        .ToArray();

                    if (latencies.Length > 0)
                        ping = (int)latencies.Average();

                    players += opponents.Count;
                }

                _ping = ping;
                _players = players;

                var latency = ping > 0 ? ping + " ms" : "-";
                var text = "FPS " + _fps + "\nPING " + latency + "\n" + _players + " oyuncu";

                new ScaledText(new Point(20, 10), text, 0.35f) { Outline = true }.Draw();
            }
            catch
            {
                // Gösterge hiçbir zaman oyunu bozmasın.
            }
        }
    }
}
