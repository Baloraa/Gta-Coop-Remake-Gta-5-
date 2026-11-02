using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.UI;

namespace GTACoOp
{
    /// <summary>
    /// Hayvan pedlerinde oynatılabilecek vanilla animasyonlar.
    ///
    /// Önemli: köpek ırkları (rottweiler, shepherd, husky, retriever, pug,
    /// westy, poodle) aynı iskeleti paylaşır. Vanilla oyunda kendi adıyla
    /// sözlüğü olmayan ırklar için rottweiler sözlükleri sorunsuz çalışır;
    /// bu yüzden "Shepherd" için ayrı bir sözlük icat etmek yerine aynı set
    /// kullanılır. Kedi iskeleti farklıdır ve köpek sözlükleri kediye
    /// oynatılmaz.
    ///
    /// `melee@streamed_core@` içindeki `victim_*` klipleri kurbana (insan
    /// pede) aittir; hayvanın kendisine oynatılmaz. Burada yalnızca hayvan
    /// klipleri listelenir.
    /// </summary>
    internal static class AnimalAnimations
    {
        internal sealed class Action
        {
            public string Label;
            public string Dict;   // null: animasyon değil, görevi temizler
            public string Clip;
            public bool Loop;
        }

        internal sealed class Set
        {
            public string Key;
            public string Label;
            public List<Action> Actions = new List<Action>();
        }

        /// <summary>
        /// Hayvan pedinde varsayılan klip sözlüğü. Hareket clipset'i
        /// (ROTTWEILER@MOVE) bu sözlüğü kullanır; sözlük yüklü olmadan
        /// klipler sessizce oynatılmaz.
        /// </summary>
        public const string DefaultClipDictionary = "CREATURES@ROTTWEILER@MOVE";

        /// <summary>
        /// Hayvan pedine varsayılan olarak atanan movement clipset.
        /// </summary>
        public const string DefaultMovementClipset = "ROTTWEILER@MOVE";

        private static readonly List<Set> Sets = BuildSets();

        /// <summary>Seçili set anahtarı ("rottweiler" / "retriever" / "cat").</summary>
        public static string SelectedSet = "rottweiler";

        private static int _catHash;
        private static int _mtlionHash;

        private static string _pendingDict;
        private static string _pendingClip;
        private static bool _pendingLoop;
        private static int _pendingDeadline;

        private static string _playingDict;
        private static int _playingUntil;

        private static int _defaultsModelHash;
        private static bool _defaultsApplied;

        private static List<Set> BuildSets()
        {
            var rott = new Set
            {
                Key = "rottweiler",
                Label = "Rottweiler (tum kopekler)"
            };
            rott.Actions.Add(new Action
            {
                Label = "Otur",
                Dict = "creatures@rottweiler@amb@world_dog_sitting@base",
                Clip = "base",
                Loop = true
            });
            rott.Actions.Add(new Action
            {
                Label = "Havla",
                Dict = "creatures@rottweiler@amb@world_dog_barking@idle_a",
                Clip = "idle_a",
                Loop = true
            });
            rott.Actions.Add(new Action
            {
                Label = "Yat (kennel)",
                Dict = "creatures@rottweiler@amb@sleep_in_kennel@",
                Clip = "sleep_in_kennel",
                Loop = true
            });
            rott.Actions.Add(new Action
            {
                Label = "Isir / saldir",
                Dict = "creatures@rottweiler@melee@streamed_core@",
                Clip = "attack",
                Loop = false
            });
            rott.Actions.Add(new Action
            {
                Label = "Kalk",
                Dict = "creatures@rottweiler@getup",
                Clip = "getup_l",
                Loop = false
            });
            rott.Actions.Add(new Action { Label = "Animasyonu durdur" });

            var retr = new Set
            {
                Key = "retriever",
                Label = "Retriever"
            };
            retr.Actions.Add(new Action
            {
                Label = "Otur",
                Dict = "creatures@retriever@amb@world_dog_sitting@base",
                Clip = "base",
                Loop = true
            });
            retr.Actions.Add(new Action
            {
                Label = "Havla",
                Dict = "creatures@retriever@amb@world_dog_barking@idle_a",
                Clip = "idle_a",
                Loop = true
            });
            retr.Actions.Add(new Action
            {
                Label = "Kalk",
                Dict = "creatures@retriever@getup",
                Clip = "getup_l",
                Loop = false
            });
            retr.Actions.Add(new Action { Label = "Animasyonu durdur" });

            var cat = new Set
            {
                Key = "cat",
                Label = "Kedi"
            };
            cat.Actions.Add(new Action
            {
                Label = "Yerde uyu",
                Dict = "creatures@cat@amb@world_cat_sleeping_ground@idle_a",
                Clip = "idle_a",
                Loop = true
            });
            cat.Actions.Add(new Action
            {
                Label = "Kalk",
                Dict = "creatures@cat@amb@world_cat_sleeping_ledge@exit",
                Clip = "exit_l",
                Loop = false
            });
            cat.Actions.Add(new Action { Label = "Animasyonu durdur" });

            return new List<Set> { rott, retr, cat };
        }

