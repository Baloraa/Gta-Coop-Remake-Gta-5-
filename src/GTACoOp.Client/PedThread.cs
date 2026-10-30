using System;
using System.Collections.Generic;
using System.Linq;
using GTA;

namespace GTACoOp
{
    public class PedThread : Script
    {
        // NPC senkronu artık her karede değil, saniyede 4 kez taranır ve
        // her NPC en fazla saniyede 10 kez gönderilir (Main.SendPedData).
        // Bu, yoğun trafikte ağ ve CPU yükünü ciddi biçimde azaltır.
        private const int CleanupInterval = 1000;
        private const int ScanInterval = 250;

        private int _lastCleanup;
        private int _lastScan;
        private List<Ped> _streamedPeds = new List<Ped>();

        public PedThread()
        {
            Tick += OnTick;
        }

        public void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsOnServer()) return;

            int now = Game.GameTime;

            if (now - _lastCleanup >= CleanupInterval)
            {
                _lastCleanup = now;
                CleanupStale();
            }

            SyncPed[] opponents;
            lock (Main.Opponents) opponents = Main.Opponents.Values.ToArray();

            SyncPed[] npcs;
            lock (Main.Npcs) npcs = Main.Npcs.Values.ToArray();

            for (int i = 0; i < opponents.Length; i++) opponents[i].DisplayLocally();
            for (int i = 0; i < npcs.Length; i++) npcs[i].DisplayLocally();

            // Kimse bağlı değilken NPC taraması tamamen gereksizdir.
            if (Main.PlayerSettings.SyncWorld && Main.HasOpponents)
            {
                if (now - _lastScan >= ScanInterval)
                {
                    _lastScan = now;
                    _streamedPeds = SelectClosestPeds(opponents, npcs);
                }

                for (int i = 0; i < _streamedPeds.Count; i++)
                {
                    var ped = _streamedPeds[i];
                    if (ped != null && ped.Exists())
                        Main.SendPedData(ped);
                }
            }
            else
            {
                _streamedPeds.Clear();
            }

            // Oyuncu konumu/durumu eskisi gibi her karede gönderilir;
            // SendPlayerData kendi içinde tick hızına göre sınırlar.
            Main.SendPlayerData();
        }

        private static void CleanupStale()
        {
            const int npcThreshold = 5000; // 5 second timeout
            const int playerThreshold = 60000; // 60 second timeout
            int now = Game.GameTime;

            List<long> staleOpponents = null;
            lock (Main.Opponents)
            {
                foreach (var pair in Main.Opponents)
                {
                    if (now - pair.Value.LastUpdateReceived <= playerThreshold) continue;
                    staleOpponents ??= new List<long>();
                    staleOpponents.Add(pair.Key);
                }

                if (staleOpponents != null)
                {
                    foreach (var key in staleOpponents)
                    {
                        if (!Main.Opponents.TryGetValue(key, out var ped)) continue;
                        ped.Clear();
                        Main.Opponents.Remove(key);
                    }
                }
            }

            List<string> staleNpcs = null;
            lock (Main.Npcs)
            {
                foreach (var pair in Main.Npcs)
                {
                    if (now - pair.Value.LastUpdateReceived <= npcThreshold) continue;
                    staleNpcs ??= new List<string>();
                    staleNpcs.Add(pair.Key);
                }

                if (staleNpcs != null)
                {
                    foreach (var key in staleNpcs)
                    {
                        if (!Main.Npcs.TryGetValue(key, out var ped)) continue;
                        ped.Clear();
                        Main.Npcs.Remove(key);
                    }
                }
            }
        }

        private static List<Ped> SelectClosestPeds(SyncPed[] opponents, SyncPed[] npcs)
        {
            int limit = Main.PlayerSettings.MaxStreamedNpcs;
            if (limit <= 0) limit = 10;

            var ignore = new HashSet<int>();
            var local = Game.Player.Character;
            if (local != null && local.Exists()) ignore.Add(local.Handle);

            foreach (var ped in opponents)
                if (ped?.Character != null) ignore.Add(ped.Character.Handle);

            foreach (var ped in npcs)
                if (ped?.Character != null) ignore.Add(ped.Character.Handle);

            var origin = local != null && local.Exists() ? local.Position : new GTA.Math.Vector3();

            return World.GetAllPeds()
                .Where(p => p != null && p.Exists() && !ignore.Contains(p.Handle))
                .OrderBy(p => (p.Position - origin).Length())
                .Take(limit)
                .ToList();
        }
    }
}
