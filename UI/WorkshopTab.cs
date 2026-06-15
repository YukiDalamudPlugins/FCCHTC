using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Excel.Sheets;
using FCCH.Common;
using FCCH.GameData;
using FCCH.IPC;
using FCCH.Models;
using FCCH.Managers;

namespace FCCH.UI
{
    public unsafe class WorkshopTab
    {
        private readonly ChestHelper _helper;
        private readonly Configuration _configuration;
        private readonly WorkshopCache _cache;
        private readonly WorkshoppaIPC _workshoppaIPC;

        private string _searchFilter = "";

        private string _presetNameInput = "";
        private string _selectedPresetName = "";
        private bool _showSavePresetModal = false;

        private HashSet<int> _expandedProjects = new HashSet<int>();

        private bool _wasTabActive;
        private int _lastShoppingListSignature;

        public WorkshopTab(ChestHelper helper, Configuration configuration, WorkshopCache cache, WorkshoppaIPC workshoppaIPC)
        {
            _helper = helper;
            _configuration = configuration;
            _cache = cache;
            _workshoppaIPC = workshoppaIPC;
        }

        public void Draw()
        {
            MaybeAutoRefresh();

            DrawPresets();
            ImGui.Separator();

            var totalMats = GetTotalMaterials();
            int missingCount = totalMats.Count(m => m.Have < m.Need);
            int projectCount = _helper.ShoppingList.Count;

            if (ImGui.BeginTable("##workshopHeader", 4))
            {
                ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##clearlist", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("##queue", ImGuiTableColumnFlags.WidthFixed, 70);
                ImGui.TableSetupColumn("##clearws", ImGuiTableColumnFlags.WidthFixed, 70);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextDisabled($"專案({projectCount})| 材料：{totalMats.Count},缺 {missingCount}");

                ImGui.TableNextColumn();
                if (projectCount > 0)
                {
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
                    if (ImGui.Button("清空清單", new Vector2(-1, 0)))
                    {
                        _helper.ShoppingList.Clear();
                        _expandedProjects.Clear();
                    }
                    ImGui.PopStyleColor();
                }

                ImGui.TableNextColumn();
                if (projectCount > 0 && _workshoppaIPC.IsAvailable)
                {
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
                    if (ImGui.Button("排入佇列", new Vector2(-1, 0)))
                    {
                        int success = 0;
                        foreach (var item in _helper.ShoppingList)
                        {
                            if (_workshoppaIPC.AddQueueItem(item.Craft.WorkshopItemId, item.Quantity))
                                success++;
                        }
                        if (success > 0)
                            Common.ChatHelper.Info($"\u5df2\u5c07 {success} \u500b\u5c08\u6848\u6392\u5165 Workshoppa\u3002");
                        else
                            Common.ChatHelper.Warning("\u6392\u5165\u5931\u6557 \u2014 Workshoppa \u662f\u5426\u5fd9\u788c\u4e2d?");
                    }
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("將專案送進 Workshoppa 佇列");
                }

                ImGui.TableNextColumn();
                if (_workshoppaIPC.IsAvailable)
                {
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
                    if (ImGui.Button("\u6e05\u7a7a WS", new Vector2(-1, 0)))
                    {
                        if (_workshoppaIPC.ClearQueue())
                            Common.ChatHelper.Info("\u5df2\u6e05\u7a7a Workshoppa \u4f47\u5217\u3002");
                        else
                            Common.ChatHelper.Warning("\u6e05\u7a7a\u5931\u6557 \u2014 Workshoppa \u662f\u5426\u5fd9\u788c\u4e2d?");
                    }
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("清空 Workshoppa 佇列");
                }
                ImGui.EndTable();
            }

            float footerHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 2;

            float matsHeight = 0;
            var storage = ImGui.GetStateStorage();
            uint matsId = ImGui.GetID("TotalMaterialsHeader");
            bool matsOpen = storage.GetBool(matsId, true);

            if (matsOpen)
            {
                matsHeight = ImGui.GetContentRegionAvail().Y * 0.30f;
                if (matsHeight < 120) matsHeight = 120;
            }
            else
            {
                matsHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;
            }

            float projectsHeight = ImGui.GetContentRegionAvail().Y - footerHeight - matsHeight - ImGui.GetStyle().ItemSpacing.Y;

            if (ImGui.BeginChild("ProjectsPane", new Vector2(0, projectsHeight), true))
            {
                if (_helper.ShoppingList.Count == 0)
                {
                    ImGui.TextDisabled("沒有工房專案。");
                    ImGui.TextDisabled("用下方搜尋加入。");
                }
                else
                {
                    var sortedProjects = _helper.ShoppingList
                        .Select((item, idx) => new { Item = item, Index = idx })
                        .OrderBy(x => x.Item.Craft.Name)
                        .ToList();

                    foreach (var proj in sortedProjects)
                    {
                        ImGui.PushID($"proj_{proj.Index}");
                        DrawProjectRow(proj.Item, proj.Index);
                        ImGui.PopID();
                    }
                }
                ImGui.EndChild();
            }

            ImGui.Spacing();

            if (ImGui.BeginChild("WorkshopMaterialsLoc", new Vector2(0, matsHeight), true))
            {
                if (ImGui.CollapsingHeader($"所需材料總計({totalMats.Count})###TotalMaterialsHeader", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (totalMats.Count == 0)
                    {
                        ImGui.TextDisabled("不需要材料。");
                    }
                    else
                    {
                        if (ImGui.BeginTable("TotalMatsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                        {
                            ImGui.TableSetupScrollFreeze(0, 1);
                            ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("需要", ImGuiTableColumnFlags.WidthFixed, 50);
                            ImGui.TableSetupColumn("持有", ImGuiTableColumnFlags.WidthFixed, 50);
                            ImGui.TableSetupColumn("差額", ImGuiTableColumnFlags.WidthFixed, 60);
                            ImGui.TableHeadersRow();

                            foreach (var mat in totalMats)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text(mat.Name);

                                ImGui.TableNextColumn();
                                ImGui.Text(mat.Need.ToString());

                                ImGui.TableNextColumn();
                                if (mat.Have >= mat.Need)
                                    ImGui.TextColored(ImGuiColors.HealerGreen, mat.Have.ToString());
                                else
                                    ImGui.TextColored(ImGuiColors.DalamudRed, mat.Have.ToString());

                                ImGui.TableNextColumn();
                                long diff = mat.Have - mat.Need;
                                if (diff >= 0)
                                    ImGui.TextColored(ImGuiColors.HealerGreen, $"+{diff}");
                                else
                                    ImGui.TextColored(ImGuiColors.DalamudRed, diff.ToString());
                            }
                            ImGui.EndTable();
                        }
                    }
                }
                ImGui.EndChild();
            }

            DrawSearchBox();

            DrawSavePresetModal();
        }

        private void DrawSearchBox()
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0, 0, 0, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.2f, 0.2f, 0.2f, 1f));

