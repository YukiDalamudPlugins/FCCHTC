using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using FCCH;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Colors;

namespace FCCH.UI
{
    public class GeneralTab
    {
        private readonly Configuration _configuration;
        private readonly FileDialogManager _fileDialogManager;
        private readonly DragDropHelper<ToolbarButtonConfig> _toolbarButtonDrag = new("FCCHToolbarButton", x => x.Id.ToString());

        public GeneralTab(Configuration configuration, FileDialogManager fileDialogManager)
        {
            _configuration = configuration;
            _fileDialogManager = fileDialogManager;
        }

        public void Draw()
        {
            if (ImGui.BeginChild("GeneralTabScroll", new Vector2(0, 0), true))
            {
                if (DrawSection("音效"))
                {
                    DrawSettingRow("完成音效", () =>
                    {
                        bool playSound = _configuration.PlayCompletionSound;
                        if (ImGui.Checkbox("##complSound", ref playSound))
                        {
                            _configuration.PlayCompletionSound = playSound;
                            _configuration.Save();
                        }
                    });

                    DrawSettingRow("自訂音效路徑", () =>
                    {
                        string path = _configuration.CustomSoundPath;
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 35);
                        if (ImGui.InputTextWithHint("##soundPath", "預設：Assets\\Completion.mp3", ref path, 1000))
                        {
                            _configuration.CustomSoundPath = path;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.Folder.ToIconString() + "##soundBrowse"))
                        {
                            _fileDialogManager.OpenFileDialog("選擇音效檔", "音效檔{.mp3,.wav}", (success, selectedPath) =>
                            {
                                if (success)
                                {
                                    _configuration.CustomSoundPath = selectedPath;
                                    _configuration.Save();
                                }
                            });
                        }
                        ImGui.PopFont();
                    });
                }
                ImGui.Spacing();

                if (DrawSection("確認提示"))
                {
                    DrawSettingRow("略過存入確認", () =>
                    {
                        bool disableDep = _configuration.DisableAskDepositAll;
                        if (ImGui.Checkbox("##skipDep", ref disableDep))
                        {
                            _configuration.DisableAskDepositAll = disableDep;
                            _configuration.Save();
                        }
                    });

                    DrawSettingRow("略過取出確認", () =>
                    {
                        bool disableWith = _configuration.DisableAskWithdrawAll;
                        if (ImGui.Checkbox("##skipWith", ref disableWith))
                        {
                            _configuration.DisableAskWithdrawAll = disableWith;
                            _configuration.Save();
                        }
                    });
                }
                ImGui.Spacing();

