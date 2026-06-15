using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FCCH.Managers;
using FCCH.Managers.Organizer;
using FCCH.Common;

namespace FCCH.UI
{
    public class OrganizerTab : IDisposable
    {
        private readonly OrgService _service;
        private readonly Configuration _config;
        private readonly ChestHelper _helper;

        private int _selectedModeIndex = 0;
        private int _selectedSourceIndex = 1;
        private int _selectedDestIndex = 2;
        private HashSet<OrgFilterCategory> _selectedFilters = new() { OrgFilterCategory.AllItems };
        private int _selectedSortIndex = 0;
        private bool _sortDescending = false;
        private bool _wasChestOpen = false;
        private int _chestClosedFrames = 0;
        private const int CHEST_CLOSE_THRESHOLD = 3;

        public OrganizerTab(OrgService service, Configuration config, ChestHelper helper)
        {
            _service = service;
            _config = config;
            _helper = helper;
            SyncRequestFromUI();
        }

        private void SyncRequestFromUI()
        {
            var tabs = OrgService.GetAvailableTabs();
            _service.CurrentRequest.Mode = _selectedModeIndex switch
            {
                0 => OrgOperationMode.Move,
                _ => OrgOperationMode.Sort
            };
            _service.CurrentRequest.SourceTab = tabs[_selectedSourceIndex];

            if (_service.CurrentRequest.Mode == OrgOperationMode.Sort)
                _service.CurrentRequest.DestTab = tabs[_selectedSourceIndex];
            else
                _service.CurrentRequest.DestTab = tabs[_selectedDestIndex];

            _service.CurrentRequest.Filters = new HashSet<OrgFilterCategory>(_selectedFilters);
            _service.CurrentRequest.SortOrder = Enum.GetValues<OrgSortOrder>()[_selectedSortIndex];
            _service.CurrentRequest.SortDescending = _sortDescending;
        }

