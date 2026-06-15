using FFXIVClientStructs.FFXIV.Client.Game;

namespace FCCH.Common
{
    public static unsafe class InventoryOpGate
    {
        public static bool HasPendingOperation()
        {
            // NOTE (API12/TC): InventoryManager.PendingOperations was added in a later
            // FFXIVClientStructs (API13+) and is not exposed in the API12 build, so we
            // cannot inspect in-flight inventory operations here. Degrade to "none pending".
            return false;
        }
    }
}
