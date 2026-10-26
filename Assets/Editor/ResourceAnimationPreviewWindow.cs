using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public sealed class ResourceAnimationPreviewWindow : EditorWindow
    {
        private static readonly string[] Categories = { "전체", "캐릭터", "몬스터", "보스", "스킬", "드랍템", "기타" };
        [SerializeField] private string selectedKey, search = "";
        [SerializeField] private int category, zoom = 6;
        [SerializeField] private float speed = 1;
        [SerializeField] private bool playing = true, grid, compare, flip;
        [SerializeField] private Color background = new Color(.13f, .15f, .17f);
        private List<ResourceAnimationSequence> sequences = new List<ResourceAnimationSequence>();
        private Vector2 listScroll, stripScroll;
        private double elapsed, lastUpdate;
        private ResourceAnimationSequence Current => sequences.FirstOrDefault(sequence => sequence.key == selectedKey);

        [MenuItem("Project DM/리소스 애니메이션 미리보기")]
        public static void Open()
        {
            var window = GetWindow<ResourceAnimationPreviewWindow>("리소스 애니메이션");
            window.minSize = new Vector2(860, 660); window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(860, 660);
            Reload(); lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        private void OnDisable() => EditorApplication.update -= Tick;
        private void OnProjectChange() { Reload(); Repaint(); }
        private void Reload()
        {
            sequences = ResourceAnimationPreviewLibrary.Load();
            if (Current == null) Select(sequences.FirstOrDefault(sequence => sequence.direction == "Down") ?? sequences.FirstOrDefault());
        }
        private void Select(ResourceAnimationSequence sequence)
        { selectedKey = sequence?.key; elapsed = 0; stripScroll = Vector2.zero; }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup, delta = Math.Max(0, now - lastUpdate); lastUpdate = now;
            if (playing && Current != null && Current.frames.Length > 1)
            {
                int previous = Current.FrameAt(elapsed);
                elapsed = (elapsed + delta * speed) % Current.Duration;
                if (Current.FrameAt(elapsed) != previous) Repaint();
            }
        }
        private void Scrub(int frame)
        { if (Current == null) return; playing = false; elapsed = Current.TimeAt(frame); Repaint(); }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                category = EditorGUILayout.Popup(category, Categories, EditorStyles.toolbarPopup, GUILayout.Width(90));
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(75))) Reload();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(235)))
                {
                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    string previous = null;
                    foreach (var sequence in sequences.Where(item => (category == 0 || item.category == Categories[category]) &&
                        (string.IsNullOrWhiteSpace(search) || (item.label + item.category).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        if (previous != sequence.category)
                        { EditorGUILayout.LabelField(sequence.category, EditorStyles.boldLabel); previous = sequence.category; }
                        var style = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
                        if (GUILayout.Toggle(selectedKey == sequence.key, sequence.label, style, GUILayout.MinHeight(28)) && selectedKey != sequence.key) Select(sequence);
                    }
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.LabelField($"총 {sequences.Count}개 리소스", EditorStyles.miniLabel);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    var sequence = Current;
                    if (sequence == null) { EditorGUILayout.HelpBox("연결된 리소스가 없습니다. 아트 설정을 확인하고 새로고침하세요.", MessageType.Info); return; }
                    EditorGUILayout.LabelField(sequence.label, EditorStyles.boldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(sequence.frames.Length < 2))
                        {
                            if (GUILayout.Button("이전", GUILayout.Width(45))) Scrub((sequence.FrameAt(elapsed) + sequence.frames.Length - 1) % sequence.frames.Length);
                            bool nextPlaying = GUILayout.Toggle(playing, playing ? "일시정지" : "재생", EditorStyles.miniButton, GUILayout.Width(70));
                            if (nextPlaying != playing) { playing = nextPlaying; lastUpdate = EditorApplication.timeSinceStartup; }
                            if (GUILayout.Button("다음", GUILayout.Width(45))) Scrub((sequence.FrameAt(elapsed) + 1) % sequence.frames.Length);
                        }
                        GUILayout.Label("속도", GUILayout.Width(28)); speed = EditorGUILayout.Slider(speed, .1f, 3f);
                        if (GUILayout.Button("1×", GUILayout.Width(32))) speed = 1;
                    }
                    int frame = sequence.FrameAt(elapsed);
                    EditorGUI.BeginChangeCheck(); int chosen = EditorGUILayout.IntSlider("프레임", frame, 0, sequence.frames.Length - 1);
                    if (EditorGUI.EndChangeCheck()) Scrub(chosen);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        zoom = EditorGUILayout.IntSlider("픽셀 배율", zoom, 1, 12);
                        grid = GUILayout.Toggle(grid, "격자", GUILayout.Width(48));
                        flip = GUILayout.Toggle(flip, "좌우 반전", GUILayout.Width(80));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        background = EditorGUILayout.ColorField("배경", background);
                        using (new EditorGUI.DisabledScope(sequence.direction == null)) compare = GUILayout.Toggle(compare, "4방향 비교", GUILayout.Width(100));
                        if (GUILayout.Button("원본 설정 선택", GUILayout.Width(105)))
                        { Selection.activeObject = sequence.source; EditorGUIUtility.PingObject(sequence.source); }
                    }
                    Rect canvas = GUILayoutUtility.GetRect(100, 10000, 200, 10000, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(canvas, background);
                    var directions = compare && sequence.direction != null ? sequences.Where(item => item.family == sequence.family && item.direction != null).ToArray() : null;
                    if (directions != null && directions.Length == 4)
                    {
                        for (int i = 0; i < directions.Length; i++)
                        {
                            Rect cell = new Rect(canvas.x + i * canvas.width / 4, canvas.y, canvas.width / 4, canvas.height);
                            GUI.Label(new Rect(cell.x + 5, cell.y + 6, cell.width - 10, 20), directions[i].direction, EditorStyles.whiteLabel);
                            DrawFrame(cell, directions[i].frames[directions[i].FrameAt(elapsed)]);
                        }
                    }
                    else DrawFrame(canvas, sequence.frames[frame]);
                    Sprite sprite = sequence.frames[frame];
                    EditorGUILayout.LabelField($"{frame + 1} / {sequence.frames.Length}  ·  {sprite.name}  ·  {sprite.rect.width}×{sprite.rect.height}px  ·  {sequence.Duration:0.###}초 / 루프", EditorStyles.miniLabel);
                    stripScroll = EditorGUILayout.BeginScrollView(stripScroll, GUILayout.Height(96));
                    using (new EditorGUILayout.HorizontalScope())
                        for (int i = 0; i < sequence.frames.Length; i++)
                        {
                            Rect thumbnail = GUILayoutUtility.GetRect(72, 72, GUILayout.Width(72));
                            EditorGUI.DrawRect(thumbnail, i == frame ? new Color(.3f, .5f, .65f) : background);
                            DrawFrame(new Rect(thumbnail.x + 4, thumbnail.y + 4, 64, 64), sequence.frames[i], false);
                            GUI.Label(new Rect(thumbnail.x + 2, thumbnail.y + 1, 22, 16), (i + 1).ToString(), EditorStyles.whiteMiniLabel);
                            if (Event.current.type == EventType.MouseDown && thumbnail.Contains(Event.current.mousePosition))
                            { Scrub(i); Event.current.Use(); }
                        }
                    EditorGUILayout.EndScrollView();
                    if (!string.IsNullOrEmpty(sequence.note)) EditorGUILayout.HelpBox(sequence.note, MessageType.Info);
                    EditorGUILayout.LabelField("십자: 스프라이트 기준점 · 확대는 Point 필터 · 설정과 씬을 변경하지 않습니다.", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawFrame(Rect canvas, Sprite sprite, bool guides = true)
        {
            if (Event.current.type != EventType.Repaint || sprite == null) return;
            Rect source = sprite.textureRect;
            float scale = Mathf.Max(1, Mathf.Floor(Mathf.Min(zoom, Mathf.Min((canvas.width - 12) / source.width, (canvas.height - 40) / source.height))));
            Rect destination = new Rect(Mathf.Round(canvas.center.x - source.width * scale * .5f),
                Mathf.Round(canvas.center.y - source.height * scale * .5f), source.width * scale, source.height * scale);
            Rect uv = new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height,
                source.width / sprite.texture.width, source.height / sprite.texture.height);
            if (flip) { uv.x += uv.width; uv.width = -uv.width; }
            GUI.DrawTextureWithTexCoords(destination, sprite.texture, uv, true);
            if (grid && guides && scale >= 4)
            {
                Color line = new Color(1, 1, 1, .12f);
                for (int x = 0; x <= source.width; x++) EditorGUI.DrawRect(new Rect(destination.x + x * scale, destination.y, 1, destination.height), line);
                for (int y = 0; y <= source.height; y++) EditorGUI.DrawRect(new Rect(destination.x, destination.y + y * scale, destination.width, 1), line);
            }
            if (guides)
            {
                Vector2 pivot = new Vector2(destination.x + (flip ? source.width - sprite.pivot.x : sprite.pivot.x) * scale,
                    destination.yMax - sprite.pivot.y * scale);
                EditorGUI.DrawRect(new Rect(pivot.x - 4, pivot.y, 9, 1), new Color(.6f, .8f, 1, .65f));
                EditorGUI.DrawRect(new Rect(pivot.x, pivot.y - 4, 1, 9), new Color(.6f, .8f, 1, .65f));
            }
        }
    }
}
