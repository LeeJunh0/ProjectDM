using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Report-only growth QA. Uses actual view/rules and memory persistence; never saves scenes or PlayerPrefs.</summary>
    public static class GrowthGraphQa
    {
        [Serializable]
        private sealed class Report
        {
            public string executedAt, status;
            public int assertions, purchases;
            public List<string> passed = new();
            public List<string> findings = new();
            public List<string> evidence = new();
        }

        private static Report report;

        [MenuItem("Project DM/검증/성장 그래프 QA (보고 전용)")]
        public static void RunMenu()
        {
            Execute();
            Debug.Log($"Growth QA: {report.assertions} assertions PASS; {report.findings.Count} layout findings. See Logs/GrowthGraphQa-result.json.");
        }

        public static void RunBatch()
        {
            try { Execute(); EditorApplication.Exit(0); }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/GrowthGraphQa-error.txt", exception.ToString());
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.assertions++;
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;

        private static void Execute()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료하고 실행하세요.");
            report = new Report { executedAt = DateTime.UtcNow.ToString("O"), status = "RUNNING" };
            float originalScale = Time.timeScale;
            Event originalEvent = Event.current;
            try
            {
                CheckFreshGraph();
                CheckObjectiveLine();
                CheckPurchaseBoundaries();
                CheckFullPathsAndPersistence();
                CheckSnapshotAndIndependentChances();
                CheckInput();
                CheckLayout();
                report.status = report.findings.Count == 0 ? "PASS" : "FUNCTIONAL_PASS_WITH_LAYOUT_FINDINGS";
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/GrowthGraphQa-result.json", JsonUtility.ToJson(report, true));
                File.WriteAllText("Logs/GrowthGraphQa-result.txt", $"{report.status}: {report.assertions} assertions, {report.purchases} successful purchases, {report.findings.Count} layout findings.\n");
            }
            finally { Time.timeScale = originalScale; Event.current = originalEvent; }
        }

        private static void CheckFreshGraph()
        {
            Fixture f = new Fixture();
            f.view.BuildPrototypeGraph();
            Check(f.view.metaTreeNodes.Length == f.tables.growthTable.nodes.Length + 1 + f.tables.growthTable.goalLine.Length, "Graph must contain root plus all growth and goal nodes");
            Check(f.view.metaTreeNodes[0].title == "기본 공격 해금" && f.view.metaTreeNodes[0].state == MetaTreeNodeState.Completed, "Basic attack root is completed for free");
            Check(f.save.metaCurrency == 0, "Fresh graph must not invent currency");
            Check(f.view.GrowthPurchaseStatus(f.tables.GrowthRow("damage"), f.tables.GrowthNode("damage_1")) == "재화 부족", "Initial purchase status explains currency shortage");
            foreach (MetaTreeNodePresentation item in f.view.metaTreeNodes)
            {
                if (item.id == "ROOT" || f.tables.goalNodeLookup.ContainsKey(item.id)) continue;
                PrototypeGrowthNode node = f.tables.GrowthNode(item.id);
                bool revealed = PrototypeRules.IsNodeRevealed(f.tables.growthTable, node, f.save.progress, 0);
                Check((item.state != MetaTreeNodeState.Hidden) == revealed, "View and purchase reveal rules agree: " + item.id);
                if (!revealed) Check(item.title == "" && item.subtitle == "", "Hidden metadata must be redacted: " + item.id);
                if (node.parent == "ROOT") Check(revealed, "Every first-ring node is visible: " + item.id);
            }
            int hidden = Array.FindIndex(f.view.metaTreeNodes, n => n.state == MetaTreeNodeState.Hidden);
            f.view.selectedMetaTreeNode = hidden;
            f.view.BuildPrototypeGraph();
            Check(f.view.selectedMetaTreeNode == -1, "Hidden selection must not leak into the details panel");
            report.passed.Add("Fresh root, all presentations, one-hop visibility, hidden metadata and stale-selection clearing.");
        }

        private static void CheckPurchaseBoundaries()
        {
            Fixture f = new Fixture();
            PrototypeGrowthNode node = f.tables.GrowthNode("damage_1");
            PrototypeGrowth row = f.tables.GrowthRow(node.growthId);
            int cost = PrototypeRules.Cost(row, 0, f.tables.balance.latePriceScale);
            foreach (int gold in new[] { 0, cost - 1, cost, cost + 1 })
            {
                f.save.progress = new PrototypeSave(); f.save.metaCurrency = gold;
                bool success = Purchase(f, node);
                Check(success == (gold >= cost), "Exact purchase boundary: " + gold);
                Check(f.save.metaCurrency == gold - (success ? cost : 0), "Exact currency debit: " + gold);
                Check(f.save.progress.Rank(row.id) == (success ? 1 : 0), "No rank mutation on rejected purchase");
            }
            f.save.progress = new PrototypeSave(); f.save.metaCurrency = int.MaxValue;
            for (int i = 0; i < node.ranks; i++) Check(Purchase(f, node), "Each allowed purchase succeeds");
            int before = f.save.metaCurrency;
            Check(!Purchase(f, node) && f.save.metaCurrency == before, "Fourth/extra click must not spend currency");
            PrototypeGrowthNode future = f.tables.GrowthNode("damage_4");
            Check(!Purchase(f, future) && f.save.metaCurrency == before, "Skipped own tiers cannot be purchased");
            PrototypeGrowthNode chance = f.tables.GrowthNode("chainChance_1");
            Check(!Purchase(f, chance) && f.save.metaCurrency == before, "Skill probability requires its skill unlock");
            PrototypeGrowthNode locked = Array.Find(f.tables.growthTable.nodes, n => f.tables.GrowthRow(n.growthId).minStage > 0 && n.startRank == 0);
            Check(!Purchase(f, locked) && f.save.metaCurrency == before, "Monster-stage restriction rejects early purchase");
            report.passed.Add("Insufficient/exact/excess currency, rank cap, skipped tiers, skill dependencies and monster-stage gates.");
        }

        private static void CheckObjectiveLine()
        {
            Fixture f = new Fixture(); f.view.BuildPrototypeGraph();
            string before = JsonUtility.ToJson(f.save.progress);
            int wallet = f.save.metaCurrency;
            Check(f.tables.growthTable.goalLine.Length == 4, "Two stage/boss pairs in the temporary objective line");
            int parent = 0;
            foreach (PrototypeGoalNode goal in f.tables.growthTable.goalLine)
            {
                int index = Array.FindIndex(f.view.metaTreeNodes, n => n.id == goal.id);
                MetaTreeNodePresentation node = f.view.metaTreeNodes[index];
                Check(node.position.x == 0 && node.position.y < f.view.metaTreeNodes[parent].position.y && node.parentIndex == parent, "One linear twelve-o'clock path from the basic attack root");
                Check(node.state != MetaTreeNodeState.Hidden && node.title == goal.name, "Objective previews remain identifiable along the roadmap");
                Check(!f.tables.growthNodeLookup.ContainsKey(goal.id), "Goal previews are not cash-purchase growth nodes");
                if (goal.kind == "boss") Check(f.view.GoalDescription(goal).Contains("런 경과") && f.view.GoalDescription(goal).Contains("몬스터 단계와는 별개"), "Boss description uses actual elapsed limit, not countdown or loot tier");
                f.view.selectedMetaTreeNode = index; f.view.BuildPrototypeGraph();
                Check(f.view.selectedMetaTreeNode == index, "Objective cards support the common selection/details path");
                parent = index;
            }
            foreach (PrototypeGrowth row in f.tables.growthTable.rows) f.save.progress.SetRank(row.id, row.maxRank);
            for (int i = 0; i < 4; i++) f.save.progress.kills[i] = f.tables.balance.monsters[i].nextUnlockKills;
            f.view.BuildPrototypeGraph();
            foreach (PrototypeGoalNode goal in f.tables.growthTable.goalLine)
                if (goal.kind == "boss") Check(f.view.metaTreeNodes[Array.FindIndex(f.view.metaTreeNodes, n => n.id == goal.id)].state == MetaTreeNodeState.Locked,
                    "Monster kills and ordinary growth do not pretend to defeat a stage boss");
            f.save.progress = JsonUtility.FromJson<PrototypeSave>(before); f.view.BuildPrototypeGraph();
            Check(JsonUtility.ToJson(f.save.progress) == before && f.save.metaCurrency == wallet, "Building/selecting previews never mutates ranks, kills, wallet or save");
            foreach (float zoom in new[] { .04f, .12f, .2f, .4f, .75f, 1.45f })
            {
                f.view.metaTreeZoom = zoom;
                Vector2 canvas = new Vector2(1920, 1080);
                for (int i = 0; i < f.view.metaTreeNodes.Length; i++) for (int j = i + 1; j < f.view.metaTreeNodes.Length; j++)
                    Check(!f.view.GetMetaTreeNodeRect(f.view.metaTreeNodes[i], canvas).Overlaps(f.view.GetMetaTreeNodeRect(f.view.metaTreeNodes[j], canvas)),
                        "Objective corridor or ordinary growth cards overlap at zoom " + zoom);
            }
            report.passed.Add("Twelve-o'clock stage/boss roadmap, common card-only selection, elapsed-limit descriptions, no purchases/fake clears/save mutations, card separation at six zoom levels.");
        }

        private static void CheckFullPathsAndPersistence()
        {
            // Different legal purchase orders across actual monster stages must all finish.
            for (int seed = 0; seed < 12; seed++)
            {
                Fixture f = new Fixture();
                f.save.metaCurrency = int.MaxValue;
                System.Random random = new System.Random(seed + 7413);
                int bought = 0;
                for (int stage = 0; stage <= 4; stage++)
                {
                    for (int monster = 0; monster < stage; monster++)
                        f.save.progress.kills[monster] = f.tables.balance.monsters[monster].nextUnlockKills;
                    while (true)
                    {
                        List<PrototypeGrowthNode> available = new();
                        foreach (PrototypeGrowthNode node in f.tables.growthTable.nodes)
                            if (PrototypeRules.NodePurchaseBlock(f.tables.balance, f.tables.GrowthRow(node.growthId), node,
                                f.save.progress, stage, f.save.metaCurrency, f.tables.growthTable) == null) available.Add(node);
                        if (available.Count == 0) break;
                        PrototypeGrowthNode chosen = available[random.Next(available.Count)];
                        int oldRank = f.save.progress.Rank(chosen.growthId), oldGold = f.save.metaCurrency;
                        int price = PrototypeRules.Cost(f.tables.GrowthRow(chosen.growthId), oldRank, f.tables.balance.latePriceScale);
                        Check(Purchase(f, chosen), "Legal order purchase rejected: " + chosen.id);
                        bought++;
                        Check(f.save.progress.Rank(chosen.growthId) == oldRank + 1 && f.save.metaCurrency == oldGold - price, "One click, one rank and one exact debit");
                        f.view.BuildPrototypeGraph();
                        foreach (MetaTreeNodePresentation item in f.view.metaTreeNodes)
                        {
                            if (item.id == "ROOT" || f.tables.goalNodeLookup.ContainsKey(item.id)) continue;
                            PrototypeGrowthNode definition = f.tables.GrowthNode(item.id);
                            bool shouldReveal = PrototypeRules.IsNodeRevealed(f.tables.growthTable, definition, f.save.progress, stage);
                            Check((item.state != MetaTreeNodeState.Hidden) == shouldReveal, "Visibility after mixed purchase order: " + item.id);
                        }
                        if (bought % 11 == 0)
                        {
                            int expectedGold = f.save.metaCurrency;
                            string beforeReload = JsonUtility.ToJson(f.save.progress);
                            f.save.Load(); // Memory-only backend, including migration/backups.
                            Check(JsonUtility.ToJson(f.save.progress) == beforeReload && f.save.metaCurrency == expectedGold, "Save/reload must preserve ranks, kills and currency");
                        }
                    }
                }
                Check(bought == Array.ConvertAll(f.tables.growthTable.rows, row => row.maxRank).Sum(), "Purchase path deadlock, skipped rank or over-purchase: seed " + seed);
                foreach (PrototypeGrowth row in f.tables.growthTable.rows)
                    Check(f.save.progress.Rank(row.id) == row.maxRank, "All growth rows finish: " + row.id);
                f.view.BuildPrototypeGraph();
                foreach (MetaTreeNodePresentation item in f.view.metaTreeNodes)
                    Check(f.tables.goalNodeLookup.ContainsKey(item.id) || item.state == MetaTreeNodeState.Completed, "Every growth node completed after legal full path; preview goals remain pending");
            }
            report.passed.Add("12 shuffled legal routes through stages 1–5, all table purchases per route, reload every 11 purchases, no deadlock or lost purchase.");
        }

        private static void CheckSnapshotAndIndependentChances()
        {
            Fixture f = new Fixture();
            f.save.metaCurrency = 100000;
            Check(Purchase(f, f.tables.GrowthNode("damage_1")), "Snapshot test purchase");
            Check(Near(f.progression.PermanentAmount("damage"), 0), "Purchase must not modify current-run damage");
            f.progression.ResetSnapshot();
            Check(Near(f.progression.PermanentAmount("damage"), .66f), "Next run applies first damage purchase and bonus");
            f.save.progress.SetRank("chain", 1); f.save.progress.SetRank("splash", 1); f.save.progress.SetRank("meteor", 1);
            f.progression.ResetSnapshot();
            float oldSplash = f.progression.PermanentAmount("splashChance"), oldMeteor = f.progression.PermanentAmount("meteorChance");
            Check(Purchase(f, f.tables.GrowthNode("chainChance_1")), "Individual chain probability purchase");
            Check(Near(f.progression.PermanentAmount("chainChance"), 0), "Probability change waits until next run");
            f.progression.ResetSnapshot();
            Check(f.progression.PermanentAmount("chainChance") > 0 && Near(f.progression.PermanentAmount("splashChance"), oldSplash) &&
                Near(f.progression.PermanentAmount("meteorChance"), oldMeteor), "Chain growth cannot change other probabilities");
            report.passed.Add("Current/next-run growth isolation and independent chain/splash/meteor permanent chances.");
        }

        private static void CheckInput()
        {
            Fixture f = new Fixture();
            f.view.BuildPrototypeGraph();
            // Edit-mode batch has no active Game view, so Screen can legitimately be 0x0.
            Vector2 canvasSize = new Vector2(Mathf.Max(1920, Screen.width), Mathf.Max(1080, Screen.height));
            report.evidence.Add($"Unity batch Screen: {Screen.width}x{Screen.height}");
            foreach (float zoom in new[] { .12f, .2f, .4f, .75f, 1f, 1.45f })
            {
                f.view.metaTreeZoom = zoom;
                foreach (MetaTreeNodePresentation node in f.view.metaTreeNodes)
                {
                    Rect card = f.view.GetMetaTreeNodeRect(node, canvasSize);
                    Rect hit = f.view.GetMetaTreeNodeHitRect(node, canvasSize);
                    Rect label = GrowthGraphView.GetMetaTreeNodeLabelRect(card);
                    Check(hit == card && !hit.Contains(label.center), "Title text cannot activate node pointer events: " + node.id);
                }
            }
            f.view.metaTreeZoom = .75f; f.view.metaTreePan = new Vector2(11, -7);
            Vector2 pointer = new Vector2(canvasSize.x * .65f, canvasSize.y * .4f);
            Rect canvas = new Rect(Vector2.zero, canvasSize);
            Check(!f.view.IsPrototypeGraphOverlay(pointer), "Chosen input point is graph canvas, not overlay");
            // Unity exposes Event.current only inside OnGUI. Do not pass a no-op synthetic test in batch edit mode.
            Event.current = new Event { type = EventType.ScrollWheel, mousePosition = pointer, delta = new Vector2(0, -3) };
            if (Event.current == null)
            {
                f.view.FitPrototypeGraph(canvasSize);
                Check(f.view.metaTreePan == Vector2.zero, "Whole-view reset");
                f.run.elapsed = 12.5f; f.run.ToggleMetaTree();
                Check(Time.timeScale == 0 && Near(f.run.elapsed, 12.5f), "Growth map pauses elapsed time");
                f.run.ToggleMetaTree();
                Check(Time.timeScale == 1 && Near(f.run.elapsed, 12.5f), "Growth map resumes elapsed time");
                report.passed.Add("Card-only hit rectangles at six zoom levels, whole-view reset and pause/resume.");
                report.evidence.Add("Native Event tests skipped: edit-mode batch has no OnGUI context; actual GUI input needs the isolated rendered player.");
                return;
            }
            Vector2 worldPoint = (pointer - canvasSize * .5f - f.view.metaTreePan) / f.view.metaTreeZoom;
            Event.current = new Event { type = EventType.ScrollWheel, mousePosition = pointer, delta = new Vector2(0, -3) };
            f.view.HandleMetaTreeInput(canvas);
            Vector2 screenAfter = canvasSize * .5f + f.view.metaTreePan + worldPoint * f.view.metaTreeZoom;
            Check(f.view.metaTreeZoom > .75f, "Synthetic wheel event must actually change zoom");
            Check((screenAfter - pointer).sqrMagnitude < .0001f, "Zoom must stay anchored at pointer");
            foreach (float delta in new[] { -1000f, 1000f })
            {
                Event.current = new Event { type = EventType.ScrollWheel, mousePosition = pointer, delta = new Vector2(0, delta) };
                f.view.HandleMetaTreeInput(canvas);
                Check(f.view.metaTreeZoom >= .04f && f.view.metaTreeZoom <= 1.45f, "Zoom clamp");
            }
            Vector2 overlayPointer = f.view.GrowthOverviewRect.center;
            float priorZoom = f.view.metaTreeZoom;
            Event.current = new Event { type = EventType.ScrollWheel, mousePosition = overlayPointer, delta = new Vector2(0, -2) };
            f.view.HandleMetaTreeInput(canvas);
            Check(Near(f.view.metaTreeZoom, priorZoom), "Overview control cannot scroll underlying graph");
            Event.current = new Event { type = EventType.MouseDown, button = 2, mousePosition = pointer };
            f.view.HandleMetaTreeInput(canvas);
            Check(f.view.isPanningMetaTree, "Middle-button drag starts on canvas");
            Vector2 priorPan = f.view.metaTreePan;
            Event.current = new Event { type = EventType.MouseDrag, button = 2, mousePosition = overlayPointer };
            f.view.HandleMetaTreeInput(canvas);
            Check((f.view.metaTreePan - priorPan - (overlayPointer - pointer)).sqrMagnitude < .0001f, "Drag remains consistent while crossing overlay");
            Event.current = new Event { type = EventType.MouseUp, button = 2, mousePosition = overlayPointer };
            f.view.HandleMetaTreeInput(canvas);
            Check(!f.view.isPanningMetaTree, "Releasing over overlay cannot leave drag stuck");
            f.view.FitPrototypeGraph(canvasSize);
            Vector2 fitPan = f.view.metaTreePan; float fitZoom = f.view.metaTreeZoom;
            f.view.metaTreePan += Vector2.one * 100; f.view.metaTreeZoom = 1.45f;
            f.view.FitPrototypeGraph(canvasSize);
            Check(f.view.metaTreePan == fitPan && Near(f.view.metaTreeZoom, fitZoom), "Whole-view restores panel-safe pan and fit zoom");
            f.run.elapsed = 12.5f; f.run.ToggleMetaTree();
            Check(Time.timeScale == 0 && Near(f.run.elapsed, 12.5f), "Growth map pauses without resetting elapsed time");
            f.run.ToggleMetaTree();
            Check(Time.timeScale == 1 && Near(f.run.elapsed, 12.5f), "Return resumes the same run");
            report.passed.Add("Real Event-based wheel/zoom anchoring and limits, overlay exclusion, middle drag/release, whole-view reset, card-only hit rectangles and pause/resume.");
        }

        private static Rect[] Overlays(float width, float height)
        {
            return GrowthGraphView.GetGrowthOverlayRects(new Vector2(width, height), false);
        }

        private static void CheckLayout()
        {
            Fixture f = new Fixture(); f.view.BuildPrototypeGraph();
            string source = File.ReadAllText("Assets/Scripts/Systems/UI/Growth/GrowthGraphView.cs");
            Check(source.Contains("prototypeGraphCanvasSize != treeCanvas.size"), "Game view resizing must refit");
            Vector2 small = new Vector2(724, 400);
            float legacyZoom = PrototypeRules.GraphFitZoom(f.tables.growthTable.nodes, small.x, small.y);
            Vector2 legacyRootSize = Vector2.one * 82 * Mathf.Clamp(legacyZoom * 1.45f, .2f, 1.2f);
            Rect legacyRoot = new Rect(small * .5f - legacyRootSize * .5f, legacyRootSize);
            Check(Array.Exists(Overlays(small.x, small.y), overlay => overlay.Overlaps(legacyRoot)), "Original small-view root occlusion must be reproducible without panel-safe fit");
            foreach (bool completed in new[] { false, true })
            {
                if (completed) foreach (PrototypeGrowth row in f.tables.growthTable.rows) f.save.progress.SetRank(row.id, row.maxRank);
                f.view.BuildPrototypeGraph();
                foreach (Vector2 size in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(960, 540), new Vector2(724, 400) })
                {
                    foreach (bool ended in new[] { false, true })
                    {
                        f.run.runEnded = ended;
                        f.view.FitPrototypeGraph(size);
                        Rect[] overlays = GrowthGraphView.GetGrowthOverlayRects(size, ended);
                        foreach (MetaTreeNodePresentation node in f.view.metaTreeNodes)
                        {
                            Rect card = f.view.GetMetaTreeNodeRect(node, size);
                            Check(card.xMin >= 0 && card.xMax <= size.x && card.yMin >= 0 && card.yMax <= size.y, "Default fit must keep every card inside screen");
                            if (node.state == MetaTreeNodeState.Hidden) continue;
                            Check(!Array.Exists(overlays, overlay => overlay.Overlaps(card)), $"Whole card must avoid panels: {size}, {node.id}, completed={completed}, ended={ended}");
                        }
                    }
                    report.evidence.Add($"{(completed ? "Completed" : "Fresh")} graph {size.x:0}x{size.y:0}: every visible card avoids fixed overlays, during/end of run.");
                }
            }

            // Place a visible card immediately above the details overlay, a legal pan/zoom result.
            Vector2 pointer = new Vector2(40, 1080 - 230);
            Check(!Array.Exists(Overlays(1920, 1080), overlay => overlay.Contains(pointer)), "Tooltip repro hover point must remain outside overlay");
            float tooltipWidth = 330;
            Rect tooltip = new Rect(60, pointer.y + 18, tooltipWidth, 158);
            Rect details = Overlays(1920, 1080)[2];
            float overlap = Mathf.Max(0, Mathf.Min(tooltip.xMax, details.xMax) - Mathf.Max(tooltip.xMin, details.xMin)) *
                Mathf.Max(0, Mathf.Min(tooltip.yMax, details.yMax) - Mathf.Max(tooltip.yMin, details.yMin));
            Check(overlap > tooltip.width * tooltip.height * .9f, "Regression scenario retains tooltip/panel overlap");
            int tooltipDraw = source.IndexOf("DrawMetaTreeHoverDescription();", StringComparison.Ordinal);
            Check(tooltipDraw > source.IndexOf("DrawPrototypeGrowthDetails();", StringComparison.Ordinal) &&
                tooltipDraw > source.IndexOf("graphics.DrawResultButton(navigation,", StringComparison.Ordinal), "Tooltip must draw after every fixed panel and navigation button");
            Check(!source.Contains("GUI.EndGroup();\n            DrawMetaTreeHoverDescription();"), "Canvas cannot draw tooltip before panels");
            report.passed.Add("GROWTH-01/02 regression: full-card panel-safe fit at four sizes and both run states; tooltip draw order above panels.");
            report.evidence.Add("Layout findings are verified by current view rectangles and draw order, not rendered screenshot/pixel inspection. Actual manual click/visual QA remains separate.");
        }

        private static bool Purchase(Fixture f, PrototypeGrowthNode node)
        {
            bool bought = PrototypeRules.TryPurchaseNode(f.tables.balance, f.tables.GrowthRow(node.growthId), node, f.save.progress,
                PrototypeRules.Stage(f.tables.balance, f.save.progress), ref f.save.metaCurrency, f.tables.growthTable);
            if (!bought) return false;
            f.save.MarkPrototypeProgressDirty(); f.save.FlushPrototypeProgress();
            report.purchases++;
            return true;
        }

        private sealed class Fixture
        {
            internal readonly GameTables tables;
            internal readonly SaveService save;
            internal readonly ProgressionService progression;
            internal readonly RunController run;
            internal readonly GrowthGraphView view;
            internal Fixture()
            {
                tables = new GameTables(
                    JsonUtility.FromJson<PrototypeBalance>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeBalance").text),
                    JsonUtility.FromJson<PrototypeSkillTable>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeSkills").text),
                    JsonUtility.FromJson<PrototypeGrowthTable>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeGrowth").text));
                // Non-sandbox path tests real serialization/flushing, but backend is strictly memory-only.
                save = new SaveService(tables, new MemoryProgressStore(), false); save.Load();
                progression = new ProgressionService(save, tables); progression.ResetSnapshot();
                GameSettings settings = new GameSettings(); WorldArtAssets art = new WorldArtAssets();
                run = new RunController(settings, save, tables, progression, new PlayerWorld(settings, tables, art, new PlayerStats(tables, progression)),
                    () => progression.ResetSnapshot(), () => { }, () => { }, () => { }, () => { }, () => false);
                view = new GrowthGraphView(settings, save, tables, progression, art, run, new GameNotifications(), new UiGraphics());
            }
        }
    }
}
