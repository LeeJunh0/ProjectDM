using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns growth-map navigation, node visibility, hover details and purchase UI.</summary>
    internal sealed class GrowthGraphView : IDisposable
    {
        private readonly GameSettings settings;
        private readonly SaveService save;
        private readonly GameTables sourceTables;
        private bool personalGraph;
        private GrowthGraphTheme graphTheme;
        private GrowthGraphTheme Theme => graphTheme != null ? graphTheme : graphTheme = Resources.Load<GrowthGraphTheme>("ProjectDM/UI/GrowthGraphTheme");
        internal string ThemeCharacterId => personalGraph ? run.characters?.SelectedId : null;
        internal string SymbolCharacterId => GrowthTreeEffects.Character(run.characters?.SelectedId);
        private Color SymbolAccent => Theme != null ? Theme.Accent(SymbolCharacterId) : GrowthTreeEffects.Accent(SymbolCharacterId);
        private Color ThemeTint(Color original) => Theme != null ? Theme.Tint(original, ThemeCharacterId) : original;
        private GameTables tables => run.characters == null ? sourceTables : personalGraph
            ? run.characters.Catalog.Graphs[run.characters.SelectedId] : run.characters.Catalog.CommonGraph;
        private readonly ProgressionService progression;
        private readonly WorldArtAssets art;
        private readonly RunController run;
        private readonly GameNotifications notifications;
        private readonly UiGraphics graphics;
        private readonly GrowthPotionRenderer potion = new();
        private readonly GrowthNodeReveal reveal = new();

        public void Dispose() => potion.Dispose();

        // Every authored purchase segment counts, including hidden descendants and legacy partial ranks.
        internal float GrowthCompletion
        {
            get
            {
                double purchased = 0, total = 0;
                bool complete = true;
                foreach (var node in tables.growthTable.nodes)
                {
                    total += node.ranks;
                    double ranks = save.progress.Rank(node.growthId) + save.progress.PartialRank(node.growthId) - node.startRank;
                    purchased += Math.Max(0, Math.Min(node.ranks, ranks));
                    if (ranks < node.ranks) complete = false;
                }
                return total > 0 ? complete ? 1 : Mathf.Min(.9999f, (float)(purchased / total)) : 0;
            }
        }

        internal GrowthGraphView(GameSettings settings, SaveService save, GameTables tables, ProgressionService progression, WorldArtAssets art, RunController run, GameNotifications notifications, UiGraphics graphics)
        {
            this.settings = settings;
            this.save = save;
            sourceTables = tables;
            this.progression = progression;
            this.art = art;
            this.run = run;
            this.notifications = notifications;
            this.graphics = graphics;
        }

        internal Vector2 metaTreePan;

        internal float metaTreeZoom = .75f;

        internal int selectedMetaTreeNode = -1;

        internal int hoveredMetaTreeNode = -1;

        internal bool isPanningMetaTree;

        internal Vector2 metaTreeLastPointer;

        internal MetaTreeNodePresentation[] metaTreeNodes = System.Array.Empty<MetaTreeNodePresentation>();

        internal void DrawMetaTree()
        {
            // The tree owns the entire screen instead of appearing as an in-game popup.
            UiGraphics.DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(.065f, .05f, .09f));
            BuildPrototypeGraph();
            Rect treeCanvas = new Rect(0f, 0f, Screen.width, Screen.height);
            if (!prototypeGraphViewInitialized || prototypeGraphCanvasSize != treeCanvas.size) FitPrototypeGraph(treeCanvas.size);
            HandleMetaTreeInput(treeCanvas);
            DrawMetaTreeCanvas(treeCanvas);
            DrawPrototypeGrowthDetails();
            DrawGrowthWallet();
            DrawPrototypeGraphOverview();
            DrawStageSelection();
            DrawCharacterSelection();
            // This is a navigation button, not a popup/header; the graph still owns the whole screen.
            Rect navigationArea = GrowthNavigationRect;
            Rect navigation = new Rect(navigationArea.x, navigationArea.yMax - 44, navigationArea.width, 44);
            if (!run.runEnded && graphics.DrawResultButton(new Rect(navigation.x, navigation.y - 52f, navigation.width, navigation.height), "새 런 시작", false))
            {
                run.BeginRun();
                return;
            }
            bool newStage = (run.stages != null && run.stages.SelectionChanged) || (run.characters != null && run.characters.SelectionChanged);
            if (graphics.DrawResultButton(navigation, run.runEnded || newStage ? "선택 구성으로 시작" : "플레이로 복귀", true))
            {
                if (run.runEnded || newStage) run.BeginRun();
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
            Vector2 symbolCenter = canvasSize * .5f + metaTreePan + Vector2.up * GrowthTreeEffects.SymbolFloatY(metaTreeZoom, Time.unscaledTime);
            hoveredMetaTreeNode = -1;
            Event currentEvent = Event.current;
            Vector2 localPointer = currentEvent != null ? currentEvent.mousePosition - canvas.position : new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < metaTreeNodes.Length; i++)
            {
                if (i > 0 && metaTreeNodes[i].state != MetaTreeNodeState.Hidden && !reveal.IsAnimating(metaTreeNodes[i].id, Time.unscaledTime) && !IsPrototypeGraphOverlay(localPointer) && GetMetaTreeNodeHitRect(metaTreeNodes[i], canvasSize).Contains(localPointer))
                {
                    hoveredMetaTreeNode = i;
                }
            }

            for (int i = 0; i < metaTreeNodes.Length; i++)
            {
                MetaTreeNodePresentation node = metaTreeNodes[i];
                if (node.parentIndex < 0 || node.state == MetaTreeNodeState.Hidden)
                {
                    continue;
                }

                MetaTreeNodePresentation parent = metaTreeNodes[node.parentIndex];
                bool orthogonal = tables.growthNodeLookup.TryGetValue(node.id, out var growth) && growth.displayOrthogonal;
                var points = GrowthConnectionPath.Points(GetMetaTreeNodeCenter(parent, canvasSize), GetMetaTreeNodeCenter(node, canvasSize), orthogonal, metaTreeZoom);
                Color connectorColor = tables.goalNodeLookup.ContainsKey(node.id)
                    ? new Color(.67f, .82f, .9f, .85f)
                    : ThemeTint(node.state == MetaTreeNodeState.Locked
                    ? new Color(.28f, .18f, .11f, .85f)
                    : new Color(.97f, .66f, .16f, .92f));
                if (IsMetaTreeConnectionVisible(parent, node))
                {
                    DrawRevealPath(points, parent.id, node.id, connectorColor, symbolCenter);
                    if (!reveal.IsAnimating(parent.id, Time.unscaledTime) && !reveal.IsAnimating(node.id, Time.unscaledTime) && HasGrowthInvestment(node.id)) GrowthTreeEffects.DrawInwardFlow(points, SymbolAccent, metaTreeZoom, Time.unscaledTime, i * .137f, symbolCenter);
                }
                if (growth?.displayLinks == null) continue;
                foreach (string link in growth.displayLinks)
                {
                    int neighbor = Array.FindIndex(metaTreeNodes, candidate => candidate.id == link);
                    if (neighbor < 0 || !IsMetaTreeConnectionVisible(node, metaTreeNodes[neighbor])) continue;
                    Color gridColor = node.state == MetaTreeNodeState.Locked || metaTreeNodes[neighbor].state == MetaTreeNodeState.Locked
                        ? ThemeTint(new Color(.28f, .18f, .11f, .85f)) : connectorColor;
                    DrawRevealPath(new[] { GetMetaTreeNodeCenter(metaTreeNodes[neighbor], canvasSize), GetMetaTreeNodeCenter(node, canvasSize) }, metaTreeNodes[neighbor].id, node.id, gridColor, symbolCenter);
                    if (!reveal.IsAnimating(node.id, Time.unscaledTime) && !reveal.IsAnimating(metaTreeNodes[neighbor].id, Time.unscaledTime) && HasGrowthInvestment(node.id) && HasGrowthInvestment(metaTreeNodes[neighbor].id))
                    {
                        int near = MetaTreeDepth(neighbor) < MetaTreeDepth(i) ? neighbor : i;
                        int far = near == i ? neighbor : i;
                        if (MetaTreeDepth(near) != MetaTreeDepth(far)) GrowthTreeEffects.DrawInwardFlow(
                            new[] { GetMetaTreeNodeCenter(metaTreeNodes[near], canvasSize), GetMetaTreeNodeCenter(metaTreeNodes[far], canvasSize) },
                            SymbolAccent, metaTreeZoom, Time.unscaledTime, i * .173f, symbolCenter);
                    }
                }
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
            nodeSize *= Mathf.Clamp(metaTreeZoom * 1.45f, .06f, 2f);
            return new Rect(GetMetaTreeNodeCenter(node, canvasSize) - nodeSize * .5f, nodeSize);
        }

        internal static Rect GetMetaTreeNodeLabelRect(Rect nodeRect)
        {
            float detailScale = NodeDetailScale(nodeRect);
            // Leave room for two larger lines without crossing the next row of the authored grid.
            float rowSpace = nodeRect.width * (160f / (66f * 1.45f) - 1f) - 8f;
            float height = Mathf.Max(32f, Mathf.Min(40f * detailScale, rowSpace));
            return new Rect(nodeRect.x - 32f, nodeRect.yMax + 5f, nodeRect.width + 64f, height);
        }

        // Preserve the overview appearance, then enlarge the details once the old card-size limit is passed.
        internal static float NodeDetailScale(Rect nodeRect) => Mathf.Max(1f, nodeRect.width / (66f * 1.2f));

        internal Rect GetMetaTreeNodeHitRect(MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            // Titles and descriptions are display-only. Only the node card owns pointer events.
            return GetMetaTreeNodeRect(node, canvasSize);
        }

        internal static bool IsMetaTreeConnectionVisible(MetaTreeNodePresentation from, MetaTreeNodePresentation to)
        {
            return from.state != MetaTreeNodeState.Hidden && to.state != MetaTreeNodeState.Hidden;
        }

        internal bool HasGrowthInvestment(string id)
        {
            return tables.growthNodeLookup.TryGetValue(id, out var node) && save.progress != null &&
                save.progress.Rank(node.growthId) + save.progress.PartialRank(node.growthId) > node.startRank;
        }

        internal int MetaTreeDepth(int index)
        {
            int depth = 0;
            while (index > 0 && index < metaTreeNodes.Length && depth < metaTreeNodes.Length)
            { index = metaTreeNodes[index].parentIndex; depth++; }
            return depth;
        }

        private void DrawGrowthSymbol(Vector2 canvasSize)
        {
            if (Event.current?.type != EventType.Repaint) return;
            Vector2 center = canvasSize * .5f + metaTreePan;
            float completion = GrowthCompletion;
            Texture bottle = potion.Render(SymbolCharacterId, SymbolAccent, Time.unscaledTime, completion);
            GrowthTreeEffects.DrawSymbol(center, metaTreeZoom, SymbolCharacterId, SymbolAccent, Time.unscaledTime, bottle, completion);
        }

        internal static void DrawMetaTreeConnector(Vector2 from, Vector2 to, Color color, Vector2? symbolCenter = null, float zoom = 1)
        {
            GrowthTreeEffects.DrawConnector(from, to, color, symbolCenter, zoom);
        }

        private void DrawRevealPath(Vector2[] points, string first, string last, Color color, Vector2 symbolCenter)
        {
            float now = Time.unscaledTime;
            bool firstNew = reveal.IsAnimating(first, now), lastNew = reveal.IsAnimating(last, now);
            if (firstNew && lastNew)
            {
                color.a *= Mathf.Min(reveal.Opacity(first, now), reveal.Opacity(last, now));
                if (color.a <= .001f) return;
            }
            float progress = Mathf.Min(reveal.Line(first, now), reveal.Line(last, now));
            float remaining = GrowthTreeEffects.PathLength(points) * progress;
            if (firstNew && !lastNew) Array.Reverse(points);
            for (int i = 1; i < points.Length && remaining > 0; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]);
                if (length <= .001f) continue;
                float fraction = Mathf.Clamp01(remaining / length);
                DrawMetaTreeConnector(points[i - 1], Vector2.Lerp(points[i - 1], points[i], fraction), color, symbolCenter, metaTreeZoom);
                remaining -= length;
            }
        }

        internal void DrawMetaTreeNode(int index, MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            if (node.state == MetaTreeNodeState.Hidden)
            {
                // Undiscovered nodes leave no card, outline, text or pointer target behind.
                return;
            }
            if (node.id == "ROOT") { DrawGrowthSymbol(canvasSize); return; }
            Rect nodeRect = GetMetaTreeNodeRect(node, canvasSize);
            float revealOpacity = reveal.Opacity(node.id, Time.unscaledTime);
            if (revealOpacity <= .001f) return;
            Color previousGuiColor = GUI.color;
            Matrix4x4 previousGuiMatrix = GUI.matrix;
            GUI.color = previousGuiColor * new Color(1, 1, 1, revealOpacity);
            float revealScale = reveal.Scale(node.id, Time.unscaledTime);
            if (!Mathf.Approximately(revealScale, 1f))
                GUI.matrix = previousGuiMatrix * Matrix4x4.TRS(nodeRect.center, Quaternion.identity, Vector3.one * revealScale) * Matrix4x4.Translate(-nodeRect.center);
            try
            {
            Rect labelRect = GetMetaTreeNodeLabelRect(nodeRect);
            Rect hitRect = GetMetaTreeNodeHitRect(node, canvasSize);
            float detailScale = NodeDetailScale(nodeRect);
            bool isSelected = selectedMetaTreeNode == index;
            bool isHovered = hoveredMetaTreeNode == index;
            bool isGoal = tables.goalNodeLookup.TryGetValue(node.id, out PrototypeGoalNode goal);
            if (isGoal) labelRect = new Rect(nodeRect.x - 32, nodeRect.yMax + 2, nodeRect.width + 64, 18 * detailScale);
            Color border = ThemeTint(node.state == MetaTreeNodeState.Locked
                ? new Color(.36f, .27f, .18f, .9f)
                : new Color(.96f, .66f, .19f));
            if (node.state == MetaTreeNodeState.Completed)
            {
                border = ThemeTint(new Color(1f, .82f, .36f));
            }
            if (isGoal)
            {
                border = goal.final ? new Color(1f, .71f, .32f) : new Color(.59f, .78f, .86f);
            }

            Color frameTint = Color.Lerp(border, Color.white, isSelected || isHovered ? .3f : .05f);
            if (node.state == MetaTreeNodeState.Locked) frameTint *= .65f;
            frameTint.a = 1;
            PixelUiArt.Draw(nodeRect, PixelUiArt.Node, Mathf.Max(2, nodeRect.width * .14f), frameTint);
            if (isSelected || isHovered)
            { Color outline = border; outline.a *= revealOpacity; UiGraphics.DrawOutline(new Rect(nodeRect.x + 2, nodeRect.y + 2, nodeRect.width - 4, nodeRect.height - 4), outline, 1); }

            GUIStyle title = new GUIStyle(graphics.statStyle)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(15f * Mathf.Sqrt(metaTreeZoom) * detailScale), 12, 28),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = ThemeTint(node.state == MetaTreeNodeState.Locked ? new Color(.72f, .64f, .52f) : new Color(.96f, .85f, .65f)) }
            };
            if (isGoal)
            {
                title.fontSize = Mathf.Clamp(Mathf.RoundToInt(14 * Mathf.Sqrt(metaTreeZoom) * detailScale), 10, 24);
                title.wordWrap = false;
                title.normal.textColor = goal.final ? new Color(1f, .8f, .45f) : new Color(.72f, .87f, .92f);
            }
            float inset = nodeRect.width * .16f;
            float iconHeight = nodeRect.height * .55f;
            if (nodeRect.width >= 36f)
                iconHeight = Mathf.Min(iconHeight, Mathf.Max(10f, nodeRect.height * .9f - 20f * detailScale - 2f));
            float iconWidth = Mathf.Min(nodeRect.width - inset * 2, iconHeight);
            PrototypeGrowth growth = isGoal ? null : tables.NodeGrowthRow(node.id);
            Sprite icon = isGoal ? GoalIcon(goal) : art.GrowthIcon(growth, tables.balance,
                run.stages?.Definition(run.stages.SelectedId), run.characters?.SelectedId);
            UiGraphics.DrawResultSprite(new Rect(nodeRect.center.x - iconWidth * .5f, nodeRect.center.y - iconHeight * .5f,
                iconWidth, iconHeight), icon, GrowthIconVariants.ContentOffset(icon));
            if (!isGoal) DrawGrowthModifier(nodeRect, growth.id);
            if (isGoal)
            {
                if (nodeRect.width >= 36)
                    GUI.Label(new Rect(nodeRect.x, nodeRect.yMax - 16f * detailScale, nodeRect.width, 12f * detailScale), goal.kind == "stage" ? $"STAGE {goal.stage}" : "BOSS",
                        new GUIStyle(title) { fontSize = Mathf.RoundToInt(11f * detailScale), wordWrap = false });
            }
            else
            {
                if (nodeRect.width >= 36)
                    GUI.Label(new Rect(nodeRect.x, nodeRect.yMax - 16f * detailScale, nodeRect.width, 12f * detailScale), progression.GrowthNodeRankLabel(node.id),
                        new GUIStyle(title) { fontSize = Mathf.RoundToInt(11f * detailScale), wordWrap = false });
                PrototypeGrowthNode definition = tables.GrowthNode(node.id);
                int filled = PrototypeRules.NodeRank(definition, save.progress);
                float padding = Mathf.Min(6, nodeRect.width * .15f), gap = Mathf.Min(3, nodeRect.width * .06f);
                float segmentWidth = (nodeRect.width - padding * 2 - gap * (definition.ranks - 1)) / definition.ranks;
                for (int slot = 0; slot < definition.ranks; slot++)
                {
                    Color segment = ThemeTint(slot < filled ? new Color(1f, .8f, .28f) : new Color(.29f, .23f, .16f));
                    segment.a *= revealOpacity;
                    UiGraphics.DrawSolidRect(new Rect(nodeRect.x + padding + slot * (segmentWidth + gap), nodeRect.yMax - 4, segmentWidth, 2), segment);
                }
            }
            if (metaTreeZoom >= .4f || (isGoal && metaTreeZoom >= .2f)) GUI.Label(labelRect, node.title, title);

            if (!reveal.IsAnimating(node.id, Time.unscaledTime) && !IsPrototypeGraphOverlay(Event.current.mousePosition) && GUI.Button(hitRect, GUIContent.none, GUIStyle.none))
            {
                selectedMetaTreeNode = index;
            }
            }
            finally { GUI.color = previousGuiColor; GUI.matrix = previousGuiMatrix; }
        }

        internal void DrawMetaTreeHoverDescription()
        {
            DrawPrototypeGraphTooltip();
        }

        private static void DrawGrowthModifier(Rect rect, string id)
        {
            Sprite modifier = GrowthIconVariants.Modifier(id);
            if (modifier == null) return;
            float size = Mathf.Min(rect.width * .27f, 28);
            UiGraphics.DrawResultSprite(new Rect(rect.xMax - size - rect.width * .09f, rect.y + rect.height * .07f, size, size), modifier);
        }

        internal bool prototypeGraphViewInitialized;
        internal Vector2 prototypeGraphCanvasSize;

        internal Rect GrowthOverviewRect => new Rect(22, 22, 110, 30);
        internal Rect GrowthDetailsRect => GetGrowthDetailsRect(new Vector2(Screen.width, Screen.height));
        internal Rect GrowthNavigationRect => GetGrowthNavigationRect(new Vector2(Screen.width, Screen.height), run.runEnded);

        internal void FitPrototypeGraph(Vector2 canvasSize, bool overview = false)
        {
            metaTreeZoom = PrototypeRules.GraphFitZoom(tables.graphLayoutNodes, canvasSize.x, canvasSize.y);
            metaTreePan = Vector2.zero;
            Rect[] overlays = GetGrowthOverlayRects(canvasSize, run.runEnded);
            if (tables.growthTable.authoredLayout)
            {
                // Overview fits the whole map; opening the tree focuses on ROOT and nearby nodes.
                float startY = Mathf.Max(overlays[1].yMax, overlays[5].yMax) + 24;
                Rect free = Rect.MinMaxRect(Mathf.Min(overlays[2].xMax + 20, canvasSize.x * .5f), startY, canvasSize.x - 24, overlays[3].yMin - 16);
                Vector2 min = Vector2.zero, max = Vector2.zero;
                foreach (var node in tables.graphLayoutNodes)
                { min = Vector2.Min(min, new Vector2(node.x, node.y)); max = Vector2.Max(max, new Vector2(node.x, node.y)); }
                metaTreeZoom = Mathf.Clamp(Mathf.Min(Mathf.Max(1, free.width - 100) / Mathf.Max(1, max.x - min.x),
                    Mathf.Max(1, free.height - 110) / Mathf.Max(1, max.y - min.y)), .04f, 1.05f);
                metaTreePan = free.center - canvasSize * .5f - (min + max) * .5f * metaTreeZoom;
                if (!overview)
                {
                    float closeZoom = Mathf.Clamp(Mathf.Min(free.width / 1100f, free.height / 700f), .4f, .75f);
                    metaTreeZoom = Mathf.Max(metaTreeZoom, closeZoom);
                    metaTreePan = free.center - canvasSize * .5f;
                }
                prototypeGraphCanvasSize = canvasSize; prototypeGraphViewInitialized = true; return;
            }
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
                float top = Mathf.Max(overlays[1].yMax, overlays[5].yMax) + 16;
                Rect above = Rect.MinMaxRect(22, top, canvasSize.x - 22, overlays[2].yMin - 16);
                Rect right = Rect.MinMaxRect(overlays[2].xMax + 16, top, canvasSize.x - 22, overlays[3].yMin - 16);
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
            if (graphics.DrawButton(GrowthOverviewRect, "전체 보기")) FitPrototypeGraph(new Vector2(Screen.width, Screen.height), true);
        }

        private static Rect StageSelectionRect => new Rect(142, 22, 240, 30);
        internal static Rect CharacterSelectionRect(Vector2 size) => new Rect(22, 96, Mathf.Min(540, size.x - 44), 64);

        private void DrawCharacterSelection()
        {
            if (run.characters == null) return;
            Rect area = CharacterSelectionRect(new Vector2(Screen.width, Screen.height));
            CharacterSession characters = run.characters;
            for (int i = 0; i < characters.Catalog.Definitions.Length; i++)
            {
                CharacterDefinition character = characters.Catalog.Definitions[i];
                bool enabled = GUI.enabled; GUI.enabled = enabled && character.enabled;
                Color accent = Theme != null ? Theme.Accent(character.id) : Color.white;
                if (graphics.DrawTab(new Rect(area.x + i * area.width / 4, area.y, area.width / 4 - 4, 28),
                    (characters.SelectedId == character.id ? "● " : "") + (character.enabled ? character.name : "빙결 · 준비 중"),
                    characters.SelectedId == character.id, accent) && characters.Select(character.id))
                {
                    save.MarkPrototypeProgressDirty(); save.FlushPrototypeProgress();
                    reveal.Clear(); selectedMetaTreeNode = -1; hoveredMetaTreeNode = -1; prototypeGraphViewInitialized = false;
                }
                GUI.enabled = enabled;
            }
            bool common = graphics.DrawTab(new Rect(area.x, area.y + 34, 112, 28), "공통 성장", !personalGraph, new Color(1, .8f, .4f));
            bool personal = graphics.DrawTab(new Rect(area.x + 118, area.y + 34, 150, 28), "선택 캐릭터 전용 성장", personalGraph, SymbolAccent);
            if ((common && personalGraph) || (personal && !personalGraph))
            {
                reveal.Clear(); personalGraph = !personalGraph; selectedMetaTreeNode = -1; hoveredMetaTreeNode = -1; prototypeGraphViewInitialized = false;
            }
            GUI.Label(new Rect(area.x + 278, area.y + 36, area.width - 278, 24), "선택·구매는 다음 런부터 적용");
        }

        private void DrawStageSelection()
        {
            if (run.stages == null || !run.stages.Ready) return;
            Rect area = StageSelectionRect;
            for (int id = 1; id <= 2; id++)
            {
                Rect button = new Rect(area.x + (id - 1) * 122, area.y, 118, area.height);
                if (graphics.DrawTab(button, (run.stages.SelectedId == id ? "● " : "") + (id == 1 ? "1 · 초원" : "2 · 석재 던전"),
                    run.stages.SelectedId == id, new Color(1, .8f, .4f)))
                    run.stages.Select(id);
            }
        }

        private Sprite GoalIcon(PrototypeGoalNode goal)
        {
            StageDefinition stage = run.stages?.Definition(goal.stage);
            return stage == null ? art.PrototypeIcon(goal.icon) : goal.kind == "boss" ? stage.bossFrames[0] : stage.monsters[0].frames[0];
        }

        internal bool IsPrototypeGraphOverlay(Vector2 pointer)
        {
            // Pointer tests run for each node; do not allocate a rectangle array per test.
            return GrowthOverviewRect.Contains(pointer) || StageSelectionRect.Contains(pointer) ||
                (run.characters != null && CharacterSelectionRect(new Vector2(Screen.width, Screen.height)).Contains(pointer)) || GrowthWalletRect.Contains(pointer) ||
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
            return new[] { new Rect(22, 22, 110, 30), GetGrowthWalletRect(size), GetGrowthDetailsRect(size), GetGrowthNavigationRect(size, ended), StageSelectionRect, CharacterSelectionRect(size) };
        }

        internal Rect GrowthWalletRect => GetGrowthWalletRect(new Vector2(Screen.width, Screen.height));

        internal void DrawGrowthWallet()
        {
            Rect rect = GrowthWalletRect;
            PixelUiArt.Draw(rect, PixelUiArt.Currency, 12, Color.white);
            UiGraphics.DrawResultSprite(new Rect(rect.x + 14f, rect.y + 16f, 32f, 32f), art.coinSprite);
            GUIStyle label = new GUIStyle(graphics.statStyle) { fontSize = 11, normal = { textColor = ThemeTint(new Color(.68f, .59f, .44f)) } };
            GUIStyle amount = new GUIStyle(graphics.statStyle) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .82f, .42f) } };
            GUI.Label(new Rect(rect.x + 60f, rect.y + 7f, rect.width - 76f, 18f), "보유 재화", label);
            string value = CompactNumber.Format(save.metaCurrency);
            while (amount.fontSize > 12 && amount.CalcSize(new GUIContent(value)).x > rect.width - 76f) amount.fontSize--;
            GUI.Label(new Rect(rect.x + 60f, rect.y + 24f, rect.width - 76f, 32f), value, amount);
        }

        internal void BuildPrototypeGraph()
        {
            if (metaTreeNodes.Length != tables.graphLayoutNodes.Length + 1)
                metaTreeNodes = new MetaTreeNodePresentation[tables.graphLayoutNodes.Length + 1];
            metaTreeNodes[0] = new MetaTreeNodePresentation("ROOT", personalGraph && run.characters != null ? run.characters.Catalog.Find(run.characters.SelectedId).name : "기본 공격 해금",
                run.characters != null ? "선택한 캐릭터의 고유 마법을 자동 시전합니다. 캐릭터 변경과 영구 성장은 다음 런부터 적용됩니다." : "가까운 몬스터를 향해 기본 투사체를 자동으로 발사합니다.", Vector2.zero,
                -1, MetaTreeNodeState.Completed, ThemeTint(new Color(.91f, .69f, .3f)));
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
                    if (tables.growthTable.nodes[j].id == node.VisualParent) parent = j + 1;
                metaTreeNodes[i + 1] = new MetaTreeNodePresentation(node.id, revealed ? node.name : "", revealed ? row.description : "",
                    new Vector2(tables.graphLayoutNodes[i].x, tables.graphLayoutNodes[i].y), parent, state, ThemeTint(new Color(.96f, .66f, .19f)));
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
            if (selectedMetaTreeNode == 0) selectedMetaTreeNode = -1;
            if (selectedMetaTreeNode >= 0 && selectedMetaTreeNode < metaTreeNodes.Length &&
                metaTreeNodes[selectedMetaTreeNode].state == MetaTreeNodeState.Hidden) selectedMetaTreeNode = -1;
        }

        internal void AnimateNewlyRevealedNodes(float now)
        {
            int stage = PrototypeRules.Stage(tables.balance, save.progress);
            for (int i = 0; i < tables.growthTable.nodes.Length; i++)
                if (metaTreeNodes[i + 1].state == MetaTreeNodeState.Hidden && PrototypeRules.IsNodeRevealed(tables.growthTable, tables.growthTable.nodes[i], save.progress, stage))
                    reveal.Begin(tables.growthTable.nodes[i].id, now);
        }

        internal string GoalDescription(PrototypeGoalNode goal)
        {
            string condition = goal.kind == "boss"
                ? $"해당 보스 노드 해금 후, 스테이지 {goal.stage}의 런 경과 {goal.limitSeconds:0.##}초 리미트에서 등장 예정.\n마나 고갈이나 몬스터 단계와는 별개입니다."
                : goal.stage == 1 ? "중앙에서 시작하는 첫 스테이지입니다." : "앞 스테이지 보스 다음에 이어지는 목표입니다. 이동 조건은 아직 확정하지 않았습니다.";
            return $"{goal.description}\n{condition}";
        }

        internal static string GoalStatus(PrototypeGoalNode goal) => goal.kind == "stage"
            ? "리소스 연결 완료 · 위의 스테이지 선택으로 새 런 시작" : "보스 외형 연결 완료 · 보스 전투/해금 조건 준비 중";

        internal string GrowthAmountText(PrototypeGrowth row, int rank, double partial = 0)
        {
            if (row.unit == "해금") return rank > 0 ? (row.id.StartsWith("magic.") ? "해금됨 · 다음 런 자동 사용" : "해금됨 (런 레벨업에서 획득)") : "미해금";
            float amount = (float)PrototypeRules.Amount(tables.balance, row, rank, partial);
            if (row.id.StartsWith("magic.")) return row.unit == "해금" ? (amount >= 1 ? "해금됨 · 다음 런 자동 사용" : "미해금") :
                row.unit == "%" || row.unit == "%p" ? $"누적 +{amount * 100:0.##}{row.unit}" : $"누적 +{amount:0.##}{row.unit}";
            switch (row.id)
            {
                // Keep the former health ID so existing purchases now grant the same amount of mana.
                case "health": return $"최대 마나 +{amount:0.##} MP";
                case "movement": return $"{PlayerStats.Calculate(tables.balance, row.id, amount):0.##} / +{amount * 100:0.##}%";
                case "criticalChance": return $"{PlayerStats.Calculate(tables.balance, row.id, amount) * 100:0.##}%";
                case "criticalDamage": return $"기본 피해의 {PlayerStats.Calculate(tables.balance, row.id, amount) * 100:0.##}%";
                case "invulnerability": return $"{PlayerStats.Calculate(tables.balance, row.id, amount):0.##}초";
                case "magnet": return $"{settings.pickupCollectionRadius + PlayerStats.Calculate(tables.balance, row.id, amount):0.##}유닛";
                case "magnetDropChance": return $"몬스터 처치 시 {Mathf.Clamp01(amount)*100:0.##}%";
                case "luck":
                    float luck = PlayerStats.Calculate(tables.balance, row.id, amount);
                    var chances = UpgradeGradeRules.ForLuck(luck);
                    return $"행운 {luck:0.##} · 금 {chances.gold * 100:0.#}% / 은 {chances.silver * 100:0.#}% / 동 {chances.bronze * 100:0.#}%";
            }
            if (row.id == "extension") return $"{Mathf.Clamp01(tables.balance.ResolvedManaAbsorbChance + amount) * 100:0.##}% / +{tables.balance.ResolvedManaAbsorbAmount:0.##} MP";
            if (LootGradeRules.CurrencyGrowthGrade(row.id) > 0) return $"+{Mathf.Clamp01(amount) * 100:0.##}%p / 낮은 재화 확률에서 전환";
            if (row.id == "currencyGradeChance" || row.id == "experienceGradeChance") return $"{Mathf.Clamp01(amount) * 100:0.##}% / 한 단계 상승";
            if (row.id.EndsWith("Chance"))
            {
                string skillId = row.id.Substring(0, row.id.Length - "Chance".Length);
                return $"{Mathf.Clamp01(tables.SkillRow(skillId).chance + amount) * 100:0.##}%";
            }
            if (row.id == "time")
            {
                float capacityBonus = tables.growthLookup.TryGetValue("health", out var capacity)
                    ? (float)PrototypeRules.Amount(tables.balance, capacity, save.progress.Rank("health"), save.progress.PartialRank("health")) : 0;
                return $"{Mathf.Max(1, (settings.useTableStartingMana ? tables.balance.ResolvedInitialMana : settings.startingMana) + amount + capacityBonus):0.##} MP";
            }
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
            PixelUiArt.Draw(panel, PixelUiArt.Panel, 12, Color.white);
            GUIStyle body = new GUIStyle(graphics.statStyle) { fontSize = 12, wordWrap = true, normal = { textColor = ThemeTint(new Color(.92f, .8f, .6f)) } };
            GUIStyle heading = new GUIStyle(body) { fontSize = 16, fontStyle = FontStyle.Bold };
            string mode = save.sandboxThisSession ? "  [실험: 저장 안 됨]" : "";
            bool goalSelected = selectedMetaTreeNode > 0 && selectedMetaTreeNode < metaTreeNodes.Length && tables.goalNodeLookup.ContainsKey(metaTreeNodes[selectedMetaTreeNode].id);
            GUI.Label(new Rect(38, panel.y + 10, width - 30, 25), $"{(goalSelected ? "목표 정보" : "성장 정보")}{mode}", heading);
            if (selectedMetaTreeNode <= 0 || selectedMetaTreeNode >= metaTreeNodes.Length ||
                metaTreeNodes[selectedMetaTreeNode].state == MetaTreeNodeState.Hidden)
            {
                GUI.Label(new Rect(38, panel.y + 43, width - 30, 88), "노드에 마우스를 올려 설명을 확인하고,\n클릭한 뒤 구매를 눌러 성장하세요.\n휠: 확대/축소 · 가운데 버튼: 이동\n구매한 성장은 다음 런부터 적용됩니다.", body);
                DrawPrototypeStageProgress(new Rect(38, panel.y + 139, width - 30, 42), body);
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
            if (graphics.DrawButton(new Rect(38, panel.y + 164, width - 30, 25), $"구매 · {CompactNumber.Format(cost)} 재화", true, 12))
            {
                if (PrototypeRules.TryPurchaseNode(tables.balance, row, node, save.progress, PrototypeRules.Stage(tables.balance, save.progress), ref save.metaCurrency, tables.growthTable))
                {
                    AnimateNewlyRevealedNodes(Time.unscaledTime);
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
                ? $"{art.MonsterName(stage, tables.balance)} 누적 {save.progress.kills[stage]}/{tables.balance.monsters[stage].nextUnlockKills} 처치 → {art.MonsterName(stage + 1, tables.balance)}"
                : "모든 일반 몬스터 해금 완료";
            GUI.Label(rect, $"몬스터 {stage + 1}단계 · {progressText}", style);
        }

        internal void DrawPrototypeGraphTooltip()
        {
            if (hoveredMetaTreeNode < 0 || hoveredMetaTreeNode >= metaTreeNodes.Length || Event.current == null) return;
            MetaTreeNodePresentation node = metaTreeNodes[hoveredMetaTreeNode];
            if (node.state == MetaTreeNodeState.Hidden) return;
            if (node.id == "ROOT") return;
            float width = Mathf.Min(330f, Screen.width - 36f), height = 158;
            Vector2 pointer = Event.current.mousePosition;
            float x = pointer.x + 20, y = pointer.y + 18;
            if (x + width > Screen.width - 18) x = pointer.x - width - 20;
            if (y + height > Screen.height - 18) y = pointer.y - height - 18;
            Rect rect = new Rect(Mathf.Clamp(x, 18, Screen.width - width - 18), Mathf.Clamp(y, 18, Mathf.Max(18, Screen.height - height - 18)), width, height);
            PixelUiArt.Draw(rect, PixelUiArt.Panel, 12, Color.white);
            GUIStyle body = new GUIStyle(graphics.statStyle) { fontSize = 12, wordWrap = true, normal = { textColor = ThemeTint(new Color(.92f, .78f, .56f)) } };
            GUI.Label(new Rect(rect.x + 16, rect.y + 10, width - 32, 24), node.title,
                new GUIStyle(body) { fontSize = 17, fontStyle = FontStyle.Bold });
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
            GUI.Label(new Rect(rect.x + 16, rect.y + 118, width - 32, 36), $"{progression.GrowthNodeRankLabel(node.id)} · 비용 {CompactNumber.Format(cost)}\n{GrowthPurchaseStatus(row, definition)}", body);
        }

        internal void ResetNavigation() { reveal.Clear(); isPanningMetaTree = false; hoveredMetaTreeNode = -1; }
    }
}
