using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns growth-map navigation, node visibility, hover details and purchase UI.</summary>
    internal sealed class GrowthGraphView
    {
        private readonly GameSettings settings;
        private readonly SaveService save;
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly WorldArtAssets art;
        private readonly RunController run;
        private readonly GameNotifications notifications;
        private readonly UiGraphics graphics;

        internal GrowthGraphView(GameSettings settings, SaveService save, GameTables tables, ProgressionService progression, WorldArtAssets art, RunController run, GameNotifications notifications, UiGraphics graphics)
        {
            this.settings = settings;
            this.save = save;
            this.tables = tables;
            this.progression = progression;
            this.art = art;
            this.run = run;
            this.notifications = notifications;
            this.graphics = graphics;
        }

        internal Vector2 metaTreePan;

        internal float metaTreeZoom = .75f;

        internal int selectedMetaTreeNode;

        internal int hoveredMetaTreeNode = -1;

        internal bool isPanningMetaTree;

        internal Vector2 metaTreeLastPointer;

        internal MetaTreeNodePresentation[] metaTreeNodes = System.Array.Empty<MetaTreeNodePresentation>();

        internal void DrawMetaTree()
        {
            // The tree owns the entire screen instead of appearing as an in-game popup.
            UiGraphics.DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(.075f, .038f, .014f, 1f));
            BuildPrototypeGraph();
            Rect treeCanvas = new Rect(0f, 0f, Screen.width, Screen.height);
            if (!prototypeGraphViewInitialized || prototypeGraphCanvasSize != treeCanvas.size) FitPrototypeGraph(treeCanvas.size);
            HandleMetaTreeInput(treeCanvas);
            DrawMetaTreeCanvas(treeCanvas);
            DrawPrototypeGrowthDetails();
            DrawGrowthWallet();
            DrawPrototypeGraphOverview();
            // This is a navigation button, not a popup/header; the graph still owns the whole screen.
            Rect navigationArea = GrowthNavigationRect;
            Rect navigation = new Rect(navigationArea.x, navigationArea.yMax - 44, navigationArea.width, 44);
            if (!run.runEnded && graphics.DrawResultButton(new Rect(navigation.x, navigation.y - 52f, navigation.width, navigation.height), "새 런 시작", false))
            {
                run.BeginRun();
                return;
            }
            if (graphics.DrawResultButton(navigation, run.runEnded ? "플레이 시작" : "플레이로 복귀", true))
            {
                if (run.runEnded) run.BeginRun();
                else run.ToggleMetaTree();
                return;
            }
            // Tooltips must be above the fixed information, wallet and navigation panels.
            DrawMetaTreeHoverDescription();
        }

        internal void HandleMetaTreeInput(Rect canvas)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            // Finish a middle-button drag even when the pointer crosses the fixed purchase panel.
            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 2)
            {
                if (isPanningMetaTree) currentEvent.Use();
                isPanningMetaTree = false;
                return;
            }
            if (currentEvent.type == EventType.MouseDrag && isPanningMetaTree)
            {
                metaTreePan += currentEvent.mousePosition - metaTreeLastPointer;
                metaTreeLastPointer = currentEvent.mousePosition;
                currentEvent.Use();
                return;
            }
            if (IsPrototypeGraphOverlay(currentEvent.mousePosition)) return;

            if (currentEvent.type == EventType.ScrollWheel && canvas.Contains(currentEvent.mousePosition))
            {
                float previousZoom = metaTreeZoom;
                metaTreeZoom = Mathf.Clamp(metaTreeZoom - currentEvent.delta.y * .06f, .04f, 1.45f);
                Vector2 canvasCenter = canvas.size * .5f;
                Vector2 pointer = currentEvent.mousePosition - canvas.position;
                metaTreePan += (pointer - canvasCenter - metaTreePan) * (1f - metaTreeZoom / previousZoom);
                currentEvent.Use();
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 2 && canvas.Contains(currentEvent.mousePosition))
            {
                isPanningMetaTree = true;
                metaTreeLastPointer = currentEvent.mousePosition;
                currentEvent.Use();
            }
        }

        internal void DrawMetaTreeCanvas(Rect canvas)
        {
            GUI.BeginGroup(canvas);
            Vector2 canvasSize = canvas.size;
            hoveredMetaTreeNode = -1;
            Event currentEvent = Event.current;
            Vector2 localPointer = currentEvent != null ? currentEvent.mousePosition - canvas.position : new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < metaTreeNodes.Length; i++)
            {
                if (metaTreeNodes[i].state != MetaTreeNodeState.Hidden && !IsPrototypeGraphOverlay(localPointer) && GetMetaTreeNodeHitRect(metaTreeNodes[i], canvasSize).Contains(localPointer))
                {
                    hoveredMetaTreeNode = i;
                }
            }

            for (int i = 0; i < metaTreeNodes.Length; i++)
            {
                MetaTreeNodePresentation node = metaTreeNodes[i];
                if (node.parentIndex < 0)
                {
                    continue;
                }

                MetaTreeNodePresentation parent = metaTreeNodes[node.parentIndex];
                DrawMetaTreeConnector(GetMetaTreeNodeCenter(parent, canvasSize), GetMetaTreeNodeCenter(node, canvasSize), tables.goalNodeLookup.ContainsKey(node.id)
                    ? new Color(.67f, .82f, .9f, .85f)
                    : node.state == MetaTreeNodeState.Hidden
                    ? new Color(.14f, .10f, .07f, .55f)
                    : node.state == MetaTreeNodeState.Locked
                    ? new Color(.28f, .18f, .11f, .85f)
                    : new Color(.97f, .66f, .16f, .92f));
            }

            for (int i = 0; i < metaTreeNodes.Length; i++)
            {
                DrawMetaTreeNode(i, metaTreeNodes[i], canvasSize);
            }

            GUI.EndGroup();
        }

        internal Vector2 GetMetaTreeNodeCenter(MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            return canvasSize * .5f + metaTreePan + node.position * metaTreeZoom;
        }

        internal Rect GetMetaTreeNodeRect(MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            Vector2 nodeSize = node.parentIndex < 0 ? new Vector2(82f, 82f) : new Vector2(66f, 66f);
            nodeSize *= Mathf.Clamp(metaTreeZoom * 1.45f, .06f, 1.2f);
            return new Rect(GetMetaTreeNodeCenter(node, canvasSize) - nodeSize * .5f, nodeSize);
        }

        internal static Rect GetMetaTreeNodeLabelRect(Rect nodeRect)
        {
            return new Rect(nodeRect.x - 32f, nodeRect.yMax + 5f, nodeRect.width + 64f, 32f);
        }

        internal Rect GetMetaTreeNodeHitRect(MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            // Titles and descriptions are display-only. Only the node card owns pointer events.
            return GetMetaTreeNodeRect(node, canvasSize);
        }

        internal static void DrawMetaTreeConnector(Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = to - from;
            float length = direction.magnitude;
            if (length < .01f)
            {
                return;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, from);
            UiGraphics.DrawSolidRect(new Rect(from.x, from.y - 1.5f, length, 3f), color);
            GUI.matrix = previousMatrix;
        }

        internal void DrawMetaTreeNode(int index, MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            Rect nodeRect = GetMetaTreeNodeRect(node, canvasSize);
            if (node.state == MetaTreeNodeState.Hidden)
            {
                // Preserve the map silhouette without exposing artwork, names, ranks or pointer events.
                UiGraphics.DrawSolidRect(nodeRect, new Color(.035f, .023f, .017f, 1f));
                UiGraphics.DrawOutline(nodeRect, new Color(.19f, .14f, .10f, .8f), 1f);
                return;
            }
            Rect labelRect = GetMetaTreeNodeLabelRect(nodeRect);
            Rect hitRect = GetMetaTreeNodeHitRect(node, canvasSize);
            bool isSelected = selectedMetaTreeNode == index;
            bool isHovered = hoveredMetaTreeNode == index;
            bool isGoal = tables.goalNodeLookup.TryGetValue(node.id, out PrototypeGoalNode goal);
            if (isGoal) labelRect = new Rect(nodeRect.x - 32, nodeRect.yMax + 2, nodeRect.width + 64, 14);
            Color fill = node.state == MetaTreeNodeState.Locked
                ? new Color(.105f, .064f, .035f, .99f)
                : new Color(.16f, .083f, .027f, .99f);
            Color border = node.state == MetaTreeNodeState.Locked
                ? new Color(.36f, .27f, .18f, .9f)
                : new Color(.96f, .66f, .19f);
            if (node.state == MetaTreeNodeState.Completed)
            {
                fill = new Color(.29f, .17f, .045f, .99f);
                border = new Color(1f, .82f, .36f);
            }
            if (isGoal)
            {
                fill = node.state == MetaTreeNodeState.Completed ? new Color(.12f, .17f, .19f) : new Color(.065f, .095f, .125f);
                border = goal.final ? new Color(1f, .71f, .32f) : new Color(.59f, .78f, .86f);
            }

            if (isSelected)
            {
                fill = Color.Lerp(fill, node.accent, .15f);
            }
            if (isHovered)
            {
                fill = Color.Lerp(fill, new Color(1f, .73f, .23f), .2f);
            }

            UiGraphics.DrawSolidRect(nodeRect, fill);
            UiGraphics.DrawOutline(nodeRect, border, isSelected || isHovered ? 2.5f : 1.25f);

            GUIStyle title = new GUIStyle(graphics.statStyle)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(12f * Mathf.Sqrt(metaTreeZoom)), 10, 16),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = node.state == MetaTreeNodeState.Locked ? new Color(.46f, .37f, .29f) : new Color(.9f, .74f, .49f) }
            };
            if (isGoal)
            {
                title.fontSize = Mathf.Clamp(Mathf.RoundToInt(12 * Mathf.Sqrt(metaTreeZoom)), 8, 12);
                title.wordWrap = false;
                title.normal.textColor = goal.final ? new Color(1f, .8f, .45f) : new Color(.72f, .87f, .92f);
            }
            float inset = nodeRect.width * .19f;
            UiGraphics.DrawResultSprite(new Rect(nodeRect.x + inset, nodeRect.y + inset * .4f, nodeRect.width - inset * 2,
                nodeRect.height * .42f), art.PrototypeIcon(node.id == "ROOT" ? "bolt" : isGoal ? goal.icon : tables.NodeGrowthRow(node.id).icon));
            if (node.id == "ROOT" && nodeRect.width >= 36)
                GUI.Label(new Rect(nodeRect.x, nodeRect.yMax - 18f, nodeRect.width, 12f), "1/1",
                    new GUIStyle(title) { fontSize = 9, wordWrap = false });
            if (isGoal)
            {
                if (nodeRect.width >= 36)
                    GUI.Label(new Rect(nodeRect.x, nodeRect.yMax - 18f, nodeRect.width, 12f), goal.kind == "stage" ? $"STAGE {goal.stage}" : "BOSS",
                        new GUIStyle(title) { fontSize = 9, wordWrap = false });
            }
            else if (node.id != "ROOT")
            {
                if (nodeRect.width >= 36)
                    GUI.Label(new Rect(nodeRect.x, nodeRect.yMax - 18f, nodeRect.width, 12f), progression.GrowthNodeRankLabel(node.id),
                        new GUIStyle(title) { fontSize = 9, wordWrap = false });
                PrototypeGrowthNode definition = tables.GrowthNode(node.id);
                int filled = PrototypeRules.NodeRank(definition, save.progress);
                float padding = Mathf.Min(6, nodeRect.width * .15f), gap = Mathf.Min(3, nodeRect.width * .06f);
                float segmentWidth = (nodeRect.width - padding * 2 - gap * (definition.ranks - 1)) / definition.ranks;
                for (int slot = 0; slot < definition.ranks; slot++)
                    UiGraphics.DrawSolidRect(new Rect(nodeRect.x + padding + slot * (segmentWidth + gap), nodeRect.yMax - 4, segmentWidth, 2),
                        slot < filled ? new Color(1f, .8f, .28f) : new Color(.29f, .23f, .16f));
            }
            if (metaTreeZoom >= .4f || node.id == "ROOT" || (isGoal && metaTreeZoom >= .2f)) GUI.Label(labelRect, node.title, title);

            if (!IsPrototypeGraphOverlay(Event.current.mousePosition) && GUI.Button(hitRect, GUIContent.none, GUIStyle.none))
            {
                selectedMetaTreeNode = index;
            }
        }

        internal void DrawMetaTreeHoverDescription()
        {
            DrawPrototypeGraphTooltip();
        }

        internal bool prototypeGraphViewInitialized;
        internal Vector2 prototypeGraphCanvasSize;

        internal Rect GrowthOverviewRect => new Rect(22, 22, 110, 30);
        internal Rect GrowthDetailsRect => GetGrowthDetailsRect(new Vector2(Screen.width, Screen.height));
        internal Rect GrowthNavigationRect => GetGrowthNavigationRect(new Vector2(Screen.width, Screen.height), run.runEnded);

        internal void FitPrototypeGraph(Vector2 canvasSize)
        {
            metaTreeZoom = PrototypeRules.GraphFitZoom(tables.graphLayoutNodes, canvasSize.x, canvasSize.y);
            metaTreePan = Vector2.zero;
            Rect[] overlays = GetGrowthOverlayRects(canvasSize, run.runEnded);
            bool obscured = false;
            foreach (MetaTreeNodePresentation node in metaTreeNodes)
            {
                if (node.state == MetaTreeNodeState.Hidden) continue;
                Rect card = GetMetaTreeNodeRect(node, canvasSize);
                foreach (Rect overlay in overlays) if (card.Overlaps(overlay)) obscured = true;
            }
            if (obscured)
            {
                // Keep the existing full-screen framing when unobstructed. In small Game views,
                // fit into whichever free rectangle (above/right of details) has more square space.
                Rect above = Rect.MinMaxRect(22, overlays[1].yMax + 16, canvasSize.x - 22, overlays[2].yMin - 16);
                Rect right = Rect.MinMaxRect(overlays[2].xMax + 16, overlays[1].yMax + 16, canvasSize.x - 22, overlays[3].yMin - 16);
                Rect available = Mathf.Min(above.width, above.height) >= Mathf.Min(right.width, right.height) ? above : right;
                float horizontal = 1, vertical = 1;
                foreach (PrototypeGrowthNode node in tables.graphLayoutNodes)
                {
                    horizontal = Mathf.Max(horizontal, Mathf.Abs(node.x));
                    vertical = Mathf.Max(vertical, Mathf.Abs(node.y));
                }
                // Reserve the same maximum card margin as the original fit, including its minimum scale.
                metaTreeZoom = Mathf.Clamp(Mathf.Min((available.width - 100) / (horizontal * 2),
                    (available.height - 100) / (vertical * 2)), .04f, 1.05f);
                metaTreePan = available.center - canvasSize * .5f;
            }
            prototypeGraphCanvasSize = canvasSize;
            prototypeGraphViewInitialized = true;
        }

        internal void DrawPrototypeGraphOverview()
        {
            if (GUI.Button(GrowthOverviewRect, "전체 보기")) FitPrototypeGraph(new Vector2(Screen.width, Screen.height));
        }

        internal bool IsPrototypeGraphOverlay(Vector2 pointer)
        {
            // Pointer tests run for each node; do not allocate a rectangle array per test.
            return GrowthOverviewRect.Contains(pointer) || GrowthWalletRect.Contains(pointer) ||
                GrowthDetailsRect.Contains(pointer) || GrowthNavigationRect.Contains(pointer);
        }

        private static Rect GetGrowthDetailsRect(Vector2 size) => new Rect(22, size.y - 222, Mathf.Min(355f, size.x - 44f), 198);
        private static Rect GetGrowthNavigationRect(Vector2 size, bool ended) =>
            new Rect(size.x - 202, size.y - (ended ? 66 : 118), 180, ended ? 44 : 96);
        private static Rect GetGrowthWalletRect(Vector2 size)
        {
            float width = Mathf.Min(300f, size.x - 44f);
            return new Rect(size.x - width - 22, 22, width, 64);
        }

        internal static Rect[] GetGrowthOverlayRects(Vector2 size, bool ended)
        {
            return new[] { new Rect(22, 22, 110, 30), GetGrowthWalletRect(size), GetGrowthDetailsRect(size), GetGrowthNavigationRect(size, ended) };
        }

        internal Rect GrowthWalletRect => GetGrowthWalletRect(new Vector2(Screen.width, Screen.height));

        internal void DrawGrowthWallet()
        {
            Rect rect = GrowthWalletRect;
            UiGraphics.DrawSolidRect(rect, new Color(.065f, .034f, .015f, .98f));
            UiGraphics.DrawOutline(rect, new Color(.57f, .38f, .16f), 1f);
            UiGraphics.DrawResultSprite(new Rect(rect.x + 14f, rect.y + 16f, 32f, 32f), art.coinSprite);
            GUIStyle label = new GUIStyle(graphics.statStyle) { fontSize = 11, normal = { textColor = new Color(.68f, .59f, .44f) } };
            GUIStyle amount = new GUIStyle(graphics.statStyle) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .82f, .42f) } };
            GUI.Label(new Rect(rect.x + 60f, rect.y + 7f, rect.width - 76f, 18f), "보유 재화", label);
            string value = save.metaCurrency.ToString("N0");
            while (amount.fontSize > 12 && amount.CalcSize(new GUIContent(value)).x > rect.width - 76f) amount.fontSize--;
            GUI.Label(new Rect(rect.x + 60f, rect.y + 24f, rect.width - 76f, 32f), value, amount);
        }

        internal void BuildPrototypeGraph()
        {
            if (metaTreeNodes.Length != tables.graphLayoutNodes.Length + 1)
                metaTreeNodes = new MetaTreeNodePresentation[tables.graphLayoutNodes.Length + 1];
            metaTreeNodes[0] = new MetaTreeNodePresentation("ROOT", "기본 공격 해금", "가까운 몬스터를 향해 기본 투사체를 자동으로 발사합니다.", Vector2.zero,
                -1, MetaTreeNodeState.Completed, new Color(.91f, .69f, .3f));
            int stage = PrototypeRules.Stage(tables.balance, save.progress);
            for (int i = 0; i < tables.growthTable.nodes.Length; i++)
            {
                PrototypeGrowthNode node = tables.growthTable.nodes[i];
                PrototypeGrowth row = tables.GrowthRow(node.growthId);
                bool completed = PrototypeRules.NodeRank(node, save.progress) >= node.ranks;
                bool locked = PrototypeRules.NodeUnlockBlock(tables.growthTable, node, save.progress) != null || stage < row.minStage ||
                    (!string.IsNullOrEmpty(row.requires) && save.progress.Rank(row.requires) < 1);
                bool revealed = PrototypeRules.IsNodeRevealed(tables.growthTable, node, save.progress, stage);
                MetaTreeNodeState state = !revealed ? MetaTreeNodeState.Hidden : completed ? MetaTreeNodeState.Completed : locked ? MetaTreeNodeState.Locked : MetaTreeNodeState.Reachable;
                int parent = 0;
                for (int j = 0; j < tables.growthTable.nodes.Length; j++)
                    if (tables.growthTable.nodes[j].id == node.parent) parent = j + 1;
                metaTreeNodes[i + 1] = new MetaTreeNodePresentation(node.id, revealed ? node.name : "", revealed ? row.description : "",
                    new Vector2(tables.graphLayoutNodes[i].x, tables.graphLayoutNodes[i].y), parent, state, new Color(.96f, .66f, .19f));
            }
            if (tables.growthTable.goalLine != null) for (int i = 0; i < tables.growthTable.goalLine.Length; i++)
            {
                PrototypeGoalNode goal = tables.growthTable.goalLine[i];
                int index = tables.growthTable.nodes.Length + i + 1;
                // The main objective roadmap is previewed in full; ordinary growth keeps one-hop concealment.
                metaTreeNodes[index] = new MetaTreeNodePresentation(goal.id, goal.name, GoalDescription(goal),
                    new Vector2(goal.x, goal.y), i == 0 ? 0 : index - 1,
                    i == 0 ? MetaTreeNodeState.Completed : MetaTreeNodeState.Locked, new Color(.59f, .78f, .86f));
            }
            if (selectedMetaTreeNode >= 0 && selectedMetaTreeNode < metaTreeNodes.Length &&
                metaTreeNodes[selectedMetaTreeNode].state == MetaTreeNodeState.Hidden) selectedMetaTreeNode = -1;
        }

        internal string GoalDescription(PrototypeGoalNode goal)
        {
            string condition = goal.kind == "boss"
                ? $"해당 보스 노드 해금 후, 스테이지 {goal.stage}의 런 경과 {goal.limitSeconds:0.##}초 리미트에서 등장 예정.\n남은 제한시간 종료나 몬스터 단계와는 별개입니다."
                : goal.stage == 1 ? "중앙에서 시작하는 첫 스테이지입니다." : "앞 스테이지 보스 다음에 이어지는 목표입니다. 이동 조건은 아직 확정하지 않았습니다.";
            return $"{goal.description}\n{condition}";
        }

        internal static string GoalStatus(PrototypeGoalNode goal) => goal.kind == "stage" && goal.stage == 1
            ? "시작 스테이지 · 임시 목표 배치" : "임시 목표 · 스테이지 이동/보스전 준비 중";

        internal string GrowthAmountText(PrototypeGrowth row, int rank, double partial = 0)
        {
            if (row.unit == "해금") return rank > 0 ? "해금됨 (런 레벨업에서 획득)" : "미해금";
            float amount = (float)PrototypeRules.Amount(tables.balance, row, rank, partial);
            if (row.id == "extension") return $"{(tables.balance.timeRewardChance + amount) * 100:0.##}% / +{tables.balance.timeRewardSeconds:0.##}초";
            if (LootGradeRules.CurrencyGrowthGrade(row.id) > 0) return $"+{Mathf.Clamp01(amount) * 100:0.##}%p / 낮은 재화 확률에서 전환";
            if (row.id == "currencyGradeChance" || row.id == "experienceGradeChance") return $"{Mathf.Clamp01(amount) * 100:0.##}% / 한 단계 상승";
            if (row.id.EndsWith("Chance"))
            {
                string skillId = row.id.Substring(0, row.id.Length - "Chance".Length);
                return $"{Mathf.Clamp01(tables.SkillRow(skillId).chance + amount) * 100:0.##}%";
            }
            if (row.id == "time") return $"{Mathf.Min(tables.balance.bossElapsedSeconds, (settings.useTableRunDuration ? tables.balance.initialSeconds : settings.runDuration) + amount):0.##}초";
            if (row.unit == "%" || row.unit == "%p") return $"+{amount * 100:0.##}{row.unit}";
            return $"+{amount:0.##}{row.unit}";
        }

        internal string GrowthDescription(PrototypeGrowth row, PrototypeGrowthNode node)
        {
            int rank = save.progress.Rank(row.id);
            string current = GrowthAmountText(row, rank, save.progress.PartialRank(row.id));
            string next = PrototypeRules.NodeRank(node, save.progress) >= node.ranks ? "이 노드 성장 완료" :
                GrowthAmountText(row, Mathf.Max(rank, node.startRank) + 1, rank >= node.startRank ? save.progress.PartialRank(row.id) : 0);
            return $"{row.description}\n현재 {current}\n다음 {next}";
        }

        internal string GrowthPurchaseStatus(PrototypeGrowth row, PrototypeGrowthNode node)
        {
            string blocked = PrototypeRules.NodePurchaseBlock(tables.balance, row, node, save.progress, PrototypeRules.Stage(tables.balance, save.progress), save.metaCurrency, tables.growthTable);
            if (blocked != null && blocked.StartsWith("선행 성장")) return tables.GrowthRow(row.requires).name + " 먼저 해금";
            return blocked ?? "구매 가능 · 다음 런부터 적용";
        }

        internal void DrawPrototypeGrowthDetails()
        {
            Rect panel = GrowthDetailsRect;
            float width = panel.width;
            UiGraphics.DrawSolidRect(panel, new Color(.065f, .034f, .015f, .97f));
            UiGraphics.DrawOutline(panel, new Color(.57f, .38f, .16f), 1);
            GUIStyle body = new GUIStyle(graphics.statStyle) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(.92f, .8f, .6f) } };
            GUIStyle heading = new GUIStyle(body) { fontSize = 16, fontStyle = FontStyle.Bold };
            string mode = save.sandboxThisSession ? "  [실험: 저장 안 됨]" : "";
            bool goalSelected = selectedMetaTreeNode > 0 && selectedMetaTreeNode < metaTreeNodes.Length && tables.goalNodeLookup.ContainsKey(metaTreeNodes[selectedMetaTreeNode].id);
            GUI.Label(new Rect(38, panel.y + 10, width - 30, 25), $"{(goalSelected ? "목표 정보" : "성장 정보")}{mode}", heading);
            if (selectedMetaTreeNode < 0 || selectedMetaTreeNode >= metaTreeNodes.Length ||
                metaTreeNodes[selectedMetaTreeNode].state == MetaTreeNodeState.Hidden)
            {
                GUI.Label(new Rect(38, panel.y + 43, width - 30, 88), "노드에 마우스를 올려 설명을 확인하고,\n클릭한 뒤 구매를 눌러 성장하세요.\n휠: 확대/축소 · 가운데 버튼: 이동\n구매한 성장은 다음 런부터 적용됩니다.", body);
                DrawPrototypeStageProgress(new Rect(38, panel.y + 139, width - 30, 42), body);
                return;
            }
            if (selectedMetaTreeNode == 0)
            {
                MetaTreeNodePresentation root = metaTreeNodes[0];
                GUI.Label(new Rect(38, panel.y + 37, width - 30, 23), root.title, heading);
                GUI.Label(new Rect(38, panel.y + 62, width - 30, 78), root.subtitle, body);
                GUI.Label(new Rect(38, panel.y + 142, width - 30, 20), "해금 완료 · 기본 자동 공격 사용 가능", body);
                return;
            }
            if (tables.goalNodeLookup.TryGetValue(metaTreeNodes[selectedMetaTreeNode].id, out PrototypeGoalNode goal))
            {
                GUI.Label(new Rect(38, panel.y + 37, width - 30, 23), goal.name, heading);
                GUI.Label(new Rect(38, panel.y + 62, width - 30, 88), GoalDescription(goal), body);
                GUI.Label(new Rect(38, panel.y + 151, width - 30, 34), GoalStatus(goal), body);
                return; // Preview only: do not invent a purchase, defeat or stage transition.
            }
            PrototypeGrowthNode node = tables.GrowthNode(metaTreeNodes[selectedMetaTreeNode].id);
            PrototypeGrowth row = tables.GrowthRow(node.growthId);
            int rank = save.progress.Rank(row.id);
            GUI.Label(new Rect(38, panel.y + 37, width - 30, 23), $"{node.name}  {progression.GrowthNodeRankLabel(node.id)}", heading);
            GUI.Label(new Rect(38, panel.y + 62, width - 30, 78), GrowthDescription(row, node), body);
            int cost = PrototypeRules.Cost(row, Mathf.Max(rank, node.startRank), tables.balance.latePriceScale);
            string status = GrowthPurchaseStatus(row, node);
            GUI.Label(new Rect(38, panel.y + 142, width - 30, 20), status, body);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = PrototypeRules.NodePurchaseBlock(tables.balance, row, node, save.progress, PrototypeRules.Stage(tables.balance, save.progress), save.metaCurrency, tables.growthTable) == null;
            if (GUI.Button(new Rect(38, panel.y + 164, width - 30, 25), $"구매 · {cost:N0} 재화"))
            {
                if (PrototypeRules.TryPurchaseNode(tables.balance, row, node, save.progress, PrototypeRules.Stage(tables.balance, save.progress), ref save.metaCurrency, tables.growthTable))
                {
                    save.MarkPrototypeProgressDirty();
                    save.FlushPrototypeProgress();
                    notifications.Notice(row.name + " 구매 완료 · 다음 런에 적용");
                }
            }
            GUI.enabled = previousEnabled;
        }

        internal void DrawPrototypeStageProgress(Rect rect, GUIStyle style)
        {
            int stage = PrototypeRules.Stage(tables.balance, save.progress);
            string progressText = stage < 4
                ? $"{tables.balance.monsters[stage].name} 누적 {save.progress.kills[stage]}/{tables.balance.monsters[stage].nextUnlockKills} 처치 → {tables.balance.monsters[stage + 1].name}"
                : "모든 일반 몬스터 해금 완료";
            GUI.Label(rect, $"몬스터 {stage + 1}단계 · {progressText}", style);
        }

        internal void DrawPrototypeGraphTooltip()
        {
            if (hoveredMetaTreeNode < 0 || hoveredMetaTreeNode >= metaTreeNodes.Length || Event.current == null) return;
            MetaTreeNodePresentation node = metaTreeNodes[hoveredMetaTreeNode];
            if (node.state == MetaTreeNodeState.Hidden) return;
            float width = Mathf.Min(330f, Screen.width - 36f), height = 158;
            Vector2 pointer = Event.current.mousePosition;
            float x = pointer.x + 20, y = pointer.y + 18;
            if (x + width > Screen.width - 18) x = pointer.x - width - 20;
            if (y + height > Screen.height - 18) y = pointer.y - height - 18;
            Rect rect = new Rect(Mathf.Clamp(x, 18, Screen.width - width - 18), Mathf.Clamp(y, 18, Mathf.Max(18, Screen.height - height - 18)), width, height);
            UiGraphics.DrawSolidRect(rect, new Color(.055f, .029f, .012f, .98f));
            UiGraphics.DrawOutline(rect, new Color(.8f, .54f, .19f), 1);
            GUIStyle body = new GUIStyle(graphics.statStyle) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(.92f, .78f, .56f) } };
            GUI.Label(new Rect(rect.x + 16, rect.y + 10, width - 32, 24), node.title,
                new GUIStyle(body) { fontSize = 17, fontStyle = FontStyle.Bold });
            if (node.id == "ROOT")
            {
                GUI.Label(new Rect(rect.x + 16, rect.y + 42, width - 32, 70), node.subtitle, body);
                GUI.Label(new Rect(rect.x + 16, rect.y + 113, width - 32, 35), "해금 완료 · 기본 자동 공격 사용 가능", body);
                return;
            }
            if (tables.goalNodeLookup.TryGetValue(node.id, out PrototypeGoalNode goal))
            {
                GUI.Label(new Rect(rect.x + 16, rect.y + 40, width - 32, 76), GoalDescription(goal), body);
                GUI.Label(new Rect(rect.x + 16, rect.y + 118, width - 32, 36), GoalStatus(goal), body);
                return;
            }
            PrototypeGrowthNode definition = tables.GrowthNode(node.id);
            PrototypeGrowth row = tables.GrowthRow(definition.growthId);
            GUI.Label(new Rect(rect.x + 16, rect.y + 40, width - 32, 76), GrowthDescription(row, definition), body);
            int cost = PrototypeRules.Cost(row, Mathf.Max(save.progress.Rank(row.id), definition.startRank), tables.balance.latePriceScale);
            GUI.Label(new Rect(rect.x + 16, rect.y + 118, width - 32, 36), $"{progression.GrowthNodeRankLabel(node.id)} · 비용 {cost:N0}\n{GrowthPurchaseStatus(row, definition)}", body);
        }

        internal void ResetNavigation() { isPanningMetaTree = false; hoveredMetaTreeNode = -1; }
    }
}