        public static IReadOnlyList<Set> AllSets => Sets;

        public static Set SetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (var set in Sets)
                if (string.Equals(set.Key, key, StringComparison.OrdinalIgnoreCase))
                    return set;
            return null;
        }

        public static bool IsAnimalModel(Ped ped)
        {
            if (ped == null || !ped.Exists()) return false;
            if (!ped.IsHuman) return true;
            return AddonPedVariation.IsAnimalHash(ped.Model.Hash);
        }

        /// <summary>Model kedi mi? Bilinmeyen addon hayvanlarında köpek kabul edilir.</summary>
        public static bool IsCat(int hash)
        {
            if (_catHash == 0)
            {
                try { _catHash = Game.GenerateHash("a_c_cat_01"); } catch { }
                try { _mtlionHash = Game.GenerateHash("a_c_mtlion"); } catch { }
            }
            return hash == _catHash || (hash != 0 && hash == _mtlionHash);
        }

        /// <summary>Modelin iskeletine uygun seti döner.</summary>
        public static Set SetForModel(Ped ped)
        {
            if (ped == null || !ped.Exists()) return SetByKey("rottweiler");
            var chosen = SetByKey(SelectedSet) ?? SetByKey("rottweiler");
            var cat = IsCat(ped.Model.Hash);
            if (cat) return SetByKey("cat");
            if (chosen != null && string.Equals(chosen.Key, "cat", StringComparison.OrdinalIgnoreCase))
                return SetByKey("rottweiler");
            return chosen;
        }

