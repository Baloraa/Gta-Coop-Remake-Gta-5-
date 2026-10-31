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
    /// </summary>
    internal static class AddonPedVariation
    {
        private sealed class PendingFix
        {
            public Ped Ped;
            public int ModelHash;
            public int Stage;
            public int DueAt;
        }

        private static readonly List<PendingFix> Pending = new List<PendingFix>();

        // Base-game animals, plus addon animal models reported by the server.
        // Ped.IsHuman depends on the ped metadata, so an addon animal built
        // with the wrong Pedtype looks human and would never enter the animal
        // path. The model name/hash is the reliable signal.
        private static readonly HashSet<int> BaseAnimalHashes = BuildBaseAnimalHashes();
        private static readonly HashSet<int> AddonAnimalHashes = new HashSet<int>();

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
                var fix = Pending[i];
                if (fix.Ped != null && fix.Ped.Exists() && fix.Ped.Handle == handle)
                    return true;
            }
            return false;
        }

        public static void ScheduleAnimalFix(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;

            var handle = ped.Handle;
            Pending.RemoveAll(p => p.Ped == null || !p.Ped.Exists() || p.Ped.Handle == handle);

            ApplyDefault(ped);

            Pending.Add(new PendingFix
            {
                Ped = ped,
                ModelHash = ped.Model.Hash,
                Stage = 0,
                DueAt = Game.GameTime + 50
            });
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
                if (ped == null || !ped.Exists() || ped.Handle == 0 ||
                    ped.Model.Hash != fix.ModelHash)
                {
                    Pending.RemoveAt(i);
                    continue;
                }

                if (fix.Stage == 0)
                {
                    // The known working sequence for animals is:
                    // component 0 -> drawable 0, texture 0, then texture 1.
                    Main.Logger.WriteLine("Addon variation stage 0: handle=" +
                        ped.Handle + " model=0x" + ped.Model.Hash.ToString("X8") +
                        " pedType=" + Function.Call<int>(Hash.GET_PED_TYPE, ped.Handle) +
                        " drawables=" + Function.Call<int>(
                            Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, ped.Handle, 0));
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped.Handle, 0, 0, 0, 0);
                    fix.Stage = 1;
                    fix.DueAt = now + 250;
                    continue;
                }

                var count = Function.Call<int>(
                    Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ped.Handle, 0, 0);
                var texture = count > 1 ? 1 : 0;
                var drawables = Function.Call<int>(
                    Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, ped.Handle, 0);
                Main.Logger.WriteLine("Addon variation stage 1: handle=" +
                    ped.Handle + " model=0x" + ped.Model.Hash.ToString("X8") +
                    " pedType=" + Function.Call<int>(Hash.GET_PED_TYPE, ped.Handle) +
                    " drawables=" + drawables +
                    " textures=" + count + " pick=" + texture);
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped.Handle, 0, 0,
                    texture, 0);
                Pending.RemoveAt(i);
            }
        }

        private static void ApplyDefault(Ped ped)
        {
            Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped.Handle);
            Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped.Handle);
            Function.Call(Hash.CLEAR_PED_DECORATIONS, ped.Handle);
        }
    }
}
