// Run in an isolated Unity project: ProjectDM.Editor.GrowthNavigationRegression.Run.
using System;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class GrowthNavigationRegression
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        private sealed class InputWindow : EditorWindow
        {
            private bool checkedInput;
            private void OnGUI()
            {
                if (checkedInput || Event.current.type != EventType.Repaint) return;
                checkedInput = true; CheckInput();
            }
        }
        public static void Run()
        {
            var window = ScriptableObject.CreateInstance<InputWindow>();
            window.position = new Rect(0, 0, 1920, 1080);
            window.ShowUtility();
        }
        private static void CheckInput()
        {
            try
            {
                var settings = new GameSettings(); var tables = GameTables.LoadResources(settings);
                var save = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave() };
                var run = new RunController(settings, save, tables, null, null, null, null, null, () => { }, () => { }, () => false, new RunMana());
                using (var view = new GrowthGraphView(settings, save, tables, null, null, run, null, null))
                {
                    var canvas = new Rect(0, 0, 1920, 1080); view.BuildPrototypeGraph(); view.FitPrototypeGraph(canvas.size);
                    Check(view.metaTreeZoom == 1.45f && view.metaTreePan == Vector2.zero, "Opening must use the reference close view centered on ROOT");
                    var blank = new Vector2(1820, 540);
                    Send(view, canvas, EventType.ScrollWheel, blank, new Vector2(0, 100));
                    Check(view.metaTreeZoom == GrowthGraphView.DefaultZoom * .5f, "Zoom out must stop at half the default scale");
                    Send(view, canvas, EventType.ScrollWheel, blank, new Vector2(0, -100));
                    Check(view.metaTreeZoom == GrowthGraphView.DefaultZoom, "Zoom in must not exceed the default scale");
                    view.FitPrototypeGraph(canvas.size);
                    Send(view, canvas, EventType.MouseDown, blank);
                    Check(view.isPanningMetaTree, "Left press on empty canvas starts moving the map: " + Event.current.type + " / " + Event.current.rawType + " / " + Event.current.mousePosition);
                    Send(view, canvas, EventType.MouseDrag, blank + new Vector2(30, 20));
                    Check(view.metaTreePan == new Vector2(30, 20), "Map follows the left drag");
                    Send(view, canvas, EventType.MouseUp, new Vector2(30, 30));
                    Check(!view.isPanningMetaTree, "Releasing over a fixed panel must end dragging");
                    view.FitPrototypeGraph(canvas.size);
                    for (int i = 1; i < view.metaTreeNodes.Length; i++)
                    {
                        if (view.metaTreeNodes[i].state == MetaTreeNodeState.Hidden) continue;
                        Vector2 point = view.GetMetaTreeNodeCenter(view.metaTreeNodes[i], canvas.size);
                        if (view.IsPrototypeGraphOverlay(point)) continue;
                        Send(view, canvas, EventType.MouseDown, point);
                        Check(!view.isPanningMetaTree && Event.current.type == EventType.MouseDown, "Node press must remain available for holding to buy");
                    }
                    Send(view, canvas, EventType.MouseDown, new Vector2(30, 30));
                    Check(!view.isPanningMetaTree, "Fixed UI cannot start a map drag");
                    Send(view, canvas, EventType.MouseDown, blank, default, 2);
                    Check(!view.isPanningMetaTree, "Middle mouse no longer starts dragging");
                    view.debugControlsExpanded = false;
                    Check(!view.IsPrototypeGraphOverlay(new Vector2(200, 30)), "Collapsed stage controls must release their map input area");
                    Check(view.IsPrototypeGraphOverlay(new Vector2(40, 30)), "Debug toggle remains clickable when collapsed");
                    view.debugControlsExpanded = true;
                    Check(view.IsPrototypeGraphOverlay(new Vector2(200, 30)), "Expanded stage controls must block map input");
                }
                Debug.Log("GROWTH NAVIGATION PASSED: " + checks + " assertions"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void Send(GrowthGraphView view, Rect canvas, EventType type, Vector2 point, Vector2 delta = default, int button = 0)
        {
            Event.current = new Event { type = type, mousePosition = point, delta = delta, button = button };
            view.HandleMetaTreeInput(canvas);
        }
    }
}