        /// <summary>
        /// Hayvan pedine varsayılan hareket setini uygular: klip sözlüğü
        /// CREATURES@ROTTWEILER@MOVE ve movement clipset ROTTWEILER@MOVE.
        /// İnsan modelinde uygulanmış seti geri alır.
        /// </summary>
        public static void ApplyDefaults(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            if (!IsAnimalModel(ped))
            {
                ResetDefaults(ped);
                return;
            }

            RequestDict(DefaultClipDictionary);
            try
            {
                Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, ped.Handle,
                    DefaultMovementClipset, 1.0f);
                _defaultsModelHash = ped.Model.Hash;
                _defaultsApplied = true;
                Main.Logger.WriteLine("Animal default movement: " +
                    DefaultMovementClipset + " (dict " + DefaultClipDictionary + ")");
            }
            catch { }
        }

        /// <summary>Varsayılan hareket setini geri alır (insan modeline dönüşte).</summary>
        public static void ResetDefaults(Ped ped = null)
        {
            if (!_defaultsApplied) return;
            _defaultsApplied = false;
            _defaultsModelHash = 0;
            try
            {
                var target = ped;
                if (target == null || !target.Exists()) target = Game.Player.Character;
                if (target != null && target.Exists())
                    Function.Call(Hash.RESET_PED_MOVEMENT_CLIPSET, target.Handle, 1.0f);
            }
            catch { }
        }

        public static void Play(Action action)
        {
            var player = Game.Player.Character;
            if (action == null) return;
            if (!IsAnimalModel(player))
            {
                Notification.Show("~o~Animal animations: ~w~secili model hayvan degil.");
                return;
            }

            if (string.IsNullOrEmpty(action.Dict))
            {
                Stop();
                Notification.Show("~g~Animal animation stopped.");
                return;
            }

            if (_playingDict != null && !string.Equals(_playingDict, action.Dict,
                    StringComparison.OrdinalIgnoreCase))
                TryRemoveDict(_playingDict);

            _pendingDict = action.Dict;
            _pendingClip = action.Clip;
            _pendingLoop = action.Loop;
            _pendingDeadline = Game.GameTime + 2000;
            RequestDict(action.Dict);
        }

        public static void Stop()
        {
            _pendingDict = null;
            var player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                try { Function.Call(Hash.CLEAR_PED_TASKS, player.Handle); } catch { }
            }
            if (_playingDict != null) TryRemoveDict(_playingDict);
            _playingDict = null;
            _playingUntil = 0;
        }

        /// <summary>Her karede çağrılır: animasyon sözlüğü yüklenince görevi başlatır.</summary>
        public static void Process()
        {
            var player = Game.Player.Character;
            var alive = player != null && player.Exists();

            if (!alive || !IsAnimalModel(player))
            {
                if (_playingDict != null || _pendingDict != null) Stop();
                ResetDefaults(player);
                return;
            }

            // Model değiştiğinde varsayılan sözlük/clipset yeniden uygulanır;
            // oyun bazı durumlarda (ragdoll, araçtan inme) clipset'i sıfırlar.
            if (!_defaultsApplied || _defaultsModelHash != player.Model.Hash)
                ApplyDefaults(player);

            // Tek seferlik animasyon bittiğinde tutamacı bırak.
            if (_playingUntil != 0 && Game.GameTime > _playingUntil)
            {
                if (_playingDict != null) TryRemoveDict(_playingDict);
                _playingDict = null;
                _playingUntil = 0;
            }

            if (string.IsNullOrEmpty(_pendingDict)) return;

            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, _pendingDict))
            {
                if (Game.GameTime > _pendingDeadline)
                {
                    // Sözlük bu sürümde/ modelde yok (ör. DLC animasyonu).
                    // Sonsuza kadar beklemek yerine setteki güvenli sözlüğe düş.
                    var fallback = FallbackFor(_pendingDict);
                    if (fallback != null && !string.Equals(fallback, _pendingDict,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Main.Logger.WriteLine("Animal animation fallback: " + _pendingDict +
                            " -> " + fallback);
                        _pendingDict = fallback;
                        _pendingDeadline = Game.GameTime + 1500;
                        RequestDict(fallback);
                        return;
                    }

                    Main.Logger.WriteLine("Animal animation unavailable: " + _pendingDict);
                    Notification.Show("~o~Bu animasyon bu modelde/ surumde yok.");
                    _pendingDict = null;
                    return;
                }

                RequestDict(_pendingDict);
                return;
            }

            var dict = _pendingDict;
            var clip = _pendingClip;
            var loop = _pendingLoop;
            _pendingDict = null;

            var flags = loop ? (int)AnimationFlags.Loop : 0;
            var duration = loop ? -1 : 1200;
            Function.Call(Hash.TASK_PLAY_ANIM, player.Handle, dict, clip,
                8.0f, -8.0f, duration, flags, 0.0f, false, false, false);

            _playingDict = dict;
            _playingUntil = loop ? 0 : Game.GameTime + duration;

            Main.Logger.WriteLine("Animal animation: " + dict + " / " + clip +
                " model=0x" + player.Model.Hash.ToString("X8"));
        }

        private static string FallbackFor(string dict)
        {
            if (string.IsNullOrEmpty(dict)) return null;
            if (dict.StartsWith("creatures@retriever@", StringComparison.OrdinalIgnoreCase))
                return "creatures@rottweiler@" + dict.Substring("creatures@retriever@".Length);
            // Kedi sözlükleri köpeğe oynatılmaz: iskelet uyuşmaz.
            return null;
        }

        private static void RequestDict(string dict)
        {
            try { Function.Call(Hash.REQUEST_ANIM_DICT, dict); } catch { }
        }

        private static void TryRemoveDict(string dict)
        {
            try { Function.Call(Hash.REMOVE_ANIM_DICT, dict); } catch { }
        }
    }
}
