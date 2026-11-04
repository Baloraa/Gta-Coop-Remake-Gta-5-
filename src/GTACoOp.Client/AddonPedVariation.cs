using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace GTACoOp
{
    /// <summary>
    /// Addon animal peds are often streamed without a resolved drawable
    /// variation. Other clients then receive a model they cannot render.
    /// Applying the default variation and forcing the same component change
    /// used by working FiveM animal scripts makes the animal visible.
    ///
    /// Çökme koruması: hiçbir native çağrısı model değişimi sırasında veya
    /// menü işleyicisinden yapılmaz. İstek kuyruğa alınır; Process() her karede
    /// çalışır ve yalnızca ped yeni modeline geçtiğinde, yerde, araç dışında ve
    /// ragdoll'da değilken uygular. Drawable/texture sayıları da çağrıdan önce
    /// doğrulanır (0 drawable'a varyasyon uygulamak oyunu çökertiyordu).
    /// </summary>
    internal static class AddonPedVariation
    {
        private sealed class PendingFix
        {
            public Ped Ped;
            public int Handle;
            public int ModelHash;
            public int Stage;
            public int DueAt;
            public int ExpiresAt;
        }

        private static readonly List<PendingFix> Pending = new List<PendingFix>();

        // Base-game animals, plus addon animal models reported by the server.
        // Ped.IsHuman depends on the ped metadata, so an addon animal built
        // with the wrong Pedtype looks human and would never enter the animal
        // path. The model name/hash is the reliable signal.
        private static readonly HashSet<int> BaseAnimalHashes = BuildBaseAnimalHashes();
        private static readonly HashSet<int> AddonAnimalHashes = new HashSet<int>();

        /// <summary>Bekleyen varyasyon düzeltmesi var mı?</summary>
        public static bool HasPending
        {
            get { return Pending.Count > 0; }
        }

        private static HashSet<int> BuildBaseAnimalHashes()
        {
            var set = new HashSet<int>();
            foreach (PedHash value in Enum.GetValues(typeof(PedHash)))
            {
                var name = value.ToString();
                if (name.StartsWith("a_c_", StringComparison.OrdinalIgnoreCase))
                    set.Add((int)value);
            }
            return set;
        }

        public static void RegisterAnimalModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return;
            try
            {
                AddonAnimalHashes.Add(Game.GenerateHash(model.Trim().ToLowerInvariant()));
            }
            catch { }
        }

        public static bool IsAnimalHash(int hash) =>
            BaseAnimalHashes.Contains(hash) || AddonAnimalHashes.Contains(hash);

        public static bool LooksLikeAnimal(string model, Ped ped)
        {
            if (ped != null && ped.Exists() && !ped.IsHuman) return true;
            if (!string.IsNullOrWhiteSpace(model) &&
                model.StartsWith("a_c_", StringComparison.OrdinalIgnoreCase))
                return true;
            return ped != null && ped.Exists() && IsAnimalHash(ped.Model.Hash);
        }

        public static bool IsScheduled(int handle)
        {
            for (var i = 0; i < Pending.Count; i++)
            {
                if (Pending[i].Handle == handle) return true;
            }
            return false;
        }

        /// <summary>
        /// Varyasyon düzeltmesini kuyruğa alır. Bu metot hiçbir native çağırmaz;
        /// böylece model değişiminin tam ortasında pede dokunulmaz.
        /// </summary>
        public static void ScheduleAnimalFix(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            var handle = ped.Handle;
            if (handle == 0) return;

            Pending.RemoveAll(p => p.Ped == null || !p.Ped.Exists() || p.Handle == handle);

            Pending.Add(new PendingFix
            {
                Ped = ped,
                Handle = handle,
                ModelHash = ped.Model.Hash,
                Stage = 0,
                DueAt = Game.GameTime + 120,
                ExpiresAt = Game.GameTime + 8000
            });
        }

        /// <summary>Bir ped için bekleyen düzeltmeleri iptal eder (model değişimi öncesi).</summary>
        public static void Cancel(int handle)
        {
            if (handle == 0) return;
            Pending.RemoveAll(p => p.Handle == handle);
        }

        /// <summary>Tüm bekleyen düzeltmeleri iptal eder (model değişimi öncesi).</summary>
        public static void CancelAll()
        {
            Pending.Clear();
        }

        public static void Process()
        {
            if (Pending.Count == 0) return;

            var now = Game.GameTime;
            for (var i = Pending.Count - 1; i >= 0; i--)
            {
                var fix = Pending[i];
                if (now < fix.DueAt) continue;

                var ped = fix.Ped;
                if (!IsReady(ped, fix))
                {
                    // Ped henüz yeni modele geçmedi ya da havada/araçta/ragdoll'da:
                    // kısa süre bekle, süre dolunca sessizce vazgeç.
                    if (now > fix.ExpiresAt) Pending.RemoveAt(i);
                    else fix.DueAt = now + 100;
                    continue;
                }

                if (fix.Stage == 0)
                {
                    try
                    {
                        Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped.Handle);
                        Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped.Handle);
                        Function.Call(Hash.CLEAR_PED_DECORATIONS, ped.Handle);
                    }
                    catch
                    {
                        // Ped bu karede silinmiş olabilir; düzeltme düşer.
                    }

                    fix.Stage = 1;
                    fix.DueAt = now + 250;
                    continue;
                }

                // Drawable/texture sayısı çağrıdan ÖNCE doğrulanır: olmayan bir
                // varyasyon indeksi oyunu çökertiyordu.
                try
                {
                    var drawables = Function.Call<int>(
                        Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, ped.Handle, 0);
                    if (drawables > 0)
                    {
                        var textures = Function.Call<int>(
                            Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ped.Handle, 0, 0);
                        if (textures > 0)
                        {
                            var texture = textures > 1 ? 1 : 0;
                            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped.Handle, 0, 0,
                                texture, 0);
                        }
                    }
                }
                catch
                {
                    // Sessizce atla; ped görünür kalır, oyun çökmez.
                }

                Pending.RemoveAt(i);
            }
        }

        /// <summary>
        /// Ped gerçekten hedef modelde mi ve varyasyon uygulamak için güvenli mi?
        /// </summary>
        private static bool IsReady(Ped ped, PendingFix fix)
        {
            if (ped == null || !ped.Exists()) return false;
            if (ped.Handle == 0 || ped.Handle != fix.Handle) return false;
            if (ped.Model.Hash != fix.ModelHash) return false;
            if (ped.IsDead) return false;
            if (ped.IsInVehicle()) return false;
            if (ped.IsRagdoll) return false;

            try
            {
                if (Function.Call<bool>(Hash.IS_ENTITY_IN_AIR, ped.Handle)) return false;
            }
            catch { }

            return true;
        }
    }
}
