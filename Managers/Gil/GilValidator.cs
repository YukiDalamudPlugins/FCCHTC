using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FCCH.Common;

namespace FCCH.Managers.Gil
{
    public struct GilValidationResult
    {
        public bool IsValid;
        public uint AdjustedAmount;
        public string ErrorMessage;
    }

    public unsafe static class GilValidator
    {
        public const uint MAX_GIL = 999_999_999;

        public static GilValidationResult ValidateDeposit(uint requestedAmount, uint playerGil, uint fcGil, uint alwaysKeep)
        {
            if (requestedAmount == 0)
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = "數量必須大於 0。" };

            if (playerGil < alwaysKeep)
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = $"你的金幣({playerGil:N0})低於「保留下限」({alwaysKeep:N0}),已停止存入。" };

            uint maxDepositable = playerGil - alwaysKeep;
            if (maxDepositable == 0)
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = "扣除「保留下限」後沒有可存入的金幣。" };

            uint fcRoomLeft = MAX_GIL - fcGil;
            if (fcRoomLeft == 0)
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = "部隊寶物庫的金幣已達上限。" };

            uint finalAmount = (uint)Math.Min((long)requestedAmount, (long)maxDepositable);
            finalAmount = (uint)Math.Min((long)finalAmount, (long)fcRoomLeft);

            if (finalAmount < requestedAmount)
            {
                ChatHelper.Info($"因限制,數量由 {requestedAmount:N0} 調整為 {finalAmount:N0}。");
            }

            return new GilValidationResult { IsValid = true, AdjustedAmount = finalAmount, ErrorMessage = "" };
        }

        public static GilValidationResult ValidateWithdraw(uint requestedAmount, uint playerGil, uint fcGilHeader, Func<uint> getContainerQuantity)
        {
            if (requestedAmount == 0)
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = "數量必須大於 0。" };

            uint actualContainerGil = getContainerQuantity();
            
            if (actualContainerGil == 0 && fcGilHeader > 0)
            {
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = "部隊金幣資料尚未載入完成,請再試一次。" };
            }

            if (actualContainerGil == 0)
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = "部隊寶物庫沒有可取出的金幣。" };

            uint playerRoomLeft = MAX_GIL - playerGil;
            if (playerRoomLeft == 0)
                return new GilValidationResult { IsValid = false, AdjustedAmount = 0, ErrorMessage = "你的金幣已達上限。" };

            uint finalAmount = (uint)Math.Min((long)requestedAmount, (long)actualContainerGil);
            finalAmount = (uint)Math.Min((long)finalAmount, (long)playerRoomLeft);

            if (finalAmount < requestedAmount)
            {
                ChatHelper.Info($"因限制,數量由 {requestedAmount:N0} 調整為 {finalAmount:N0}。");
            }

            return new GilValidationResult { IsValid = true, AdjustedAmount = finalAmount, ErrorMessage = "" };
        }

        public static bool IsChestOpen()
        {
            var addon = Plugin.GameGui.GetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>(Constants.FC_CHEST_ADDON_NAME, 1);
            return addon != null && addon->IsVisible;
        }

        public static bool CanAccessGilTab()
        {
            var addon = (FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase*)Plugin.GameGui.GetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>(Constants.FC_CHEST_ADDON_NAME, 1);
            if (addon == null || !addon->IsVisible) return false;

            var gilNode = addon->GetNodeById(16);
            if (gilNode == null) return false;

            return gilNode->NodeFlags.HasFlag(FFXIVClientStructs.FFXIV.Component.GUI.NodeFlags.Enabled);
        }

        public static uint GetPlayerGil()
        {
            var invManager = InventoryManager.Instance();
            return invManager != null ? invManager->GetGil() : 0;
        }

        public static uint GetFCGilHeader()
        {
            var invManager = InventoryManager.Instance();
            return invManager != null ? invManager->GetFreeCompanyGil() : 0;
        }

        public static uint GetFCGilContainerQuantity()
        {
            var invManager = InventoryManager.Instance();
            if (invManager == null) return 0;
            
            var container = invManager->GetInventoryContainer(InventoryType.FreeCompanyGil);
            if (container == null || !container->IsLoaded) return 0;
            
            var item = container->GetInventorySlot(0);
            return item != null ? (uint)item->Quantity : 0u;
        }

        public static string GetPermissionString(ChestManager chestManager)
        {
            if (!IsChestOpen()) return "Chest Closed";
            return ChestManager.NameAccess(chestManager.GetChestAccess(InventoryType.FreeCompanyGil));
        }
    }
}