        public void Draw()
        {
            var tabs = OrgService.GetAvailableTabs();
            var filterCategories = Enum.GetValues<OrgFilterCategory>();
            var sortOrders = Enum.GetValues<OrgSortOrder>();
            var check = _service.LastCheck;
            var status = _service.JobStatus;
            var style = ImGui.GetStyle();

            bool isMove = _selectedModeIndex == 0;
            bool isSort = _selectedModeIndex == 1;

            if (_selectedSourceIndex == 0)
            {
                _selectedSourceIndex = 1;
                if (_selectedDestIndex == 1) _selectedDestIndex = 2;
            }

            float footerHeight = ImGui.GetFrameHeight() * 2 + style.ItemSpacing.Y;

            float previewHeight = ImGui.GetContentRegionAvail().Y * 0.37f;
            if (previewHeight < 100) previewHeight = 100;

            float settingsHeight = ImGui.GetContentRegionAvail().Y - footerHeight - previewHeight - style.ItemSpacing.Y * 2;

            if (ImGui.BeginChild("SettingsPane", new Vector2(0, settingsHeight), true))
            {
                if (ImGui.BeginTable("ModeTable", 2, ImGuiTableFlags.None))
                {
                    ImGui.TableSetupColumn("Col1", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Col2", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    Vector4 moveBg = isMove ? style.Colors[(int)ImGuiCol.TabActive] : style.Colors[(int)ImGuiCol.FrameBg];
                    ImGui.PushStyleColor(ImGuiCol.Button, moveBg);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, style.Colors[(int)ImGuiCol.TabHovered]);
                    if (status == OrgJobStatus.Running) ImGui.BeginDisabled();
                    if (ImGui.Button("搬移", new Vector2(-1, 30)))
                    {
                        _selectedModeIndex = 0;
                        SyncAndInvalidate();
                    }
                    if (status == OrgJobStatus.Running) ImGui.EndDisabled();
                    ImGui.PopStyleColor(2);
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(status == OrgJobStatus.Running ? "操作進行中" : "在分頁之間搬移物品");

                    ImGui.TableNextColumn();
                    Vector4 sortBg = isSort ? style.Colors[(int)ImGuiCol.TabActive] : style.Colors[(int)ImGuiCol.FrameBg];
                    ImGui.PushStyleColor(ImGuiCol.Button, sortBg);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, style.Colors[(int)ImGuiCol.TabHovered]);
                    if (status == OrgJobStatus.Running) ImGui.BeginDisabled();
                    if (ImGui.Button("排序", new Vector2(-1, 30)))
                    {
                        _selectedModeIndex = 1;
                        SyncAndInvalidate();
                    }
                    if (status == OrgJobStatus.Running) ImGui.EndDisabled();
                    ImGui.PopStyleColor(2);
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(status == OrgJobStatus.Running ? "操作進行中" : "在分頁內重新排列物品");

                    ImGui.EndTable();
                }

                ImGui.Spacing();

                ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                ImGui.CollapsingHeader("搬移設定", ImGuiTreeNodeFlags.Leaf);
                if (ImGui.BeginTable("TransferTable", 4, ImGuiTableFlags.None))
                {
                    ImGui.TableSetupColumn("L1", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("D1", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("L2", ImGuiTableColumnFlags.WidthFixed, 30);
                    ImGui.TableSetupColumn("D2", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableNextRow();

                    if (isSort)
                    {
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text("分頁：");
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.BeginCombo("##TargetTab", OrgService.GetTabDisplayName(tabs[_selectedSourceIndex])))
                        {
                            for (int i = 1; i < tabs.Length; i++)
                            {
                                if (ImGui.Selectable(OrgService.GetTabDisplayName(tabs[i]), i == _selectedSourceIndex))
                                {
                                    _selectedSourceIndex = i;
                                    _selectedDestIndex = i;
                                    SyncAndInvalidate();
                                }
                            }
                            ImGui.EndCombo();
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("要排序的分頁");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                    }
                    else
                    {
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text("從：");
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.BeginCombo("##FromTab", OrgService.GetTabDisplayName(tabs[_selectedSourceIndex])))
                        {
                            for (int i = 1; i < tabs.Length; i++)
                            {
                                if (ImGui.Selectable(OrgService.GetTabDisplayName(tabs[i]), i == _selectedSourceIndex))
                                {
                                    _selectedSourceIndex = i;
                                    if (_selectedDestIndex == _selectedSourceIndex)
                                    {
                                        for (int j = 1; j < tabs.Length; j++)
                                        {
                                            if (j != _selectedSourceIndex)
                                            {
                                                _selectedDestIndex = j;
                                                break;
                                            }
                                        }
                                    }
                                    SyncAndInvalidate();
                                }
                            }
                            ImGui.EndCombo();
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("來源分頁");

                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text("到：");
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.BeginCombo("##ToTab", OrgService.GetTabDisplayName(tabs[_selectedDestIndex])))
                        {
                            for (int i = 1; i < tabs.Length; i++)
                            {
                                bool isSource = (i == _selectedSourceIndex);
                                if (isSource) ImGui.BeginDisabled();
                                if (ImGui.Selectable(OrgService.GetTabDisplayName(tabs[i]), i == _selectedDestIndex))
                                {
                                    _selectedDestIndex = i;
                                    SyncAndInvalidate();
                                }
                                if (isSource) ImGui.EndDisabled();
                            }
                            ImGui.EndCombo();
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("目標分頁");
                    }
                    ImGui.EndTable();
                }

                ImGui.Spacing();

                ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                ImGui.CollapsingHeader("篩選", ImGuiTreeNodeFlags.Leaf);
                
                var sortedCategories = new List<OrgFilterCategory> { OrgFilterCategory.AllItems };
                var otherCats = filterCategories.Where(c => c != OrgFilterCategory.AllItems).OrderBy(c => GetFilterShortName(c)).ToList();
                sortedCategories.AddRange(otherCats);

                if (ImGui.BeginTable("FilterGrid", 4, ImGuiTableFlags.None))
                {
                    ImGui.TableSetupColumn("C1", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("C2", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("C3", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("C4", ImGuiTableColumnFlags.WidthStretch);

                    for (int i = 0; i < sortedCategories.Count; i++)
                    {
                        if (i % 4 == 0) ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        var cat = sortedCategories[i];
                        bool isChecked = _selectedFilters.Contains(cat);
                        string label = GetFilterShortName(cat);

                        if (ImGui.Checkbox($"{label}##F{i}", ref isChecked))
                        {
                            if (cat == OrgFilterCategory.AllItems)
                            {
                                _selectedFilters.Clear();
                                _selectedFilters.Add(OrgFilterCategory.AllItems);
                            }
                            else
                            {
                                if (isChecked)
                                {
                                    _selectedFilters.Remove(OrgFilterCategory.AllItems);
                                    _selectedFilters.Add(cat);
                                }
                                else
                                {
                                    _selectedFilters.Remove(cat);
                                }
                                if (_selectedFilters.Count == 0) _selectedFilters.Add(OrgFilterCategory.AllItems);
                            }
                            SyncAndInvalidate();
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"包含 {label}");
                    }
                    ImGui.EndTable();
                }

                ImGui.Spacing();

                ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                ImGui.CollapsingHeader("排序方式", ImGuiTreeNodeFlags.Leaf);

                var sortOptions = new[] {
                    (Value: OrgSortOrder.ByCategory, Label: "分類"),
                    (Value: OrgSortOrder.ById, Label: "ID"),
                    (Value: OrgSortOrder.ByName, Label: "名稱"),
                    (Value: OrgSortOrder.ByQuantity, Label: "數量")
                };

                if (ImGui.BeginTable("OrderLayout", 2, ImGuiTableFlags.None))
                {
                    ImGui.TableSetupColumn("ComboCol", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("RevCol", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    OrgSortOrder currentOrder = sortOrders[_selectedSortIndex];
                    string currentLabel = sortOptions.FirstOrDefault(x => x.Value == currentOrder).Label ?? currentOrder.ToString();

                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.BeginCombo("##SortCombo", currentLabel))
                    {
                        foreach (var opt in sortOptions)
                        {
                            bool isSelected = opt.Value == currentOrder;
                            if (ImGui.Selectable(opt.Label, isSelected))
                            {
                                _selectedSortIndex = Array.IndexOf(sortOrders, opt.Value);
                                SyncAndInvalidate();
                            }
                        }
                        ImGui.EndCombo();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("物品排序方式");

                    ImGui.TableNextColumn();
                    if (ImGui.Checkbox("反向", ref _sortDescending))
                    {
                        SyncAndInvalidate();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("反轉排序順序");

                    ImGui.EndTable();
                }

                ImGui.EndChild();
            }

            ImGui.Spacing();

            int previewCount = check?.StackCount ?? 0;
            if (ImGui.BeginChild("PreviewPane", new Vector2(0, previewHeight), true))
            {
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                ImGui.CollapsingHeader($"預覽({previewCount} 項)", ImGuiTreeNodeFlags.Leaf);

                if (ImGui.BeginTable("PreviewTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn("物品名稱", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("數量", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("分類", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableHeadersRow();

                    if (check != null && check.PreviewItems.Count > 0)
                    {
                        foreach (var item in check.PreviewItems)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            string mergeTag = item.WillMerge ? " (M)" : "";
                            ItemNameDisplay.Text(item.ItemId, item.ItemName, _config, mergeTag, item.WillMerge ? "將與現有堆疊合併" : null);

                            ImGui.TableNextColumn();
                            ImGui.Text($"{item.Quantity}");

                            ImGui.TableNextColumn();
                            ImGui.TextDisabled(item.CategoryName);
                        }
                    }
                    else
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TextDisabled("點擊「檢查」以預覽...");
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                    }
                    ImGui.EndTable();
                }
                ImGui.EndChild();
            }

            if (ImGui.BeginTable("FooterTable", 2, ImGuiTableFlags.None))
            {
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Button", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (status == OrgJobStatus.Running)
                {
                    ImGui.Text($"狀態：執行中({_service.CompletedMoves}/{_service.TotalMoves})");
                }
                else if (status == OrgJobStatus.Completed)
                {
                    ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), "狀態：已完成!");
                }
                else if (status == OrgJobStatus.Failed)
                {
                    ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1.0f), $"狀態：{_service.StatusMessage}");
                }
                else if (check != null && check.IsValid)
                {
                    ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), $"狀態：就緒({check.StackCount} 項)");
                    ImGui.SameLine();
                    var pColor = check.PlayerBufferOK ? new Vector4(0.5f, 0.8f, 0.5f, 1.0f) : new Vector4(0.8f, 0.5f, 0.5f, 1.0f);
                    var dColor = check.DestCapacityOK ? new Vector4(0.5f, 0.8f, 0.5f, 1.0f) : new Vector4(0.8f, 0.5f, 0.5f, 1.0f);
                    ImGui.TextColored(pColor, $"| 背包：{check.PlayerFreeSlots}");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("背包可用格數");
                    ImGui.SameLine();
                    ImGui.TextColored(dColor, $"| 目標：{check.DestFreeSlots}");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("目標可用格數");
                }
                else if (check != null)
                {
                    ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1.0f), $"狀態：{check.StatusMessage}");
                }
                else
                {
                    ImGui.TextDisabled("狀態：尚未檢查");
                }

                ImGui.TableNextColumn();
                bool isRunning = status == OrgJobStatus.Running;
                bool conflict = !isSort && (_selectedSourceIndex == _selectedDestIndex);
                var gate = _helper.CanStartUserAction();
                bool blocked = !gate.CanRun && !isRunning;
                bool canRun = !conflict && check != null && check.IsValid;
                string buttonLabel = isRunning ? "取消" : (canRun ? GetActionLabel() : "檢查");

                if (conflict || blocked) ImGui.BeginDisabled();
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, isRunning ? new Vector4(0.8f, 0.2f, 0.2f, 1.0f) : style.Colors[(int)ImGuiCol.TabHovered]);
                if (ImGui.Button(buttonLabel, new Vector2(-1, 30)))
                {
                    if (isRunning)
                        _service.Cancel();
                    else if (canRun)
                        _helper.TryStartUserAction(() => _service.Run());
                    else
                        _helper.TryStartUserAction(() => _service.Check());
                }
                ImGui.PopStyleColor();
                if (conflict || blocked) ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    if (conflict) ImGui.SetTooltip("來源與目標不能相同");
                    else if (blocked) ImGui.SetTooltip(gate.Reason);
                    else if (isRunning) ImGui.SetTooltip("取消目前的操作");
                    else if (canRun) ImGui.SetTooltip("執行操作");
                    else ImGui.SetTooltip("檢查操作是否有效");
                }

