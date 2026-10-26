using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public sealed class GrowthTreeEditorWindow : EditorWindow
    {
        internal const string AssetPath = "Assets/Resources/ProjectDM/Data/GrowthTrees.asset";
        [SerializeField] GrowthTreeSettings draft;
        Dictionary<string, GameTables> defaults;
        [SerializeField] int treeIndex;
        [SerializeField] string selected, search = "", message = "", filePath = "";
        [SerializeField] Vector2 pan;
        Vector2 listScroll, detailScroll;
        [SerializeField] float zoom = .55f;
        bool connecting, dragging, panning;
        Vector2 pointer;
        GrowthTreeDefinition Tree => draft.trees[treeIndex];
        GrowthTreeNodeSettings Selected => Tree.nodes.FirstOrDefault(node => node.id == selected);
        Color Accent => new[] { new Color(1, .7f, .3f), new Color(.72f, .48f, 1), new Color(1, .4f, .23f), new Color(.52f, .62f, 1), new Color(.35f, .8f, 1) }[treeIndex];

        [MenuItem("Project DM/성장트리 편집기")]
        public static void Open() { var window = GetWindow<GrowthTreeEditorWindow>("성장트리 편집기"); window.minSize = new Vector2(1160, 730); window.Show(); }
        void OnEnable()
        {
            minSize = new Vector2(1160, 730); saveChangesMessage = "수정한 성장트리를 게임에 저장할까요?";
            Undo.undoRedoPerformed += OnUndo;
            try
            {
                defaults = GrowthTreeSettings.Graphs(GameTables.LoadResources(new GameSettings(), false));
                if (draft == null)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GrowthTreeSettings>(AssetPath);
                    draft = asset != null ? Instantiate(asset) : GrowthTreeSettings.Defaults(GameTables.LoadResources(new GameSettings(), false));
                    draft.hideFlags = HideFlags.HideAndDontSave; Fit();
                }
                if (string.IsNullOrEmpty(filePath)) filePath = Path.GetFullPath("Design/GrowthTree.xlsx");
            }
            catch (Exception error) { message = error.Message; }
        }
        void OnDisable()
        { Undo.undoRedoPerformed -= OnUndo; }
        void OnDestroy() { if (draft != null) DestroyImmediate(draft); }
        void OnUndo() { if (draft == null) return; hasUnsavedChanges = true; Repaint(); }
        void Change(string label) { Undo.RecordObject(draft, label); hasUnsavedChanges = true; message = ""; }
        public override void SaveChanges() { if (SaveAsset()) base.SaveChanges(); }

        bool SaveAsset()
        {
            string error = draft.Validate(defaults);
            if (error != null) { message = error; ShowNotification(new GUIContent("설정 오류를 먼저 수정하세요.")); return false; }
            var asset = AssetDatabase.LoadAssetAtPath<GrowthTreeSettings>(AssetPath);
            if (asset == null) { asset = CreateInstance<GrowthTreeSettings>(); AssetDatabase.CreateAsset(asset, AssetPath); }
            Undo.RecordObject(asset, "성장트리 게임 설정 저장"); EditorUtility.CopySerialized(draft, asset);
            asset.name = "GrowthTrees"; asset.hideFlags = HideFlags.None; EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            hasUnsavedChanges = false; message = "게임 설정 저장 완료. 다음 플레이 시작 시 적용됩니다."; return true;
        }

        void Import()
        {
            string path = EditorUtility.OpenFilePanel("성장트리 엑셀/CSV 불러오기", Path.GetDirectoryName(filePath), "");
            if (string.IsNullOrEmpty(path)) return;
            GrowthTreeSettings imported = null;
            try
            {
                imported = GrowthTreeSpreadsheet.Import(path, draft);
                string error = imported.Validate(defaults);
                if (error != null) throw new InvalidOperationException(error);
                Change("엑셀 성장트리 불러오기"); draft.trees = imported.trees; selected = null; filePath = path; Fit();
                message = "엑셀을 편집 초안으로 불러왔습니다. 확인 후 게임에 저장하세요.";
            }
            catch (Exception error) { message = error.Message; }
            finally { if (imported != null) DestroyImmediate(imported); }
        }
        void Export()
        {
            string path = EditorUtility.SaveFilePanel("현재 성장트리를 엑셀로 저장", Path.GetDirectoryName(filePath), "GrowthTree", "xlsx");
            if (string.IsNullOrEmpty(path)) return;
            try { GrowthTreeSpreadsheet.Export(path, draft, defaults); filePath = path; message = "엑셀 저장 완료. Nodes 시트에서 노드와 연결을 편집하세요."; }
            catch (Exception error) { message = error.Message; }
        }

        void OnGUI()
        {
            if (draft == null || defaults == null) { EditorGUILayout.HelpBox(message, MessageType.Error); return; }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int next = EditorGUILayout.Popup(treeIndex, GrowthTreeSettings.TreeNames, EditorStyles.toolbarPopup, GUILayout.Width(120));
                if (next != treeIndex) { treeIndex = next; selected = null; Fit(); }
                if (GUILayout.Button("엑셀/CSV 불러오기", EditorStyles.toolbarButton, GUILayout.Width(130))) Import();
                if (GUILayout.Button("엑셀 저장", EditorStyles.toolbarButton, GUILayout.Width(80))) Export();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("게임에 저장" + (hasUnsavedChanges ? " *" : ""), EditorStyles.toolbarButton, GUILayout.Width(110))) SaveAsset();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(215))) DrawList();
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        connecting = GUILayout.Toggle(connecting, "표시 연결", EditorStyles.miniButton, GUILayout.Width(80));
                        if (GUILayout.Button("격자 지도 정리", GUILayout.Width(100)))
                        { Change("성장 격자 지도 배치"); GrowthTreeLayout.Arrange(Tree); Fit(); message = "성장별 격자를 가지와 분기로 이었습니다. 구매 조건은 유지됩니다."; }
                        if (GUILayout.Button("전체 보기", GUILayout.Width(75))) Fit(true);
                        GUILayout.Label("확대", GUILayout.Width(28)); zoom = EditorGUILayout.Slider(zoom, .04f, 1.2f);
                    }
                    Rect canvas = GUILayoutUtility.GetRect(100, 10000, 300, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawGraph(canvas);
                    EditorGUILayout.LabelField("드래그: 위치 이동 · 휠: 확대 · 빈 공간/가운데 버튼: 화면 이동", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("표시 연결: 앞 노드 선택 → 다음 노드 클릭. 해금 조건은 오른쪽에서 별도로 설정합니다.", EditorStyles.miniLabel);
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(290))) DrawDetails();
            }
            string invalid = draft.Validate(defaults);
            EditorGUILayout.HelpBox(invalid ?? (string.IsNullOrEmpty(message) ? "엑셀은 Nodes 시트를 읽습니다. 초안 편집 후 '게임에 저장'으로 반영합니다." : message), invalid != null ? MessageType.Error : MessageType.Info);
        }

        void DrawList()
        {
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("노드 추가")) AddNode();
                using (new EditorGUI.DisabledScope(Selected == null)) if (GUILayout.Button("선택 삭제")) RemoveNode();
            }
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            if (GUILayout.Toggle(selected == "ROOT", "ROOT · 시작점", EditorStyles.miniButton)) selected = "ROOT";
            foreach (var node in Tree.nodes)
            {
                if (!string.IsNullOrWhiteSpace(search) && (node.id + node.name + node.growthId).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (GUILayout.Toggle(selected == node.id, (node.enabled ? "● " : "○ ") + node.name, new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft }, GUILayout.Height(25)) && selected != node.id)
                { selected = node.id; pan = new Vector2(-node.x, -node.y); zoom = Mathf.Max(.55f, zoom); }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"생성 {Tree.nodes.Count(n => n.enabled)} / 전체 {Tree.nodes.Length}", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("같은 성장 종류의 구매 순서는 목록/엑셀 행 순서입니다. 비활성 노드는 게임에서 생성하지 않습니다.", MessageType.None);
        }

        void AddNode()
        {
            var rows = defaults[Tree.id].growthTable.rows;
            var row = rows.FirstOrDefault(item => item.id == Selected?.growthId && Tree.nodes.Where(n => n.enabled && n.growthId == item.id).Sum(n => n.ranks) < item.maxRank)
                ?? rows.FirstOrDefault(item => Tree.nodes.Where(n => n.enabled && n.growthId == item.id).Sum(n => n.ranks) < item.maxRank);
            if (row == null) { message = "모든 성장 종류가 최대 단계까지 배치되어 있습니다. 기존 노드의 구매 단계를 줄이거나 생성을 해제한 뒤 추가하세요."; return; }
            string id; int suffix = 1;
            do { id = Tree.id + "." + row.id + "." + suffix++; } while (draft.trees.Any(tree => tree.nodes.Any(node => node.id == id)));
            var parent = Selected; Change("성장 노드 추가");
            var node = new GrowthTreeNodeSettings { id = id, name = row.name, growthId = row.id, ranks = Math.Min(3, row.maxRank - Tree.nodes.Where(n => n.enabled && n.growthId == row.id).Sum(n => n.ranks)),
                parent = parent?.enabled == true ? parent.id : "ROOT", parentRanks = parent?.enabled == true ? 1 : 0, x = (parent?.x ?? 0) + 210, y = parent?.y ?? 160 };
            Tree.nodes = Tree.nodes.Concat(new[] { node }).ToArray(); selected = id;
        }
        void RemoveNode()
        {
            var node = Selected; if (node == null) return; Change("성장 노드 삭제");
            foreach (var child in Tree.nodes.Where(child => child.parent == node.id))
            {
                child.parent = node.parent; var parent = Tree.nodes.FirstOrDefault(n => n.id == node.parent);
                child.parentRanks = parent == null ? 0 : Math.Min(child.parentRanks, parent.ranks);
            }
            foreach (var child in Tree.nodes.Where(child => child.displayParent == node.id)) child.displayParent = node.VisualParent;
            foreach (var other in Tree.nodes) other.displayLinks = (other.displayLinks ?? Array.Empty<string>()).Where(id => id != node.id).ToArray();
            Tree.nodes = Tree.nodes.Where(n => n != node).ToArray(); selected = null;
        }

        void DrawDetails()
        {
            EditorGUILayout.LabelField("노드 설정", EditorStyles.boldLabel);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            var node = Selected;
            if (node == null) { EditorGUILayout.HelpBox("노드를 선택하면 생성 여부·성장 종류·연결을 편집할 수 있습니다.", MessageType.Info); EditorGUILayout.EndScrollView(); return; }
            EditorGUILayout.LabelField(node.id, EditorStyles.wordWrappedMiniLabel);
            var rows = defaults[Tree.id].growthTable.rows; int index = Array.FindIndex(rows, row => row.id == node.growthId);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("게임에서 생성", node.enabled);
            string name = EditorGUILayout.TextField("표시 이름", node.name);
            int effect = EditorGUILayout.Popup("성장 종류", Math.Max(0, index), rows.Select(row => row.name + " · " + row.id).ToArray());
            int ranks = EditorGUILayout.IntSlider("구매 단계", node.ranks, 1, 3);
            var parents = new[] { "ROOT" }.Concat(Tree.nodes.Where(n => n != node && n.enabled).Select(n => n.id)).ToArray();
            int p = Array.IndexOf(parents, string.IsNullOrEmpty(node.parent) ? "ROOT" : node.parent);
            // Keep an invalid draft dependency visible; do not silently reconnect it to ROOT.
            if (p < 0) { parents = parents.Concat(new[] { node.parent ?? "" }).ToArray(); p = parents.Length - 1; }
            int parentIndex = EditorGUILayout.Popup("해금 기준 부모", p, parents);
            int required = EditorGUILayout.IntField("부모 필요 구매 단계", node.parentRanks);
            var displayParents = new[] { "ROOT" }.Concat(Tree.nodes.Where(n => n != node && n.enabled).Select(n => n.id)).ToArray();
            int displayIndex = Array.IndexOf(displayParents, node.VisualParent);
            if (displayIndex < 0) { displayParents = displayParents.Concat(new[] { node.VisualParent ?? "" }).ToArray(); displayIndex = displayParents.Length - 1; }
            displayIndex = EditorGUILayout.Popup("표시 연결", displayIndex, displayParents);
            bool orthogonal = EditorGUILayout.Toggle("직각 연결", node.displayOrthogonal);
            string extraLinks = EditorGUILayout.TextField("추가 표시 연결", string.Join("|", node.displayLinks ?? Array.Empty<string>()));
            float x = EditorGUILayout.FloatField("X", node.x), y = EditorGUILayout.FloatField("Y", node.y);
            if (EditorGUI.EndChangeCheck())
            {
                Change("성장 노드 설정"); node.enabled = enabled; node.name = name; node.growthId = rows[effect].id; node.ranks = ranks;
                node.parent = parents[parentIndex]; node.parentRanks = node.parent == "ROOT" ? 0 : required;
                bool moved = node.x != x || node.y != y;
                node.displayParent = displayParents[displayIndex]; node.displayOrthogonal = orthogonal; node.x = x; node.y = y;
                node.displayLinks = extraLinks.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim()).ToArray();
                if (moved) FreeConnections(node);
            }
            var row = rows[effect];
            EditorGUILayout.HelpBox($"{row.description}\n종류 최대 {row.maxRank}단계 · 기본 가격 {row.baseCost}\n선행 성장: {(string.IsNullOrEmpty(row.requires) ? "없음" : row.requires)}", MessageType.None);
            EditorGUILayout.HelpBox("표시 연결·추가 표시 연결: 화면의 선\n추가 연결은 노드 ID를 |로 구분합니다.\n해금 기준 부모·필요 단계: 실제 구매 조건\n격자 지도 정리는 구매 조건을 바꾸지 않습니다.", MessageType.None);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("표시상 다음 노드", EditorStyles.boldLabel);
            foreach (var child in Tree.nodes.Where(item => item.VisualParent == node.id))
                if (GUILayout.Button(child.name + " · " + child.id)) selected = child.id;
            using (new EditorGUILayout.HorizontalScope())
            {
                int at = Array.IndexOf(Tree.nodes, node);
                using (new EditorGUI.DisabledScope(at <= 0)) if (GUILayout.Button("순서 위로")) { Change("구매 순서 이동"); (Tree.nodes[at - 1], Tree.nodes[at]) = (Tree.nodes[at], Tree.nodes[at - 1]); }
                using (new EditorGUI.DisabledScope(at >= Tree.nodes.Length - 1)) if (GUILayout.Button("순서 아래로")) { Change("구매 순서 이동"); (Tree.nodes[at + 1], Tree.nodes[at]) = (Tree.nodes[at], Tree.nodes[at + 1]); }
            }
            EditorGUILayout.EndScrollView();
        }

        void Fit(bool overview = false)
        {
            if (draft == null) return;
            float minX = Math.Min(0, Tree.nodes.Length == 0 ? 0 : Tree.nodes.Min(n => n.x)), maxX = Math.Max(0, Tree.nodes.Length == 0 ? 0 : Tree.nodes.Max(n => n.x));
            float minY = Math.Min(0, Tree.nodes.Length == 0 ? 0 : Tree.nodes.Min(n => n.y)), maxY = Math.Max(0, Tree.nodes.Length == 0 ? 0 : Tree.nodes.Max(n => n.y));
            zoom = Mathf.Clamp(Mathf.Min(Mathf.Max(300, position.width - 560) / (maxX - minX + 280), Mathf.Max(400, position.height - 150) / (maxY - minY + 200)), .04f, 1.2f);
            pan = new Vector2(-(minX + maxX) * .5f, -(minY + maxY) * .5f);
            if (!overview)
            {
                float closeZoom = Mathf.Clamp(Mathf.Min(Mathf.Max(300, position.width - 560) / 1100f,
                    Mathf.Max(400, position.height - 150) / 700f), .4f, .75f);
                zoom = Mathf.Max(zoom, closeZoom);
                pan = Vector2.zero;
            }
        }

        void DrawGraph(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(.085f, .1f, .12f)); GUI.BeginClip(canvas);
            Vector2 Center(float x, float y) => canvas.size * .5f + (new Vector2(x, y) + pan) * zoom;
            Rect Card(float x, float y) => new Rect(Center(x, y) - new Vector2(75, 30) * zoom, new Vector2(150, 60) * zoom);
            var rootRect = Card(0, 0); var ev = Event.current; Vector2 mouse = ev.mousePosition;
            if (ev.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                foreach (var node in Tree.nodes)
                {
                    var parent = Tree.nodes.FirstOrDefault(n => n.id == node.VisualParent);
                    if (parent == null && node.VisualParent != "ROOT" && !string.IsNullOrEmpty(node.VisualParent)) continue;
                    Vector2 from = Center(parent?.x ?? 0, parent?.y ?? 0), to = Center(node.x, node.y);
                    Handles.color = node.enabled ? new Color(Accent.r, Accent.g, Accent.b, .6f) : Color.gray;
                    Handles.DrawAAPolyLine(2, GrowthConnectionPath.Points(from, to, node.displayOrthogonal, zoom).Select(point => (Vector3)point).ToArray());
                    foreach (string link in node.displayLinks ?? Array.Empty<string>())
                    {
                        var neighbor = Tree.nodes.FirstOrDefault(n => n.id == link);
                        if (neighbor != null) Handles.DrawAAPolyLine(2, (Vector3)Center(neighbor.x, neighbor.y), (Vector3)to);
                    }
                }
                Handles.EndGUI();
            }
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = Math.Max(8, Mathf.RoundToInt(12 * zoom)), normal = { textColor = Color.white } };
            EditorGUI.DrawRect(rootRect, selected == "ROOT" ? Accent * .6f : new Color(.25f, .28f, .33f)); GUI.Label(rootRect, "ROOT", style);
            foreach (var node in Tree.nodes)
            {
                Rect rect = Card(node.x, node.y);
                EditorGUI.DrawRect(rect, selected == node.id ? Accent * .6f : node.enabled ? new Color(.2f, .23f, .28f) : new Color(.13f, .13f, .13f));
                GUI.Label(rect, new GUIContent(zoom < .25f ? "" : (node.enabled ? "" : "비활성 · ") + node.name + "\n" + node.ranks + "단계",
                    node.name + "\n" + node.id + "\n표시 연결: " + node.VisualParent + "\n해금 부모: " + node.parent + " · 필요 구매: " + node.parentRanks), style);
            }
            if (canvas.Contains(ev.mousePosition + canvas.position))
            {
                if (ev.type == EventType.ScrollWheel)
                {
                    Vector2 world = (mouse - canvas.size * .5f) / zoom - pan; zoom = Mathf.Clamp(zoom * Mathf.Pow(1.12f, -ev.delta.y), .04f, 1.2f);
                    pan = (mouse - canvas.size * .5f) / zoom - world; ev.Use(); Repaint();
                }
                if (ev.type == EventType.MouseDown && (ev.button == 0 || ev.button == 2))
                {
                    var hit = Tree.nodes.LastOrDefault(n => Card(n.x, n.y).Contains(mouse)); string hitId = hit?.id ?? (rootRect.Contains(mouse) ? "ROOT" : null);
                    if (ev.button == 0 && connecting && selected != null && hit != null && hit.id != selected)
                    {
                        string old = hit.displayParent; hit.displayParent = selected;
                        string error = draft.Validate(defaults); hit.displayParent = old;
                        if (error == null) { Change("성장 노드 표시 연결"); hit.displayParent = selected; hit.displayOrthogonal = false; }
                        else message = error;
                    }
                    else { selected = hitId; dragging = hit != null && ev.button == 0 && !connecting; panning = ev.button == 2 || hitId == null; if (dragging) Change("성장 노드 위치 이동"); }
                    pointer = mouse; ev.Use(); Repaint();
                }
            }
            if (ev.type == EventType.MouseDrag && (dragging || panning))
            {
                Vector2 delta = (mouse - pointer) / zoom; pointer = mouse;
                if (dragging && Selected != null) { Selected.x += delta.x; Selected.y += delta.y; FreeConnections(Selected); } else pan += delta;
                ev.Use(); Repaint();
            }
            if (ev.type == EventType.MouseUp) { dragging = panning = false; }
            GUI.EndClip();
        }

        void FreeConnections(GrowthTreeNodeSettings node)
        {
            node.displayOrthogonal = false;
            foreach (var child in Tree.nodes.Where(n => n.VisualParent == node.id)) child.displayOrthogonal = false;
        }
    }
}
