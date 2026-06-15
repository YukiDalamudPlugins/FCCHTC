using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FCCH.Common;
using FCCH.Models;

namespace FCCH.Managers.Gil
{
    public unsafe class GilExecutor
    {
        private readonly Configuration _configuration;
        private readonly ChestManager _chestManager;
        private readonly MoveManager _moveManager;
        private readonly Action<PendingGilTransaction> _setPendingTransaction;

        public GilExecutor(Configuration configuration, ChestManager chestManager, MoveManager moveManager, Action<PendingGilTransaction> setPendingTransaction)
        {
            _configuration = configuration;
            _chestManager = chestManager;
            _moveManager = moveManager;
            _setPendingTransaction = setPendingTransaction;
        }

        public void ExecuteDeposit(uint amount)
        {
            if (!GilValidator.IsChestOpen())
            {
                ChatHelper.Error("必須先打開部隊寶物庫才能存入金幣。");
                return;
            }

            var access = _chestManager.GetChestAccess(InventoryType.FreeCompanyGil);
            if (access != Constants.FCPermissions.FULL_ACCESS && access != Constants.FCPermissions.DEPOSIT_ONLY)
            {
                ChatHelper.Info("略過金幣的 gd。");
                return;
            }

            var validationResult = GilValidator.ValidateDeposit(amount, GilValidator.GetPlayerGil(), GilValidator.GetFCGilHeader(), _configuration.GilAlwaysKeep);
            if (!validationResult.IsValid)
            {
                ChatHelper.Error(validationResult.ErrorMessage);
                return;
            }

            var finalAmount = validationResult.AdjustedAmount;
            if (finalAmount == 0)
            {
                ChatHelper.Info("套用限制後沒有可存入的金幣。");
                return;
            }

            _setPendingTransaction(new PendingGilTransaction
            {
                Amount = finalAmount,
                IsDeposit = true,
                Timestamp = DateTime.Now
            });

            var bank = (AtkUnitBase*)Plugin.GameGui.GetAddonByName<AtkUnitBase>("Bank", 1);
            if (bank != null && bank->IsVisible)
            {
                FireBankDeposit(bank, finalAmount);
            }
            else
            {
                SwitchToGilTab();
                ChatHelper.Verbose($"已排入存入 {finalAmount:N0} 金幣。");
            }
        }

        public void ExecuteWithdraw(uint amount)
        {
            if (!GilValidator.IsChestOpen())
            {
                ChatHelper.Error("必須先打開部隊寶物庫才能取出金幣。");
                return;
            }

            if (_chestManager.GetChestAccess(InventoryType.FreeCompanyGil) != Constants.FCPermissions.FULL_ACCESS)
            {
                ChatHelper.Info("略過金幣的 gw。");
                return;
            }

            var validationResult = GilValidator.ValidateWithdraw(amount, GilValidator.GetPlayerGil(), GilValidator.GetFCGilHeader(), GilValidator.GetFCGilContainerQuantity);
            if (!validationResult.IsValid)
            {
                ChatHelper.Error(validationResult.ErrorMessage);
                return;
            }

            var finalAmount = validationResult.AdjustedAmount;
            if (finalAmount == 0)
            {
                ChatHelper.Info("套用限制後沒有可取出的金幣。");
                return;
            }

            _setPendingTransaction(new PendingGilTransaction
            {
                Amount = finalAmount,
                IsDeposit = false,
                Timestamp = DateTime.Now
            });

            var moveOp = new MoveOperation
            {
                SrcInv = InventoryType.FreeCompanyGil,
                SrcSlot = 0,
                DstInv = InventoryType.Currency,
                DstSlot = 0,
                ItemId = 1,
                Amount = finalAmount,
                IsNativeMove = true
            };
            
            _moveManager.Enqueue(moveOp);
            ChatHelper.Verbose($"已排入取出 {finalAmount:N0} 金幣。");
        }

        public void AutoDeposit()
        {
            if (_configuration.GilMode == GilDepositMode.Disabled) return;
            var access = _chestManager.GetChestAccess(InventoryType.FreeCompanyGil);
            if (access != Constants.FCPermissions.FULL_ACCESS && access != Constants.FCPermissions.DEPOSIT_ONLY) return;

            uint amount = 0;
            uint playerGil = GilValidator.GetPlayerGil();

            if (_configuration.GilMode == GilDepositMode.Percentage)
            {
                var pct = Math.Clamp(_configuration.GilPercentage, 1, 100);
                amount = (uint)(playerGil * pct / 100);
            }
            else if (_configuration.GilMode == GilDepositMode.FixedAmount)
            {
                amount = _configuration.GilFixedAmount;
            }

            if (amount == 0) return;

            var validationResult = GilValidator.ValidateDeposit(amount, playerGil, GilValidator.GetFCGilHeader(), _configuration.GilAlwaysKeep);
            if (!validationResult.IsValid || validationResult.AdjustedAmount == 0) return;

            _setPendingTransaction(new PendingGilTransaction
            {
                Amount = validationResult.AdjustedAmount,
                IsDeposit = true,
                Timestamp = DateTime.Now
            });

            var bank = (AtkUnitBase*)Plugin.GameGui.GetAddonByName<AtkUnitBase>("Bank", 1);
            if (bank != null && bank->IsVisible)
            {
                FireBankDeposit(bank, validationResult.AdjustedAmount);
            }
            else
            {
                SwitchToGilTab();
            }
        }

        private static void FireBankDeposit(AtkUnitBase* bank, uint amount)
        {
            Callback.Fire(bank, true, 3, amount);
            Callback.Fire(bank, true, 0);
        }

        private void SwitchToGilTab()
        {
            var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName<AtkUnitBase>(Constants.FC_CHEST_ADDON_NAME, 1);
            if (addon == null || !addon->IsVisible) return;

            Callback.Fire(addon, true, 2);
        }
    }

    public struct PendingGilTransaction
    {
        public uint Amount;
        public bool IsDeposit;
        public DateTime Timestamp;
    }
}
