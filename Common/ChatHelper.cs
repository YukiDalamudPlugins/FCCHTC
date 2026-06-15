using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace FCCH.Common
{
    /// <summary>
    /// Centralized chat messaging with colored [FCCH] prefix.
    /// Colors: Info=Green, Debug=Orange, Warning=Yellow, Error=Red
    /// </summary>
    public static class ChatHelper
    {
        // UIForeground color IDs (from Lumina UIColor sheet)
        private const ushort ColorGreen = 504;   // Healer green
        private const ushort ColorOrange = 500;  // Debug orange
        private const ushort ColorYellow = 31;   // Warning yellow
        private const ushort ColorRed = 17;      // Error red

        public static void Info(string message)
        {
            var seString = new SeStringBuilder()
                .AddUiForeground(ColorGreen)
                .AddText("[FCCH]")
                .AddUiForegroundOff()
                .AddText($" {message}")
                .Build();
            Plugin.Chat.Print(seString);
        }

        public static void Debug(string message)
        {
            var seString = new SeStringBuilder()
                .AddUiForeground(ColorOrange)
                .AddText("[FCCH] 除錯：")
                .AddUiForegroundOff()
                .AddText($" {message}")
                .Build();
            Plugin.Chat.Print(seString);
        }

        public static void Warning(string message)
        {
            var seString = new SeStringBuilder()
                .AddUiForeground(ColorYellow)
                .AddText("[FCCH] 警告：")
                .AddUiForegroundOff()
                .AddText($" {message}")
                .Build();
            Plugin.Chat.Print(seString);
        }

        public static void Error(string message)
        {
            var seString = new SeStringBuilder()
                .AddUiForeground(ColorRed)
                .AddText("[FCCH] 錯誤：")
                .AddUiForegroundOff()
                .AddText($" {message}")
                .Build();
            Plugin.Chat.Print(seString);
        }

        public static void Verbose(string message)
        {
            if (!Plugin.Configuration.VerboseMode) return;
            var seString = new SeStringBuilder()
                .AddUiForeground(ColorGreen)
                .AddText("[FCCH]")
                .AddUiForegroundOff()
                .AddText($" {message}")
                .Build();
            Plugin.Chat.Print(seString);
        }

        public static void PrintBatchWarnings(System.Collections.Generic.Dictionary<string, int> failures)
        {
            if (failures == null || failures.Count == 0) return;

            foreach (var kvp in failures)
            {
                Warning($"因「{kvp.Key}」無法處理 {kvp.Value} 個物品(開啟詳細模式可查看細節)。");
            }
        }
    }
}
