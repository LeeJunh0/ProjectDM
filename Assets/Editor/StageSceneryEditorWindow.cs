using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM.Editor
{
    /// <summary>Paints persistent stage-relative scenery; preview objects never enter the scene or player saves.</summary>
    public sealed class StageSceneryEditorWindow : EditorWindow
    {
        [SerializeField] private StageDefinition stage;
        [SerializeField] private int paletteIndex, mode;
        [SerializeField] private bool painting, flipX, randomFlip;
        [SerializeField] private float scale = 1, rotation, spacing = .8f, eraseRadius = .8f, snap = .25f;
        private Vector2 scroll, lastPaint;
        private GameObject preview;
        private StageSceneryRenderer renderer;
        private SpriteRenderer ghost;
        private Bounds field;
        private int selected = -1, strokeGroup = -1, sceneControl;
        private StageSceneryLayout Layout => stage != null ? stage.scenery : null;

        [MenuItem("Project DM/스테이지 장식 배치")]
        public static void Open() => GetWindow<StageSceneryEditorWindow>("스테이지 장식 배치");

        private void OnEnable()
        {
            minSize = new Vector2(330, 420);
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (stage == null) stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(StageScenerySetup.StagePath + "/Stage1_Meadow.asset");
            Refresh();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI; Undo.undoRedoPerformed -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EndStroke(); ClearPreview();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) { EndStroke(); ClearPreview(); }
            if (change == PlayModeStateChange.EnteredEditMode) Refresh();
        }

        private void OnInspectorUpdate()
        {
            if (!EditorApplication.isPlaying && renderer != null)
            { renderer.RenderFlames((float)EditorApplication.timeSinceStartup); SceneView.RepaintAll(); }
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                var next = (StageDefinition)EditorGUILayout.ObjectField("스테이지", stage, typeof(StageDefinition), false);
                if (EditorGUI.EndChangeCheck()) { EndStroke(); stage = next; selected = -1; paletteIndex = 0; Refresh(); }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("초원")) ChooseStage("Stage1_Meadow");
                if (GUILayout.Button("석재 던전")) ChooseStage("Stage2_Dungeon");
                if (GUILayout.Button("필드 보기")) { if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(field, false); }
                if (GUILayout.Button("중앙 보기")) { if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(new Bounds(field.center, new Vector3(22, 15, 1)), false); }
                EditorGUILayout.EndHorizontal();
                if (Layout == null)
                {
                    EditorGUILayout.HelpBox("장식 배치 에셋이 없습니다. 리소스를 연결한 뒤 배치하세요.", MessageType.Info);
                    if (GUILayout.Button("장식 리소스 연결")) { StageScenerySetup.Install(); Refresh(); }
                    return;
                }
                if (Layout.palette == null || Layout.placements == null)
                { EditorGUILayout.HelpBox("배치 에셋의 팔레트와 배치 목록을 확인하세요.", MessageType.Warning); return; }
                painting = EditorGUILayout.ToggleLeft("씬 뷰에서 배치 편집", painting);
                mode = GUILayout.Toolbar(mode, new[] { "붓 [B]", "지우개 [E]", "선택 [V]" });
                EditorGUILayout.HelpBox("좌클릭·드래그: 배치 / Shift: 지우기 / Alt: 씬 이동\n선택 모드: 장식 클릭 후 이동 핸들 / Delete: 선택 삭제\nCtrl+Z: 되돌리기 · 위치는 스테이지 배치 에셋에 저장됩니다.", MessageType.None);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawPalette();
                scale = EditorGUILayout.Slider("붓 크기", scale, .25f, 3);
                rotation = EditorGUILayout.Slider("붓 회전", rotation, -180, 180);
                flipX = EditorGUILayout.Toggle("좌우 반전", flipX);
                randomFlip = EditorGUILayout.Toggle("반전 섞기", randomFlip);
                spacing = Mathf.Max(.1f, EditorGUILayout.FloatField("드래그 배치 간격", spacing));
                snap = Mathf.Max(0, EditorGUILayout.FloatField("격자 간격 (0: 자유)", snap));
                eraseRadius = EditorGUILayout.Slider("지우개 반경", eraseRadius, .2f, 4);
                DrawSelection();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("저장된 장식", Layout.placements.Count.ToString());
                if (GUILayout.Button("배치 저장")) { EditorUtility.SetDirty(Layout); AssetDatabase.SaveAssetIfDirty(Layout); }
                if (GUILayout.Button("기본 배치로 교체 (되돌리기 가능)"))
                { Undo.RecordObject(Layout, "기본 장식 배치"); StageScenerySetup.FillDefault(Layout, stage.stageId); Changed(); }
                if (GUILayout.Button("전체 배치 지우기 (되돌리기 가능)"))
                { Undo.RecordObject(Layout, "장식 전체 지우기"); Layout.placements.Clear(); selected = -1; Changed(); }
                EditorGUILayout.EndScrollView();
            }
        }

        private void ChooseStage(string name)
        {
            EndStroke(); stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(StageScenerySetup.StagePath + "/" + name + ".asset");
            selected = -1; paletteIndex = 0; Refresh();
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("장식 팔레트", EditorStyles.boldLabel);
            for (int row = 0; row * 3 < Layout.palette.Length; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < 3; column++)
                {
                    int i = row * 3 + column; if (i >= Layout.palette.Length) break;
                    var prop = Layout.palette[i]; Color previous = GUI.backgroundColor;
                    if (i == paletteIndex) GUI.backgroundColor = new Color(.6f, .85f, 1);
                    if (GUILayout.Button(GUIContent.none, GUILayout.Height(85), GUILayout.MinWidth(80)) && prop?.sprite != null) { paletteIndex = i; mode = 0; painting = true; }
                    GUI.backgroundColor = previous;
                    Rect card = GUILayoutUtility.GetLastRect();
                    GUI.Label(new Rect(card.x, card.yMax - 25, card.width, 20), prop?.displayName ?? "미연결", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
                    Rect rect = new Rect(card.x + 10, card.y + 5, card.width - 20, 52);
                    if (prop?.sprite != null) GUI.DrawTexture(rect, prop.sprite.texture, ScaleMode.ScaleToFit, true);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSelection()
        {
            if (selected < 0 || selected >= Layout.placements.Count) return;
            var item = Layout.placements[selected];
            EditorGUILayout.Space(); EditorGUILayout.LabelField("선택: " + Layout.Find(item.propId)?.displayName, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Vector2 position = EditorGUILayout.Vector2Field("월드 위치", Layout.ToWorld(item.position, field));
            float size = EditorGUILayout.Slider("선택 크기", item.scale, .25f, 3);
            float angle = EditorGUILayout.Slider("선택 회전", item.rotation, -180, 180);
            bool flip = EditorGUILayout.Toggle("선택 좌우 반전", item.flipX);
            if (EditorGUI.EndChangeCheck())
            { Undo.RecordObject(Layout, "장식 속성 변경"); item.position = Layout.FromWorld(Clamp(position), field); item.scale = size; item.rotation = angle; item.flipX = flip; Changed(); }
            if (GUILayout.Button("선택 장식 삭제")) DeleteSelected();
        }

        private Vector2 Clamp(Vector2 position) => new(Mathf.Clamp(position.x, field.min.x, field.max.x), Mathf.Clamp(position.y, field.min.y, field.max.y));
        private Vector2 Snap(Vector2 position) => snap <= 0 ? position : new Vector2(Mathf.Round(position.x / snap) * snap, Mathf.Round(position.y / snap) * snap);

        private void OnSceneGUI(SceneView view)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Layout == null || Layout.palette == null || Layout.placements == null || !painting)
            { if (ghost != null) ghost.enabled = false; return; }
            Event e = Event.current;
            sceneControl = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { painting = false; EndStroke(); Repaint(); return; }
                if (e.keyCode == KeyCode.B) mode = 0;
                else if (e.keyCode == KeyCode.E) mode = 1;
                else if (e.keyCode == KeyCode.V) mode = 2;
                else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) { DeleteSelected(); e.Use(); return; }
                else return;
                e.Use(); Repaint();
            }
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector2 pointer = Snap(ray.GetPoint(distance));
            bool inside = pointer.x >= field.min.x && pointer.x <= field.max.x && pointer.y >= field.min.y && pointer.y <= field.max.y;
            if (ghost != null)
            {
                ghost.enabled = inside && mode == 0 && !e.shift && Layout.palette.Length > 0;
                if (ghost.enabled)
                {
                    var prop = Layout.palette[Mathf.Clamp(paletteIndex, 0, Layout.palette.Length - 1)];
                    ghost.enabled = prop?.sprite != null;
                    if (ghost.enabled)
                    {
                        ghost.sprite = prop.sprite;
                        ghost.transform.position = pointer; ghost.transform.rotation = Quaternion.Euler(0, 0, rotation);
                        ghost.transform.localScale = Vector3.one * prop.height * scale / Mathf.Max(.001f, prop.sprite.bounds.size.y);
                        ghost.flipX = flipX; ghost.color = new Color(prop.tint.r, prop.tint.g, prop.tint.b, .5f);
                    }
                }
            }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == sceneControl) { EndStroke(); e.Use(); return; }
            if (e.alt || e.button == 2) return;
            if (e.type == EventType.Layout && inside) HandleUtility.AddDefaultControl(sceneControl);
            Handles.color = mode == 1 || e.shift ? new Color(1, .4f, .3f, .85f) : new Color(.5f, 1, .65f, .85f);
            if (inside) Handles.DrawWireDisc(pointer, Vector3.forward, mode == 1 || e.shift ? eraseRadius : spacing * .5f);
            if (mode == 2 && selected >= 0 && selected < Layout.placements.Count)
            {
                var item = Layout.placements[selected]; Vector2 position = Layout.ToWorld(item.position, field);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(Layout, "장식 이동"); item.position = Layout.FromWorld(Clamp(Snap(moved)), field); Changed(); }
            }
            if (!inside) return;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (mode == 2 && !e.shift) { selected = Nearest(pointer, 1.5f); e.Use(); Repaint(); return; }
                Undo.IncrementCurrentGroup(); strokeGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("스테이지 장식 붓질");
                GUIUtility.hotControl = sceneControl; lastPaint = new Vector2(float.PositiveInfinity, 0);
                Paint(pointer, mode == 1 || e.shift); e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && GUIUtility.hotControl == sceneControl)
            { if (mode == 1 || e.shift || Vector2.Distance(lastPaint, pointer) >= spacing) Paint(pointer, mode == 1 || e.shift); e.Use(); }
            if (e.type == EventType.MouseMove) view.Repaint();
        }

        private int Nearest(Vector2 position, float radius)
        {
            int found = -1; float closest = radius * radius;
            for (int i = 0; i < Layout.placements.Count; i++)
            {
                float distance = (Layout.ToWorld(Layout.placements[i].position, field) - position).sqrMagnitude;
                if (distance < closest) { closest = distance; found = i; }
            }
            return found;
        }

        private void Paint(Vector2 position, bool erase)
        {
            Undo.RecordObject(Layout, "스테이지 장식");
            if (erase) Layout.placements.RemoveAll(item => Vector2.Distance(Layout.ToWorld(item.position, field), position) <= eraseRadius);
            else if (Layout.palette.Length > 0)
            {
                paletteIndex = Mathf.Clamp(paletteIndex, 0, Layout.palette.Length - 1);
                if (Layout.palette[paletteIndex]?.sprite == null) return;
                // Editor-only deterministic variation; painting never consumes gameplay Random state.
                bool flip = randomFlip ? ((Layout.placements.Count * 1103515245L + 12345) & 1) == 0 : flipX;
                Layout.placements.Add(new StageSceneryLayout.Placement { propId = Layout.palette[paletteIndex].id,
                    position = Layout.FromWorld(position, field), scale = scale, rotation = rotation, flipX = flip });
            }
            lastPaint = position; selected = -1; Changed(false);
        }

        private void EndStroke()
        {
            if (strokeGroup < 0) return;
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(strokeGroup); strokeGroup = -1;
            if (GUIUtility.hotControl == sceneControl) GUIUtility.hotControl = 0;
            if (Layout != null) AssetDatabase.SaveAssetIfDirty(Layout);
        }

        private void DeleteSelected()
        {
            if (Layout == null || selected < 0 || selected >= Layout.placements.Count) return;
            Undo.RecordObject(Layout, "장식 삭제"); Layout.placements.RemoveAt(selected); selected = -1; Changed();
        }

        private void Changed(bool save = true)
        {
            EditorUtility.SetDirty(Layout); if (save) AssetDatabase.SaveAssetIfDirty(Layout);
            if (renderer != null) renderer.Configure(Layout, field);
            Repaint(); SceneView.RepaintAll();
        }

        private void ClearPreview() { if (preview != null) DestroyImmediate(preview); preview = null; renderer = null; ghost = null; }
        private void Refresh()
        {
            ClearPreview();
            if (EditorApplication.isPlayingOrWillChangePlaymode || Layout == null) return;
            var sourceFloor = FindFirstObjectByType<DungeonFloorTilemap>();
            var bounds = FindFirstObjectByType<GameFieldBounds>();
            field = sourceFloor != null && sourceFloor.TryGetWorldBounds(out Bounds floor) ? floor :
                new Bounds(bounds != null ? bounds.transform.position : Vector3.zero, bounds != null ? (Vector3)bounds.Size : new Vector3(100, 100));
            preview = new GameObject("장식 배치 미리보기 · 저장 안 됨", typeof(Grid)) { hideFlags = HideFlags.HideAndDontSave };
            var ground = new GameObject("스테이지 바닥 미리보기", typeof(Tilemap), typeof(TilemapRenderer)) { hideFlags = HideFlags.HideAndDontSave };
            ground.transform.SetParent(preview.transform, false); ground.transform.position = field.center;
            var tiles = ground.AddComponent<DungeonFloorTilemap>(); tiles.SetGroundRuleTile(stage.ground); tiles.Configure(field.size);
            ground.GetComponent<TilemapRenderer>().sortingOrder = -9;
            renderer = preview.AddComponent<StageSceneryRenderer>(); renderer.Configure(Layout, field);
            var brush = new GameObject("붓 미리보기", typeof(SpriteRenderer)) { hideFlags = HideFlags.HideAndDontSave };
            brush.transform.SetParent(preview.transform, false); ghost = brush.GetComponent<SpriteRenderer>(); ghost.sortingOrder = -5; ghost.enabled = false;
            Repaint(); SceneView.RepaintAll();
        }
    }
}
