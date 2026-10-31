using GTA;
using GTA.Native;

namespace GTACoOp
{
    /// <summary>
    /// Addon animal peds use animal skeletons and usually have no human
    /// ragdoll data. When a vehicle hits such a ped, GTA tries to start a
    /// human ragdoll and crashes (GTA5.exe 0xC0000005). Disabling ragdoll on
    /// collision and the ped ragdoll flag for animals keeps their normal
    /// movement while avoiding that code path. Human peds are never touched.
    /// </summary>
    internal static class AnimalPedSafety
    {
        // CPED_CONFIG_FLAG_CanRagdoll
        private const int CanRagdollConfigFlag = 32;

        private static int _lastLocalHandle;
        private static int _lastLocalModel;
        private static int _lastLocalApplied;

        /// <summary>Applies the protection once; safe to call repeatedly.</summary>
        public static void Apply(Ped ped)
        {
            if (ped == null || !ped.Exists() || ped.Handle == 0 || ped.IsHuman)
                return;

            // Player ped ragdoll is a different code path than the one used
            // by ambient animals; forcing it off is the actual crash guard.
            ped.CanRagdoll = false;

            Function.Call(Hash.SET_PED_RAGDOLL_ON_COLLISION, ped.Handle, false);
            Function.Call(Hash.SET_PED_CAN_RAGDOLL_FROM_PLAYER_IMPACT, ped.Handle, false);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, ped.Handle, CanRagdollConfigFlag, false);
        }

        /// <summary>
        /// Reapplies the protection to the local player after a model change
        /// or respawn, because the game can reset these flags.
        /// </summary>
        public static void TickLocalPlayer(Ped player)
        {
            if (player == null || !player.Exists())
                return;

            if (player.IsHuman)
            {
                _lastLocalHandle = 0;
                return;
            }

            var now = Game.GameTime;
            var handle = player.Handle;
            var model = player.Model.Hash;

            if (handle == _lastLocalHandle && model == _lastLocalModel &&
                now - _lastLocalApplied < 1000)
                return;

            _lastLocalHandle = handle;
            _lastLocalModel = model;
            _lastLocalApplied = now;

            Apply(player);
        }
    }
}