                if (DrawSection("工具列"))
                {
                    DrawSettingRow("鎖定工具列位置", () =>
                    {
                        bool locked = _configuration.ToolbarLocked;
                        if (ImGui.Checkbox("##toolbarLocked", ref locked))
                        {
                            _configuration.ToolbarLocked = locked;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("(?)");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("鎖定時工具列維持在目前位置;解鎖後可自由拖曳。");
                    });

                    DrawSettingRow("貼齊寶物庫", () =>
                    {
                        if (ImGui.Button("貼齊##toolbarSnap", new Vector2(120, 0)))
                        {
                            _configuration.ToolbarPosX = -1f;
                            _configuration.ToolbarPosY = -1f;
                            _configuration.Save();
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("將工具列位置重設回部隊寶物庫上方的附著位置。");
                    });

                    DrawSettingRow("貼齊格線", () =>
                    {
                        bool snapGrid = _configuration.ToolbarSnapToGrid;
                        if (ImGui.Checkbox("##toolbarSnapGrid", ref snapGrid))
                        {
                            _configuration.ToolbarSnapToGrid = snapGrid;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("(?)");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("解鎖時拖曳工具列會貼齊 10px 格線。");
                    });

                    DrawToolbarButtonLayout();
                }
                ImGui.Spacing();

                if (DrawSection("行為規則"))
                {
                    DrawSettingRow("存入時降級為 NQ", () =>
                    {
                        bool lowerQuality = _configuration.LowerQualityOnDeposit;
                        if (ImGui.Checkbox("##lowerQual", ref lowerQuality))
                        {
                            _configuration.LowerQualityOnDeposit = lowerQuality;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("(?)");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("存入前自動將 HQ 物品轉為 NQ。");
                    });

                    DrawSettingRow("每疊保留一個", () =>
                    {
                        bool leaveOne = _configuration.LeaveOneItemPerStack;
                        if (ImGui.Checkbox("##leaveOne", ref leaveOne))
                        {
                            _configuration.LeaveOneItemPerStack = leaveOne;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("(?)");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("取出時部隊寶物庫至少保留 1 個物品。");
                    });

                    DrawSettingRow("精簡物品名稱", () =>
                    {
                        bool compactNames = _configuration.CompactItemNames;
                        if (ImGui.Checkbox("##compactItemNames", ref compactNames))
                        {
                            _configuration.CompactItemNames = compactNames;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("(?)");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("在自訂、忽略、整理清單中縮短支援的物品名稱。");
                    });

                    DrawSettingRow("物品右鍵選單", () =>
                    {
                        bool enabled = _configuration.EnableItemContextMenuEntries;
                        if (ImGui.Checkbox("##itemContextMenu", ref enabled))
                        {
                            _configuration.EnableItemContextMenuEntries = enabled;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("(?)");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("在支援的物品右鍵選單中加入 FCCH 項目。");
                    });
                }
                ImGui.Spacing();

                if (DrawSection("延遲時間"))
                {
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.1f, 0.5f));
                    if (ImGui.BeginChild("TimingWarning", new Vector2(ImGui.GetContentRegionAvail().X, 40), true))
                    {
                        ImGui.TextColored(ImGuiColors.DalamudOrange, "延遲過低在連線較慢時可能造成不同步。");
                    }
                    ImGui.EndChild();
                    ImGui.PopStyleColor();

                    ImGui.Spacing();

                    DrawSettingRow("存入延遲", () =>
                    {
                        int depositDelay = _configuration.MoveDelayInMs;
                        ImGui.SetNextItemWidth(180);
                        if (ImGui.SliderInt("##depDelay", ref depositDelay, 700, 1500, "%d ms"))
                        {
                            _configuration.MoveDelayInMs = depositDelay;
                            _configuration.Save();
                        }
                    });

                    DrawSettingRow("取出延遲", () =>
                    {
                        int withdrawDelay = _configuration.WithdrawDelayInMs;
                        ImGui.SetNextItemWidth(180);
                        if (ImGui.SliderInt("##withDelay", ref withdrawDelay, 700, 1500, "%d ms"))
                        {
                            _configuration.WithdrawDelayInMs = withdrawDelay;
                            _configuration.Save();
                        }
                    });
                }
                ImGui.Spacing();

                if (DrawSection("診斷"))
                {
                    DrawSettingRow("啟用除錯模式", () =>
                    {
                        bool debug = _configuration.DebugMode;
                        if (ImGui.Checkbox("##debugMode", ref debug))
                        {
                            _configuration.DebugMode = debug;
                            _configuration.Save();
                        }
                    });

                    DrawSettingRow("自訂除錯紀錄路徑", () =>
                    {
                        string logPath = _configuration.DebugLogPath;
                        ImGui.SetNextItemWidth(220f);
                        if (ImGui.InputTextWithHint("##logPath", "預設：FCCH_Debug.log", ref logPath, 256))
                        {
                            _configuration.DebugLogPath = logPath;
                            _configuration.Save();
                        }
                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.Folder.ToIconString() + "##logBrowse"))
                        {
                            _fileDialogManager.SaveFileDialog("選擇紀錄檔", ".log", "FCCH_Debug.log", ".log", (success, selectedPath) =>
                            {
                                if (success)
                                {
                                    _configuration.DebugLogPath = selectedPath;
                                    _configuration.Save();
                                }
                            });
                        }
                        ImGui.PopFont();
                    });

                    DrawSettingRow("詳細紀錄", () =>
                    {
                        bool verbose = _configuration.VerboseMode;
                        if (ImGui.Checkbox("##verbose", ref verbose))
                        {
                            _configuration.VerboseMode = verbose;
                            _configuration.Save();
                        }
                    });

                    ImGui.Spacing();
                    ImGui.TextDisabled("內部診斷指令");
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.1f, 0.5f));
                    float diagnosticBoxHeight = ImGui.GetTextLineHeightWithSpacing() * 5 + ImGui.GetStyle().WindowPadding.Y * 2;
                    if (ImGui.BeginChild("InternalDiagnosticsBox", new Vector2(ImGui.GetContentRegionAvail().X, diagnosticBoxHeight), true))
                    {
                        ImGui.TextColored(ImGuiColors.DalamudOrange, "debug - 切換除錯紀錄");
                        ImGui.TextColored(ImGuiColors.DalamudOrange, "gildebug - 追蹤金幣 callback");
                        ImGui.TextColored(ImGuiColors.DalamudOrange, "accessprobe - 輸出目前寶物庫權限狀態");
                        ImGui.TextColored(ImGuiColors.DalamudOrange, "fcperms [row] - 輸出部隊階級權限原始位元組");
                        ImGui.TextColored(ImGuiColors.DalamudOrange, "ipctest - 呼叫 FCCH IPC 並將結果輸出到 /xllog");
                    }
                    ImGui.EndChild();
                    ImGui.PopStyleColor();
                }
                ImGui.Spacing();

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidth = 130;
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - buttonWidth) * 0.5f + ImGui.GetCursorPosX());
                var style = ImGui.GetStyle();
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, style.Colors[(int)ImGuiCol.TabHovered]);
                if (ImGui.Button("重設為預設值", new Vector2(buttonWidth, 0)))
                {
                    _configuration.PlayCompletionSound = false;
                    _configuration.CustomSoundPath = "";
                    _configuration.DisableAskDepositAll = false;
                    _configuration.DisableAskWithdrawAll = false;
                    _configuration.LowerQualityOnDeposit = false;
                    _configuration.LeaveOneItemPerStack = false;
                    _configuration.ToolbarLocked = true;
                    _configuration.ToolbarPosX = -1f;
                    _configuration.ToolbarPosY = -1f;
                    _configuration.ToolbarSnapToGrid = false;
                    _configuration.ResetToolbarButtons();
                    _configuration.MoveDelayInMs = 700;
                    _configuration.WithdrawDelayInMs = 700;
                    _configuration.DebugMode = false;
                    _configuration.DebugLogPath = "";
                    _configuration.VerboseMode = false;
                    _configuration.CompactItemNames = true;
                    _configuration.EnableItemContextMenuEntries = false;
                    _configuration.Save();
                }
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("將所有「一般」設定重設為預設值。");

                ImGui.EndChild();
            }
        }

        private void DrawSettingRow(string label, System.Action drawControl)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text(label);
            ImGui.SameLine(180);
            drawControl();
        }

        private static bool DrawSection(string label)
        {
            ImGui.SetNextItemOpen(true, ImGuiCond.FirstUseEver);
            return ImGui.CollapsingHeader(label);
        }

        private void DrawToolbarButtonLayout()
        {
            if (_configuration.EnsureToolbarButtons())
                _configuration.Save();

            DrawSettingRow("工具列按鈕", () =>
            {
                if (ImGui.Button("重設##toolbarButtonsReset", new Vector2(120, 0)))
                {
                    _configuration.ResetToolbarButtons();
                    _configuration.Save();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("還原預設的工具列按鈕順序與顯示。");
            });

            var tableWidth = CalculateToolbarButtonTableWidth();
            if (!ImGui.BeginTable("ToolbarButtonLayout", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoHostExtendX, new Vector2(tableWidth, 0)))
                return;

            ImGui.TableSetupColumn("Move", ImGuiTableColumnFlags.WidthFixed, 22);
            ImGui.TableSetupColumn("Show", ImGuiTableColumnFlags.WidthFixed, 22);
            ImGui.TableSetupColumn("Button", ImGuiTableColumnFlags.WidthFixed, tableWidth - 44);

            _toolbarButtonDrag.Begin();
            var visibleCount = CountVisibleToolbarButtons();
            for (var i = 0; i < _configuration.ToolbarButtons.Count; i++)
            {
                var button = _configuration.ToolbarButtons[i];
                ImGui.PushID($"toolbar_button_{button.Id}");
                _toolbarButtonDrag.NextRow();
                ImGui.TableNextRow();
                _toolbarButtonDrag.SetRowColor(button);

                ImGui.TableNextColumn();
                _toolbarButtonDrag.DrawButtonDummy(button, _configuration.ToolbarButtons, i, _ => _configuration.Save());

                ImGui.TableNextColumn();
                DrawToolbarButtonToggle(button, visibleCount);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(GetToolbarButtonLabel(button.Id));

                ImGui.PopID();
            }

            ImGui.EndTable();
            _toolbarButtonDrag.End();
        }

        private float CalculateToolbarButtonTableWidth()
        {
            var maxLabelWidth = 0f;
            foreach (var button in _configuration.ToolbarButtons)
            {
                var labelWidth = ImGui.CalcTextSize(GetToolbarButtonLabel(button.Id)).X;
                if (labelWidth > maxLabelWidth) maxLabelWidth = labelWidth;
            }

            var style = ImGui.GetStyle();
            return 44f + maxLabelWidth + style.CellPadding.X * 4f + 4f;
        }

        private void DrawToolbarButtonToggle(ToolbarButtonConfig button, int visibleCount)
        {
            var visible = button.IsVisible;
            var mustKeepVisible = visible && visibleCount <= 1;
            if (mustKeepVisible) ImGui.BeginDisabled();

            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.TabHovered]);
            if (visible) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            if (ImGui.SmallButton($"{(visible ? FontAwesomeIcon.ToggleOn : FontAwesomeIcon.ToggleOff).ToIconString()}##visible"))
            {
                button.IsVisible = !visible;
                _configuration.Save();
            }
            if (visible) ImGui.PopStyleColor();
            ImGui.PopStyleColor(2);
            ImGui.PopFont();

            if (mustKeepVisible) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(mustKeepVisible ? "至少要保留一個工具列按鈕顯示。" : visible ? "顯示於工具列" : "已從工具列隱藏");
        }

        private int CountVisibleToolbarButtons()
        {
            var count = 0;
            foreach (var button in _configuration.ToolbarButtons)
            {
                if (button.IsVisible) count++;
            }
            return count;
        }

        private static string GetToolbarButtonLabel(ToolbarButtonId id)
        {
            return id switch
            {
                ToolbarButtonId.Settings => "設定",
                ToolbarButtonId.Deposit => "存入",
                ToolbarButtonId.DepositCustom => "存入自訂清單",
                ToolbarButtonId.DepositDuplicates => "存入重複物品",
                ToolbarButtonId.Crystals => "水晶",
                ToolbarButtonId.Withdraw => "取出",
                ToolbarButtonId.WithdrawCustom => "取出自訂清單",
                ToolbarButtonId.WithdrawWorkshop => "取出工房清單",
                _ => id.ToString()
            };
        }
    }
}