                ImGui.EndTable();
            }
        }

        private string GetActionLabel()
        {
            return _selectedModeIndex switch
            {
                0 => "搬移物品",
                1 => "排序物品",
                _ => "執行"
            };
        }

        private static string GetFilterShortName(OrgFilterCategory cat)
        {
            return cat switch
            {
                OrgFilterCategory.AllItems => "全部物品",
                OrgFilterCategory.Equipment => "裝備",
                OrgFilterCategory.MedicinesMeals => "藥品/食物",
                OrgFilterCategory.Materials => "素材",
                OrgFilterCategory.Materia => "魔晶石",
                OrgFilterCategory.Registrable => "可登錄",
                OrgFilterCategory.Dye => "染劑",
                OrgFilterCategory.Housing => "房屋",
                OrgFilterCategory.Gardening => "園藝",
                OrgFilterCategory.Miscellaneous => "雜項",
                _ => cat.ToString()
            };
        }

        private void SyncAndInvalidate()
        {
            SyncRequestFromUI();
            _service.Reset();
        }

        private void DebugLog(string msg)
        {
            if (!_config.DebugMode) return;
            FCCH.Common.FCCHLog.Info($"[OrganizerTab] {msg}");
            ChatHelper.Debug($"[OrgTab] {msg}");
        }

        public unsafe void Update()
        {
            _service.Update();

            var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>(Constants.FC_CHEST_ADDON_NAME, 1);
            bool isChestOpen = addon != null && addon->IsVisible;

            if (_wasChestOpen && !isChestOpen)
            {
                _chestClosedFrames++;
                if (_chestClosedFrames >= CHEST_CLOSE_THRESHOLD)
                {
                    DebugLog("Chest closed (confirmed). Resetting check.");
                    _service.Reset();
                    SyncRequestFromUI();
                    _chestClosedFrames = 0;
                }
            }
            else
            {
                _chestClosedFrames = 0;
            }
            _wasChestOpen = isChestOpen;
        }

        public void Dispose()
        {
        }
    }
}
