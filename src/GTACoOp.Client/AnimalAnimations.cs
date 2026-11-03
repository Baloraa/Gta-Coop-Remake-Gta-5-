using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.UI;

namespace GTACoOp
{
    /// <summary>
    /// Hayvan pedlerinde oynatılabilecek vanilla animasyonlar ve ırk bazlı
    /// hareket (movement) setleri.
    ///
    /// Köpek ırkları (rottweiler, shepherd, husky, retriever, pug, westy,
    /// poodle) aynı iskeleti paylaşır; her ırkın kendi hareket clipset'i
    /// vardır ve clipset adı oyunun ped meta verisinde `&lt;irk&gt;@move`
    /// biçiminde geçer (ör. retriever pedi: MovementClipSet = retriever@move,
    /// ClipDictionaryName = creatures@retriever@move). Kedi iskeleti farklıdır
    /// ve köpek hareket setleri kediye uygulanmaz.
    ///
    /// Bir ırkın sözlüğü bu oyun sürümünde yoksa istemci rottweiler setine
    /// düşer; oyun clipset'i tanımadığında çökmesin diye clipset yalnızca
    /// klip sözlüğü gerçekten yüklendikten sonra uygulanır.
    ///
    /// `melee@streamed_core@` içindeki `victim_*` klipleri kurbana (insan
    /// pede) aittir; hayvanın kendisine oynatılmaz.
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
        /// Irk bazlı hareket seti. Klip sözlüğü yürüme/koşma kliplerini taşır,
        /// clipset ise oyunun peds'e uyguladığı movement setidir.
        /// </summary>
        internal sealed class MoveSet
        {
            public string Key;
            public string Label;    // menüde görünen ad, ör. "Rottweiler (Chop)"
            public string Dict;     // ör. CREATURES@ROTTWEILER@MOVE
            public string Clipset;  // ör. ROTTWEILER@MOVE
            public bool Cat;
        }

        /// <summary>Varsayılan klip sözlüğü (rottweiler hareket klipleri).</summary>
        public const string DefaultClipDictionary = "CREATURES@ROTTWEILER@MOVE";

        /// <summary>Varsayılan movement clipset.</summary>
        public const string DefaultMovementClipset = "ROTTWEILER@MOVE";

        private static readonly List<MoveSet> MoveSets = BuildMoveSets();
        private static readonly List<Set> Sets = BuildSets();

        /// <summary>Duruş animasyonları için seçili set ("rottweiler" / "retriever" / "cat").</summary>
        public static string SelectedSet = "rottweiler";

        private static string _selectedMoveKey = "rottweiler";

        private static int _catHash;
        private static int _mtlionHash;

        private static string _pendingDict;
        private static string _pendingClip;
        private static bool _pendingLoop;
        private static int _pendingDeadline;

        private static string _playingDict;
        private static int _playingUntil;

        private static MoveSet _pendingMove;
        private static int _pendingMoveDeadline;
        private static MoveSet _appliedMove;
        private static int _appliedMoveModelHash;
        private static int _moveFallbackWarned;

        private static int _defaultsModelHash;
        private static bool _defaultsApplied;

        private static List<MoveSet> BuildMoveSets()
        {
            // Clipset adları oyunun kendi ped meta verisindeki
            // <MovementClipSet> değerleriyle aynıdır (küçük harf de kabul edilir).
            return new List<MoveSet>
            {
                NewMove("rottweiler", "Rottweiler (Chop)", "ROTTWEILER@MOVE", false),
                NewMove("shepherd", "Shepherd", "SHEPHERD@MOVE", false),
                NewMove("husky", "Husky", "HUSKY@MOVE", false),
                NewMove("retriever", "Retriever", "RETRIEVER@MOVE", false),
                NewMove("pug", "Pug", "PUG@MOVE", false),
                NewMove("westy", "Westy", "WESTY@MOVE", false),
                NewMove("poodle", "Poodle", "POODLE@MOVE", false),
                NewMove("cat", "Kedi", "CAT@MOVE", true)
            };
        }

        private static MoveSet NewMove(string key, string label, string clipset, bool cat)
        {
            return new MoveSet
            {
                Key = key,
                Label = label,
                Clipset = clipset,
                Dict = "CREATURES@" + clipset.Replace("@MOVE", "") + "@MOVE",
                Cat = cat
            };
        }

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

        public static IReadOnlyList<MoveSet> AllMoveSets => MoveSets;

        public static string SelectedMoveKey
        {
            get { return _selectedMoveKey; }
        }

