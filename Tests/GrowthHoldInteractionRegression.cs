// Copy to an isolated project's Assets/Editor; execute ProjectDM.Editor.GrowthHoldInteractionRegression.Run.
using System;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class GrowthHoldInteractionRegression
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException(message);
        }

        public static void Run()
        {
            try
            {
                var hold = new GrowthPurchaseHold();
                Check(!hold.Update("a", true, false, true, 0), "Holding before entering cannot begin a purchase");
                Check(!hold.Update("a", true, true, true, 1), "Mouse down must not purchase");
                Check(!hold.Update("a", true, false, true, 1.25f) && Math.Abs(hold.Progress(1.25f) - .25f) < .0001f, "Quarter-second hold fills one quarter without purchase");
                Check(!hold.Update("a", true, false, false, 1.3f) && hold.Progress(1.3f) == 0, "Early release cancels");
                hold.Update("a", true, true, true, 2);
                Check(!hold.Update("a", true, false, true, 2.99f), "One second is required before purchase");
                Check(hold.Update("a", true, false, true, 3f), "One-second threshold purchases once");
                Check(!hold.IsHolding, "Completing must end hold enlargement even before release");
                Check(!hold.Update("a", true, false, true, 10), "Continued hold cannot purchase additional ranks");
                Check(!hold.Update("a", true, false, false, 11), "Release does not purchase");
                hold.Update("a", true, true, true, 12);
                Check(hold.Update("a", true, false, true, 13f), "A new press permits the next rank");
                hold.Update("a", true, true, true, 20);
                hold.Update(null, true, false, true, 20.2f);
                Check(!hold.Update("a", true, false, true, 21) && hold.NodeId == null, "Leaving then returning cancels until next press");
                hold.Update("a", true, true, true, 22);
                Check(!hold.Update("b", true, false, true, 23) && hold.NodeId == null, "Dragging to another node cannot transfer progress");
                hold.Update("a", true, true, true, 24);
                Check(!hold.Update("a", false, false, true, 25), "Losing eligibility cancels before spending");
                Check(!hold.Update("a", true, false, true, 26), "Restoring eligibility requires a new press");
                Check(!hold.Update("a", false, true, true, 27) && hold.NodeId == null, "Locked, completed, or unaffordable nodes cannot start");
                hold.Update("a", true, true, true, 28);
                hold.Clear();
                Check(!hold.Update("a", true, false, true, 30), "Navigation reset cannot resume old progress");

                var tables = GameTables.LoadResources(new GameSettings());
                var graph = tables.characters.CommonGraph;
                var save = new PrototypeSave();
                var node = Array.Find(graph.growthTable.nodes, candidate => candidate.VisualParent == "ROOT" &&
                    PrototypeRules.NodePurchaseBlock(graph.balance, graph.GrowthRow(candidate.growthId), candidate, save, 0, 100000, graph.growthTable) == null);
                Check(node != null, "Fresh graph must have a purchasable root neighbor");
                var row = graph.GrowthRow(node.growthId);
                int wallet = 100000, price = PrototypeRules.Cost(row, 0, graph.balance.latePriceScale);
                hold.Update(node.id, true, true, true, 40);
                Check(!hold.Update(node.id, true, false, true, 40.2f) && wallet == 100000 && save.Rank(row.id) == 0, "Incomplete hold cannot change saved growth or currency");
                Check(hold.Update(node.id, true, false, true, 41f) &&
                    PrototypeRules.TryPurchaseNode(graph.balance, row, node, save, 0, ref wallet, graph.growthTable), "Completed hold purchases through real graph prerequisites");
                Check(wallet == 100000 - price && save.Rank(row.id) == 1, "Only one rank and its exact price are applied");
                Check(!hold.Update(node.id, true, false, true, 45) && wallet == 100000 - price, "Continued hold cannot spend again");

                var feedback = new GrowthNodeFeedback();
                Check(feedback.Scale("a", false, false, 0, 0) == 1, "Nodes begin at their normal size");
                float hoverScale = feedback.Scale("a", true, false, 0, .1f);
                Check(hoverScale > 1 && hoverScale < 1.06f, "Hover grows smoothly without snapping");
                float heldScale = feedback.Scale("a", true, true, .9f, .3f);
                Check(heldScale > hoverScale && heldScale <= 1.14f, "Holding grows a little more");
                float releaseScale = feedback.Scale("a", true, false, 0, .4f);
                Check(releaseScale < heldScale && releaseScale > 1.06f, "Cancel or completion eases back to hover size");
                Check(Mathf.Abs(feedback.Scale("a", false, false, 0, 2) - 1) < .0001f, "Leaving returns to normal size");
                Check(float.IsPositiveInfinity(feedback.PurchaseAge("a", 2)), "Hover and cancellation do not spawn completion sparkles");
                feedback.Purchased("a", 3);
                feedback.Purchased("b", 3.2f);
                Check(Mathf.Abs(feedback.PurchaseAge("a", 3.4f) - .4f) < .0001f && Mathf.Abs(feedback.PurchaseAge("b", 3.4f) - .2f) < .0001f,
                    "Quick purchases retain independent sparkle ages");
                foreach (float fps in new[] { 30f, 60f, 144f })
                {
                    var timed = new GrowthNodeFeedback();
                    timed.Scale("a", false, false, 0, 0);
                    float scale = 1;
                    for (int frame = 1; frame <= fps; frame++) scale = timed.Scale("a", true, false, 0, frame / fps);
                    Check(Mathf.Abs(scale - 1.06f) < .0001f, "Smooth hover must settle consistently at different frame rates");
                }
                feedback.Clear();
                Check(feedback.Scale("a", false, false, 0, 4) == 1 && float.IsPositiveInfinity(feedback.PurchaseAge("a", 4)), "Navigation clears scale and sparkles");
                for (int particle = 0; particle < 8; particle++)
                {
                    Check(GrowthTreeEffects.PurchaseSparkleOpacity(0, particle) == 0 && GrowthTreeEffects.PurchaseSparkleOpacity(GrowthTreeEffects.PurchaseSparkleDuration, particle) == 0,
                        "Sparkles start empty and disappear completely");
                    Check(GrowthTreeEffects.PurchaseSparkleOpacity(particle * .022f + .2f, particle) > 0, "Staggered particles become visible");
                }

                Rect inside = new Rect(100, 100, 72, 72);
                foreach (float progress in new[] { -1f, 0, .25f, .5f, 1, 2 })
                {
                    Rect fill = GrowthGraphView.GrowthHoldFillRect(inside, progress);
                    Check(fill.yMax == inside.yMax && fill.height >= 0 && fill.height <= inside.height, "Fill must grow upward inside the node");
                }
                Check(GrowthGraphView.GrowthHoldFillRect(inside, .5f).y == 136, "Half fill rises from the bottom");
                Rect fullFill = GrowthGraphView.GrowthHoldFillRect(inside, 1);
                Rect centerBand = GrowthGraphView.GrowthHoldFillBand(fullFill, inside, 8, 2);
                Check(centerBand.xMin == inside.xMin && centerBand.xMax == inside.xMax, "Fill must touch both inner side walls");
                float coveredHeight = 0;
                for (int band = 0; band < 5; band++)
                {
                    Rect part = GrowthGraphView.GrowthHoldFillBand(fullFill, inside, 8, band);
                    coveredHeight += part.height;
                    Check(part.xMin >= inside.xMin && part.xMax <= inside.xMax, "Corner fill cannot escape the inner frame");
                    if (band == 0) Check(part.yMin == inside.yMin && part.xMin > inside.xMin, "Top fill touches rim while leaving the stepped corner clear");
                    if (band == 4) Check(part.yMax == inside.yMax, "Bottom fill touches the inner rim");
                }
                Check(coveredHeight == inside.height, "Fill bands must not leave horizontal gaps");
                foreach (Vector2 screen in new[] { new Vector2(1920,1080), new Vector2(960,540), new Vector2(480,320) })
                foreach (Vector2 pointer in new[] { Vector2.zero, new Vector2(screen.x, 0), screen, new Vector2(0, screen.y), screen * .5f })
                foreach (Vector2 size in new[] { new Vector2(360,230), new Vector2(600,800) })
                {
                    Rect rect = GrowthGraphView.GrowthTooltipRect(pointer, size, screen);
                    Check(rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= screen.x && rect.yMax <= screen.y, "Tooltip must stay in the viewport at every edge");
                    if (size.x < screen.x && pointer.x == screen.x) Check(rect.xMax < pointer.x, "Right edge must flip tooltip left");
                    if (size.y < screen.y && pointer.y == screen.y) Check(rect.yMax < pointer.y, "Bottom edge must flip tooltip up");
                }
                Debug.Log("GROWTH HOLD INTERACTION PASSED: " + checks + " assertions");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
