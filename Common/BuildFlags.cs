namespace FCCH.Common
{
    /// <summary>
    /// Build-level safety switches.
    /// </summary>
    internal static class BuildFlags
    {
        /// <summary>
        /// Master gate for FCCH's raw native hooks that are installed at load and stay live
        /// for the whole session (OpLockManager.SendInventoryRefresh, InventoryScanner FC-bitset
        /// setter, RefusalWatch ShowLogMessage*).
        ///
        /// These resolve via global-client signatures / FFXIVClientStructs member-function
        /// pointers. On a divergent client (TC / USERJOY) a signature can match the WRONG
        /// function, so the detour then runs on the wrong code every time it fires and can
        /// corrupt game state — which has been observed to crash the game from the framework
        /// tick (native AV in a game hash-map lookup) even while FCCH's UI is idle.
        ///
        /// API15 / main (matching FFXIVClientStructs): set true.
        /// API12 / TC build (unverified signatures): set false.
        /// </summary>
        public const bool EnableNativeHooks = false;

        /// <summary>
        /// Master gate for FCCH reading the FC company chest (auto-scan on open, per-frame
        /// page/state reads, container iteration). These walk the FreeCompanyChest addon node
        /// tree and the FC inventory containers using global-client offsets / InventoryType
        /// values / sig-resolved member functions. On TC those are unverified and have been
        /// observed to take the game down the instant the chest opens ("Chest opened. Starting
        /// full scan..." is the last log line, no crash dump).
        ///
        /// API15 / main: true.  API12 / TC build (until offsets are verified): false.
        /// With this false FCCH stays loaded but does nothing while the chest is open — it will
        /// not crash, but chest deposit/withdraw/scan features are inert on TC.
        ///
        /// 2026-06-15: re-enabled for the TC crash fix attempt. The chest-open crash was traced
        /// to ChestManager.SwitchToPage, NOT to reading inventory (the Organizer reads fine via
        /// ScanFCChest). The suspected crashing call is ExecuteCommand(404) — see
        /// UseExecuteCommandOnPageSwitch below. If the chest still crashes on open with this true,
        /// flip it back to false to return to the safe (inert) behavior.
        /// </summary>
        public const bool EnableChestAccess = true;

        /// <summary>
        /// Whether ChestManager.SwitchToPage issues GameFunctions.ExecuteCommand(404, page) after
        /// firing the tab-switch callback. ExecuteCommand is sig-resolved and command id 404 is a
        /// global-client assumption; this is the SAME call the (now-disabled) OpLockManager hook
        /// made, which produced the +944BD2 native crash on TC. The tab-switch FireCallback on the
        /// addon should already drive the page load, so on TC we skip this extra call.
        ///
        /// API15 / main: true.  API12 / TC build: false (crash-fix candidate).
        /// </summary>
        public const bool UseExecuteCommandOnPageSwitch = false;
    }
}