        public static Set SetByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (var set in Sets)
                if (string.Equals(set.Key, key, StringComparison.OrdinalIgnoreCase))
                    return set;
            return null;
        }

        public static MoveSet MoveByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (var set in MoveSets)
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

        /// <summary>Modelin iskeletine uygun duruş setini döner.</summary>
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

        /// <summary>Modelin iskeletine uygun hareket setini döner.</summary>
        public static MoveSet MoveForModel(Ped ped)
        {
            var chosen = MoveByKey(_selectedMoveKey) ?? MoveByKey("rottweiler");
            if (ped == null || !ped.Exists()) return chosen;
            if (IsCat(ped.Model.Hash)) return MoveByKey("cat");
            if (chosen != null && chosen.Cat) return MoveByKey("rottweiler");
            return chosen;
        }

        /// <summary>
        /// Menüden bir hareket seti seçer. Clipset hemen uygulanmaz: klip
        /// sözlüğü yüklendikten sonra Process() uygular, böylece oyun
        /// tanımadığı bir clipset adıyla karşılaşmaz.
        /// </summary>
        public static void SelectMove(string key)
        {
            _selectedMoveKey = string.IsNullOrWhiteSpace(key) ? "rottweiler" : key;
            _pendingMove = null;
            _pendingMoveDeadline = 0;
            _appliedMove = null;
            _defaultsApplied = false;
            _moveFallbackWarned = 0;
        }

        /// <summary>Varsayılan hareket setine (rottweiler) döner.</summary>
        public static void ApplyDefaults(Ped ped)
        {
            SelectMove("rottweiler");
        }

        /// <summary>Varsayılan hareket setini geri alır (insan modeline dönüşte).</summary>
        public static void ResetDefaults(Ped ped = null)
        {
            _pendingMove = null;
            _pendingMoveDeadline = 0;
            _appliedMove = null;
            _appliedMoveModelHash = 0;
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

        /// <summary>Her karede çağrılır: hareket setini ve bekleyen animasyonu uygular.</summary>
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

            EnsureMove(player, MoveForModel(player));

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

        /// <summary>
        /// Hareket clipset'ini uygular. Sözlük yüklenmeden uygulanmaz; sözlük
        /// yoksa rottweiler setine düşülür (kedide kedi setine geri dönülür).
        /// </summary>
        private static void EnsureMove(Ped ped, MoveSet set)
        {
            if (ped == null || !ped.Exists() || set == null) return;
            var hash = ped.Model.Hash;

            if (_appliedMove != null && _appliedMoveModelHash == hash &&
                string.Equals(_appliedMove.Key, set.Key, StringComparison.OrdinalIgnoreCase))
            {
                _defaultsApplied = true;
                _defaultsModelHash = hash;
                return;
            }

            if (_pendingMove == null ||
                !string.Equals(_pendingMove.Key, set.Key, StringComparison.OrdinalIgnoreCase))
            {
                _pendingMove = set;
                _pendingMoveDeadline = Game.GameTime + 2500;
                RequestDict(set.Dict);
            }

            if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, set.Dict))
            {
                try
                {
                    Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, ped.Handle, set.Clipset, 1.0f);
                    _appliedMove = set;
                    _appliedMoveModelHash = hash;
                    _pendingMove = null;
                    _defaultsApplied = true;
                    _defaultsModelHash = hash;
                    Main.Logger.WriteLine("Animal movement: " + set.Clipset +
                        " (dict " + set.Dict + ") model=0x" + hash.ToString("X8"));
                }
                catch (Exception ex)
                {
                    Main.Logger.WriteLine("Animal movement failed: " + set.Clipset +
                        " - " + ex.Message);
                }
                return;
            }

            if (Game.GameTime <= _pendingMoveDeadline)
            {
                RequestDict(set.Dict);
                return;
            }

            // Irkın hareket sözlüğü yok: kedi ise kedi, köpek ise rottweiler.
            var fallback = set.Cat ? null : MoveByKey("rottweiler");
            if (set.Cat) fallback = MoveByKey("cat");
            _pendingMove = null;

            if (fallback != null && !string.Equals(fallback.Key, set.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (_moveFallbackWarned == 0)
                {
                    _moveFallbackWarned = 1;
                    Main.Logger.WriteLine("Animal movement fallback: " + set.Clipset +
                        " -> " + fallback.Clipset);
                    Notification.Show("~o~" + set.Label + " hareket seti yok, ~w~" +
                        fallback.Label + " kullaniliyor.");
                }
                _selectedMoveKey = fallback.Key;
                RequestDict(fallback.Dict);
                return;
            }

            if (_moveFallbackWarned == 0)
            {
                _moveFallbackWarned = 1;
                Main.Logger.WriteLine("Animal movement unavailable: " + set.Clipset);
                Notification.Show("~o~Hareket seti bu surumde yok: ~w~" + set.Clipset);
            }
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
            if (string.IsNullOrEmpty(dict)) return;
            try { Function.Call(Hash.REQUEST_ANIM_DICT, dict); } catch { }
        }

        private static void TryRemoveDict(string dict)
        {
            try { Function.Call(Hash.REMOVE_ANIM_DICT, dict); } catch { }
        }
    }
}