            float refreshWidth = 80;
            float searchWidth = ImGui.GetContentRegionAvail().X - refreshWidth - ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetNextItemWidth(searchWidth);
            if (ImGui.BeginCombo("##addCraftSearch", "搜尋工房專案...", ImGuiComboFlags.HeightLarge | ImGuiComboFlags.PopupAlignLeft))
            {
                ImGui.PopStyleColor(2);
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##searchInC", ref _searchFilter, 64);
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    var filtered = _cache.Crafts.Where(c => c.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).Take(20);
                    if (filtered.Any())
                    {
                        foreach (var craft in filtered)
                        {
                            if (ImGui.Selectable(craft.Name, false))
                            {
                                AddProject(craft);
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("找不到專案");
                    }
                }
                ImGui.EndCombo();
            }
            else
            {
                ImGui.PopStyleColor(2);
            }

            ImGui.SameLine();
            var gate = _helper.CanStartUserAction();
            if (!gate.CanRun) ImGui.BeginDisabled();
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
            if (ImGui.Button("重新整理", new Vector2(refreshWidth, 0)))
            {
                TryRefreshChestData();
            }
            ImGui.PopStyleColor();
            if (!gate.CanRun) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(!gate.CanRun
                    ? gate.Reason
                    : IsChestAddonVisible()
                    ? "重新整理寶物庫資料"
                    : "重新整理寶物庫資料\n請開啟部隊寶物庫以更新。");
            }
        }

        private void MaybeAutoRefresh()
        {
            int signature = ComputeShoppingListSignature();
            bool tabJustActivated = !_wasTabActive;
            bool listChanged = signature != _lastShoppingListSignature;
            _wasTabActive = true;

            if (_helper.ShoppingList.Count == 0)
            {
                _lastShoppingListSignature = signature;
                return;
            }

            if (!tabJustActivated && !listChanged) return;

            _lastShoppingListSignature = signature;

            TryRefreshChestData();
        }

        public void OnTabDeactivated()
        {
            _wasTabActive = false;
        }

        private void TryRefreshChestData()
        {
            if (!_helper.CanStartUserAction().CanRun) return;
            if (!IsChestAddonVisible()) return;
            _helper.StartIndexing(false);
        }

        private static bool IsChestAddonVisible()
        {
            var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>("FreeCompanyChest", 1);
            return addon != null && addon->IsVisible;
        }

        private int ComputeShoppingListSignature()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _helper.ShoppingList.Count;
                foreach (var item in _helper.ShoppingList)
                {
                    hash = hash * 31 + (int)item.Craft.WorkshopItemId;
                    hash = hash * 31 + item.Quantity;
                }
                return hash;
            }
        }

        private void AddProject(WorkshopCraft craft)
        {
            var existing = _helper.ShoppingList.FirstOrDefault(x => x.Craft.WorkshopItemId == craft.WorkshopItemId);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                var newItem = new ShoppingItem { Craft = craft, Quantity = 1 };
                _helper.ShoppingList.Add(newItem);
            }

            _helper.ShoppingList.Sort((a, b) => string.Compare(a.Craft.Name, b.Craft.Name, StringComparison.OrdinalIgnoreCase));

            _searchFilter = "";
        }

        private void DrawProjectRow(ShoppingItem item, int index)
        {
            var materials = item.Craft.Phases
                .SelectMany(p => p.Items)
                .Select(x => new { Item = x, Required = x.TotalQuantity * item.Quantity })
                .GroupBy(x => x.Item.ItemId)
                .Select(g => new { ItemId = g.Key, Name = g.First().Item.Name, TotalNeeded = g.Sum(x => x.Required) })
                .OrderBy(m => m.Name)
                .ToList();

            int readyCount = 0;
            int totalCount = materials.Count;
            foreach (var mat in materials)
            {
                long have = _helper.GetItemCountInChest(mat.ItemId) + _helper.GetItemCountInPlayerInventory(mat.ItemId);
                if (have >= mat.TotalNeeded) readyCount++;
            }

            bool isReady = readyCount == totalCount;
            bool isExpanded = _expandedProjects.Contains(index);

            if (ImGui.BeginTable($"ProjectRow{index}", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Project", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn("Max", ImGuiTableColumnFlags.WidthFixed, CellActionButton.ColumnWidth);
                ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, CellActionButton.ColumnWidth);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                string arrow = isExpanded ? "▼" : "▶";

                if (ImGui.Selectable($"{arrow} {item.Craft.Name}##sel{index}", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
                {
                    if (isExpanded)
                        _expandedProjects.Remove(index);
                    else
                        _expandedProjects.Add(index);
                }

                ImGui.TableNextColumn();
                int qty = item.Quantity;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt($"##pQty{index}", ref qty, 0))
                {
                    if (qty < 1) qty = 1;
                    item.Quantity = qty;
                }

                ImGui.TableNextColumn();
                if (isReady)
                    ImGui.TextColored(ImGuiColors.HealerGreen, "完成");
                else
                    ImGui.TextColored(ImGuiColors.DalamudOrange, $"{readyCount}/{totalCount}");

                ImGui.TableNextColumn();
                CellActionButton.DrawText("M", $"max{index}", "可製作的最大量", () =>
                {
                    item.Quantity = CalculateMaxCraft(item.Craft);
                });

                ImGui.TableNextColumn();
                CellActionButton.DrawIcon(FontAwesomeIcon.Minus, $"delete{index}", "移除", () =>
                {
                    _helper.ShoppingList.RemoveAt(index);
                    _expandedProjects.Remove(index);
                }, true);

                ImGui.EndTable();
            }

            if (isExpanded)
            {
                ImGui.Indent(20);
                if (ImGui.BeginTable($"MatTable{index}", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("需要", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("持有", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableHeadersRow();

                    foreach (var mat in materials)
                    {
                        long haveFC = _helper.GetItemCountInChest(mat.ItemId);
                        long havePl = _helper.GetItemCountInPlayerInventory(mat.ItemId);
                        long total = haveFC + havePl;
                        bool isComplete = total >= mat.TotalNeeded;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(mat.Name);
                        ImGui.TableNextColumn();
                        ImGui.Text(mat.TotalNeeded.ToString());
                        ImGui.TableNextColumn();
                        ImGui.TextColored(isComplete ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, total.ToString());
                    }
                    ImGui.EndTable();
                }
                ImGui.Unindent(20);
                ImGui.Spacing();
            }
        }

        private List<(string Name, uint ItemId, long Need, long Have)> GetTotalMaterials()
        {
            var totalMap = new Dictionary<uint, long>();
            foreach (var shopItem in _helper.ShoppingList)
            {
                var mats = shopItem.Craft.Phases.SelectMany(p => p.Items).Select(x => new { Item = x, Req = x.TotalQuantity * shopItem.Quantity });
                foreach (var mat in mats)
                {
                    if (!totalMap.ContainsKey(mat.Item.ItemId)) totalMap[mat.Item.ItemId] = 0;
                    totalMap[mat.Item.ItemId] += mat.Req;
                }
            }

            return totalMap
                .Select(kvp =>
                {
                    var name = _helper.GetItemName(kvp.Key);
                    var haveFC = _helper.GetItemCountInChest(kvp.Key);
                    var havePlayer = _helper.GetItemCountInPlayerInventory(kvp.Key);
                    return (Name: name, ItemId: kvp.Key, Need: kvp.Value, Have: haveFC + havePlayer);
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        private void DrawPresets()
        {
            float avail = ImGui.GetContentRegionAvail().X;
            ImGui.SetNextItemWidth(avail * 0.45f);

            if (ImGui.BeginCombo("##workPresetSel", string.IsNullOrEmpty(_selectedPresetName) ? "載入預設集..." : _selectedPresetName))
            {
                foreach (var presetName in _configuration.WorkshopPresets.Keys)
                {
                    if (ImGui.Selectable(presetName, _selectedPresetName == presetName))
                    {
                        LoadPreset(presetName);
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine(0, 5);

            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Save.ToIconString()))
            {
                _showSavePresetModal = true;
                _presetNameInput = "";
            }
            ImGui.PopFont();
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("將目前專案儲存為預設集");

            ImGui.SameLine(0, 5);

            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1f));
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString()) && !string.IsNullOrEmpty(_selectedPresetName))
            {
                if (_configuration.WorkshopPresets.ContainsKey(_selectedPresetName))
                {
                    _configuration.WorkshopPresets.Remove(_selectedPresetName);
                    _configuration.Save();
                    _selectedPresetName = "";
                }
            }
            ImGui.PopFont();
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("刪除預設集");

            ImGui.SameLine(0, 15);

            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
            if (ImGui.Button("匯出"))
            {
                var exportData = _helper.ShoppingList.Select(x => new PresetShoppingItem
                {
                    WorkshopItemId = x.Craft.WorkshopItemId,
                    Quantity = x.Quantity
                }).ToList();

                if (Common.ExportHelper.Export(Common.ExportHelper.HEADER_WORKSHOP, exportData))
                {
                    Common.ChatHelper.Info($"已匯出 {exportData.Count} 個工房專案到剪貼簿。");
                }
                else
                {
                    Common.ChatHelper.Warning("匯出工房專案失敗。");
                }
            }
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("匯出到剪貼簿");

            ImGui.SameLine(0, 5);

            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
            if (ImGui.Button("匯入"))
            {
                var (result, data) = Common.ExportHelper.Import<List<PresetShoppingItem>>(Common.ExportHelper.HEADER_WORKSHOP);
                if (result == Common.ExportHelper.ImportResult.Success && data != null)
                {
                    _helper.ShoppingList.Clear();
                    foreach (var item in data)
                    {
                        var craft = _cache.Crafts.FirstOrDefault(c => c.WorkshopItemId == item.WorkshopItemId);
                        if (craft != null)
                        {
                            _helper.ShoppingList.Add(new ShoppingItem { Craft = craft, Quantity = item.Quantity });
                        }
                    }
                    Common.ChatHelper.Info($"已匯入 {data.Count} 個工房專案。");
                }
                else
                {
                    Common.ChatHelper.Warning(Common.ExportHelper.GetErrorMessage(result, "工房清單"));
                }
            }
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("從剪貼簿匯入");
        }

        private void LoadPreset(string name)
        {
            if (_configuration.WorkshopPresets.TryGetValue(name, out var savedItems))
            {
                _selectedPresetName = name;
                _helper.ShoppingList.Clear();
                _expandedProjects.Clear();
                foreach (var item in savedItems)
                {
                    var craft = _cache.Crafts.FirstOrDefault(c => c.WorkshopItemId == item.WorkshopItemId);
                    if (craft != null)
                    {
                        _helper.ShoppingList.Add(new ShoppingItem { Craft = craft, Quantity = item.Quantity });
                    }
                }
            }
        }

        private void DrawSavePresetModal()
        {
            if (_showSavePresetModal) ImGui.OpenPopup("儲存工房預設集");

            if (ImGui.BeginPopupModal("儲存工房預設集", ref _showSavePresetModal, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("輸入預設集名稱：");
                ImGui.InputText("##wkPresetName", ref _presetNameInput, 64);

                ImGui.Spacing();

                if (ImGui.Button("儲存", new Vector2(120, 0)))
                {
                    if (!string.IsNullOrWhiteSpace(_presetNameInput))
                    {
                        var listCopy = _helper.ShoppingList.Select(x => new PresetShoppingItem
                        {
                            WorkshopItemId = x.Craft.WorkshopItemId,
                            Quantity = x.Quantity
                        }).ToList();

                        _configuration.WorkshopPresets[_presetNameInput] = listCopy;
                        _configuration.Save();
                        _selectedPresetName = _presetNameInput;
                        _showSavePresetModal = false;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("取消", new Vector2(120, 0)))
                {
                    _showSavePresetModal = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private int CalculateMaxCraft(WorkshopCraft craft)
        {
            var materials = craft.Phases
               .SelectMany(p => p.Items)
               .Select(x => new { ItemId = x.ItemId, PerDraft = x.TotalQuantity })
               .GroupBy(x => x.ItemId)
               .Select(g => new { ItemId = g.Key, RequiredPerUnit = g.Sum(x => x.PerDraft) });

            long maxPossible = long.MaxValue;

            foreach (var mat in materials)
            {
                long effectiveFC = _helper.GetItemCountInChest(mat.ItemId);
                long player = _helper.GetItemCountInPlayerInventory(mat.ItemId);
                long totalAvail = effectiveFC + player;

                long canMake = totalAvail / mat.RequiredPerUnit;
                if (canMake < maxPossible) maxPossible = canMake;
            }

            if (maxPossible < 0) maxPossible = 0;
            if (maxPossible > 9999) maxPossible = 9999;
            return Math.Max(1, (int)maxPossible);
        }
    }
}
