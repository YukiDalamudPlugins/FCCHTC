using Dalamud.Plugin.Services;

namespace FCCH.Common
{
    /// <summary>
    /// API12 shims. Dalamud API13+ added a generic <c>IGameGui.GetAddonByName&lt;T&gt;()</c>
    /// returning a typed pointer; API12 only exposes the non-generic <c>nint</c> overload.
    /// This extension restores the generic call shape so call sites compile unchanged.
    /// </summary>
    internal static unsafe class GameGuiApiCompat
    {
        public static T* GetAddonByName<T>(this IGameGui gui, string name, int index = 1) where T : unmanaged
            => (T*)gui.GetAddonByName(name, index);
    }
}
