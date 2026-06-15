using System.Runtime.InteropServices;

namespace FCCH.Common
{
    /// <summary>
    /// API12 shim for native game functions that newer FFXIVClientStructs wraps but the
    /// API12 (TC) build does not. We resolve them ourselves via the same signatures CS uses.
    /// </summary>
    internal static class GameFunctions
    {
        private delegate void ExecuteCommandDelegate(int command, int param1, int param2, int param3, int param4);
        private static ExecuteCommandDelegate? _executeCommand;

        /// <summary>
        /// Global game <c>ExecuteCommand</c>. API13+ exposes this as <c>GameMain.ExecuteCommand</c>;
        /// here we resolve it from the call-site signature (same one CS uses internally).
        /// </summary>
        public static void ExecuteCommand(int command, int param1 = 0, int param2 = 0, int param3 = 0, int param4 = 0)
        {
            if (_executeCommand == null)
            {
                // MemberFunction sig "E8 ?? ?? ?? ?? 8D 46 0A" is a `call rel32`; resolve the target.
                nint callSite = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? 8D 46 0A");
                nint target = callSite + 5 + Marshal.ReadInt32(callSite + 1);
                _executeCommand = Marshal.GetDelegateForFunctionPointer<ExecuteCommandDelegate>(target);
            }

            _executeCommand(command, param1, param2, param3, param4);
        }
    }
}
