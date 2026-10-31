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
                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped.Handle, 0, 0, 0, 0);
                    fix.Stage = 1;
                    fix.DueAt = now + 250;
                    continue;
                }

                var count = Function.Call<int>(
                    Hash.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS, ped.Handle, 0, 0);
                var texture = count > 1 ? 1 : 0;
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
