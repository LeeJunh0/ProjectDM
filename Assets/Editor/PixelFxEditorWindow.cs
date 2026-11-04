using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    [Serializable] internal sealed class PixelFxProfile
    {
        public string id;
        public int seed = 17;
        public float light = 1, density = 1, width = 1, spread = 1, speed = 1, size = 1, tail = 1, starRadius = 1;
        public string[] palette;
    }
    [Serializable] internal sealed class PixelFxEditorSettings { public PixelFxProfile[] profiles; }

    internal sealed class PixelFxEditorWindow : EditorWindow
    {
        [Serializable] private sealed class Preview
        {
            public int width, height, fps, clipped;
            public float duration;
            public bool loop;
            public PixelFxLibrary.FrameRect[] frames;
        }
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string Work => Path.Combine(Root, "work/pixelfx");
        private static string SettingsFile => Path.Combine(Root, "Design/Skills/PixelFX-v1/tuning.json");
        private PixelFxEditorSettings settings;
        private Preview preview;
        private Texture2D sheet, meadow, dungeon;
        private int selected, revision, renderingRevision;
        private Vector2 scroll;
        private bool playing = true, dirty, applying, autoPreview = true;
        private float zoom = 4, playback = 1, time;
        private int background = 1;
        private double lastTime, renderAt = double.PositiveInfinity, nextWatch;
        private DateTime sourceStamp;
        private Process process;
        private Task<string> outputTask, errorTask;
        private string outputDirectory, status = "효과를 준비하고 있어요.";
        private static readonly string[] Backgrounds = { "어두운 배경", "초원", "석재 던전", "밝은 배경" };
        private static readonly string[] PaletteNames = { "가장 어두운 색", "어두운 색", "중간색", "하이라이트", "가장 밝은 색" };

        [MenuItem("Project DM/픽셀 이펙트 편집기")]
        internal static void Open() => GetWindow<PixelFxEditorWindow>("픽셀 이펙트 편집기");

        private void OnEnable()
        {
            minSize = new Vector2(800, 560);
            lastTime = EditorApplication.timeSinceStartup;
            try
            {
                settings = JsonUtility.FromJson<PixelFxEditorSettings>(File.ReadAllText(SettingsFile));
                if (settings?.profiles == null || settings.profiles.Length == 0) throw new InvalidDataException("이펙트 설정이 비어 있습니다.");
                meadow = LoadImage(Path.Combine(Root, "Docs/QA/2026-11-03-meadow-sharp.png"));
                dungeon = LoadImage(Path.Combine(Root, "Docs/QA/2026-11-03-dungeon-sharp.png"));
                QueuePreview();
            }
            catch (Exception e) { status = e.Message; }
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        private static Texture2D LoadImage(string file)
        {
            if (!File.Exists(file)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            if (!texture.LoadImage(File.ReadAllBytes(file))) { DestroyImmediate(texture); throw new InvalidDataException("미리보기 PNG를 읽지 못했습니다."); }
            return texture;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            // Preview writes only to Temp. An in-progress apply is allowed to finish writing its files.
            if (process != null && !process.HasExited && !applying) process.Kill();
            if (process != null && process.HasExited) process.Dispose();
            DestroyImmediate(sheet); DestroyImmediate(meadow); DestroyImmediate(dungeon);
        }
        private void QueuePreview() { revision++; renderAt = EditorApplication.timeSinceStartup + .25; }
        private void Changed() { dirty = true; if (autoPreview) QueuePreview(); }
        private void Save()
        {
            File.WriteAllText(SettingsFile, JsonUtility.ToJson(settings, true) + "\n", new System.Text.UTF8Encoding(false));
            dirty = false; status = "설정을 저장했어요. 게임 리소스는 ‘게임에 적용’으로 갱신하세요.";
        }
        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
        private void StartRender(bool apply)
        {
            outputDirectory = Path.Combine(Root, "Temp/PixelFXEditor", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);
            string snapshot = Path.Combine(outputDirectory, "tuning.json");
            File.WriteAllText(snapshot, JsonUtility.ToJson(settings, true), new System.Text.UTF8Encoding(false));
            var info = new ProcessStartInfo
            {
                FileName = EditorPrefs.GetString("ProjectDM.PixelFx.Node", "node"),
                WorkingDirectory = Work, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                Arguments = Quote(Path.Combine(Work, "tools/projectdm-edit.mjs")) + (apply ? " apply " : " preview ") +
                    Quote(settings.profiles[selected].id) + " " + Quote(snapshot) + " " + Quote(outputDirectory)
            };
            process = Process.Start(info);
            outputTask = process.StandardOutput.ReadToEndAsync(); errorTask = process.StandardError.ReadToEndAsync();
            applying = apply; renderingRevision = revision;
            status = apply ? "게임 리소스와 브라우저 미리보기를 내보내고 있어요…" : "미리보기를 생성하고 있어요…";
            renderAt = double.PositiveInfinity;
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing && preview != null) time = (time + (float)(now - lastTime) * playback) % (preview.duration + (preview.loop ? 0 : .4f));
            lastTime = now;
            try
            {
                if (process != null && process.HasExited && outputTask.IsCompleted && errorTask.IsCompleted)
                {
                    int code = process.ExitCode;
                    string result = outputTask.GetAwaiter().GetResult(), error = errorTask.GetAwaiter().GetResult();
                    process.Dispose(); process = null;
                    if (code != 0) status = "생성 실패: " + (string.IsNullOrWhiteSpace(error) ? result : error);
                    else if (applying) { status = "게임에 적용했어요. 다음 재생부터 새 이펙트를 확인할 수 있어요."; AssetDatabase.Refresh(); QueuePreview(); }
                    else if (renderingRevision == revision)
                    {
                        var next = JsonUtility.FromJson<Preview>(File.ReadAllText(Path.Combine(outputDirectory, "preview.json")));
                        var texture = LoadImage(Path.Combine(outputDirectory, "preview.png"));
                        DestroyImmediate(sheet); sheet = texture; preview = next; time = 0;
                        status = preview.clipped > 0 ? "프레임 밖으로 나가는 픽셀이 있어요. 크기를 줄인 후 적용하세요." : "미리보기 갱신 완료";
                    }
                    applying = false;
                }
                if (settings != null && process == null && now >= renderAt) StartRender(false);
                if (settings != null && now > nextWatch)
                {
                    nextWatch = now + .5;
                    DateTime stamp = File.GetLastWriteTimeUtc(Path.Combine(Work, "src/fx/projectdm/_shared.js"));
                    if (sourceStamp != default && stamp != sourceStamp && autoPreview) QueuePreview();
                    sourceStamp = stamp;
                }
            }
            catch (Exception e) { status = "생성 오류: " + e.Message; process = null; applying = false; renderAt = double.PositiveInfinity; }
            Repaint();
        }
        private static string Name(string id) => id switch
        {
            "missile" => "마력탄", "beam" => "마력 광선", "satellite" => "마력 위성", "cone" => "화염 부채꼴", "firefield" => "화염 지대",
            "meteor" => "화염 유성", "chain" => "연쇄 번개", "orb" => "번개 구체", "thunder" => "낙뢰", "frostshard" => "빙결 공전 조각",
            "blizzard" => "빙결 폭풍", "shatter" => "빙결 파쇄", "hit" => "명중 파편", "fireimpact" => "유성 충돌", "mote" => "획득·구매 반짝임", _ => id
        };
        private void OnGUI()
        {
            if (settings?.profiles == null) { EditorGUILayout.HelpBox(status, MessageType.Error); if (GUILayout.Button("다시 읽기")) OnEnable(); return; }
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int selection = EditorGUILayout.Popup(selected, settings.profiles.Select(p => Name(p.id)).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(190));
            if (selection != selected) { selected = selection; time = 0; preview = null; QueuePreview(); }
            playing = GUILayout.Toggle(playing, playing ? "일시정지" : "재생", EditorStyles.toolbarButton, GUILayout.Width(65));
            autoPreview = GUILayout.Toggle(autoPreview, "자동 미리보기", EditorStyles.toolbarButton, GUILayout.Width(105));
            if (GUILayout.Button("다시 생성", EditorStyles.toolbarButton)) QueuePreview();
            GUILayout.FlexibleSpace(); GUILayout.Label(dirty ? "저장하지 않은 설정" : "저장됨", EditorStyles.miniLabel); EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(300));
            var p = settings.profiles[selected];
            EditorGUILayout.LabelField("이펙트 조절", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            p.size = EditorGUILayout.Slider("전체 크기", p.size, .5f, 1.3f);
            p.speed = EditorGUILayout.Slider("움직임 속도", p.speed, .5f, 2);
            p.light = EditorGUILayout.Slider("잔광 밝기", p.light, 0, 2);
            if (p.id != "mote" && p.id != "thunder") p.density = EditorGUILayout.Slider("입자량", p.density, .25f, 2);
            if (p.id == "cone" || p.id == "beam") p.width = EditorGUILayout.Slider("불꽃·광선 굵기", p.width, .5f, 1.5f);
            if (p.id == "cone") p.spread = EditorGUILayout.Slider("불꽃 퍼짐", p.spread, .5f, 1.4f);
            if (p.id == "missile") p.tail = EditorGUILayout.Slider("짧은 잔광 길이", p.tail, 0, 1.8f);
            if (p.id == "blizzard") p.starRadius = EditorGUILayout.Slider("큰 별의 바깥 거리", p.starRadius, .6f, 1.4f);
            p.seed = Mathf.Clamp(EditorGUILayout.IntField("입자 배치 시드", p.seed), 0, 1000000);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("색상 · 어두운 색부터", EditorStyles.boldLabel);
            for (int i = 0; i < p.palette.Length; i++)
            {
                ColorUtility.TryParseHtmlString(p.palette[i], out Color color);
                Color next = EditorGUILayout.ColorField(i < PaletteNames.Length ? PaletteNames[i] : "색상 " + (i + 1), color);
                p.palette[i] = "#" + ColorUtility.ToHtmlStringRGB(next);
            }
            if (EditorGUI.EndChangeCheck()) Changed();
            EditorGUILayout.Space();
            if (GUILayout.Button("설정 저장")) Save();
            using (new EditorGUI.DisabledScope(process != null || (preview != null && preview.clipped > 0)))
                if (GUILayout.Button("게임에 적용", GUILayout.Height(32))) { Save(); StartRender(true); }
            if (GUILayout.Button("선택 효과를 초기값으로"))
            {
                var defaults = JsonUtility.FromJson<PixelFxEditorSettings>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(SettingsFile), "tuning-defaults.json")));
                settings.profiles[selected] = defaults.profiles.First(value => value.id == p.id); Changed();
            }
            if (GUILayout.Button("효과 모양 코드 열기"))
            {
                string file = Path.Combine(Work, "src/fx/projectdm/_shared.js");
                int line = Array.FindIndex(File.ReadAllLines(file), text => text.Contains("id==='" + p.id + "'"));
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(file, Mathf.Max(1, line + 1));
            }
            if (GUILayout.Button("Node 실행 파일 지정"))
            {
                string node = EditorUtility.OpenFilePanel("Node 실행 파일", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                if (!string.IsNullOrEmpty(node)) EditorPrefs.SetString("ProjectDM.PixelFx.Node", node);
            }
            EditorGUILayout.HelpBox("수치·색 변경은 미리보기에만 반영돼요. ‘게임에 적용’으로 스프라이트 시트를 갱신하세요. 공격 범위와 피해 수치는 바뀌지 않아요.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginVertical();
            background = EditorGUILayout.Popup("배경", background, Backgrounds);
            zoom = EditorGUILayout.Slider("확대", zoom, 1, 8);
            playback = EditorGUILayout.Slider("미리보기 배속", playback, .1f, 2);
            Rect area = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPreview(area);
            if (preview != null)
            {
                EditorGUI.BeginChangeCheck(); float next = EditorGUILayout.Slider("프레임 확인", Mathf.Min(time, preview.duration), 0, preview.duration);
                if (EditorGUI.EndChangeCheck()) { time = next; playing = false; }
                EditorGUILayout.LabelField(preview.frames.Length + "프레임 · " + preview.fps + "fps · " + preview.duration.ToString("0.00") + "초");
            }
            EditorGUILayout.HelpBox(status, status.StartsWith("생성 실패") || status.StartsWith("생성 오류") ? MessageType.Error : MessageType.None);
            EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal();
        }
        private void DrawPreview(Rect area)
        {
            EditorGUI.DrawRect(area, background == 3 ? new Color(.75f, .75f, .75f) : new Color(.08f, .09f, .13f));
            Texture2D bg = background == 1 ? meadow : background == 2 ? dungeon : null;
            if (bg != null) GUI.DrawTextureWithTexCoords(area, bg, new Rect(.16f, .3f, .4f, .4f));
            if (sheet == null || preview?.frames == null) return;
            int index = Mathf.Clamp(Mathf.FloorToInt(time * preview.fps), 0, preview.frames.Length - 1);
            var frame = preview.frames[index];
            float scale = Mathf.Min(zoom, area.width / frame.width, area.height / frame.height);
            Rect image = new Rect(area.center - new Vector2(frame.width, frame.height) * scale * .5f, new Vector2(frame.width, frame.height) * scale);
            GUI.DrawTextureWithTexCoords(image, sheet, new Rect((float)frame.x / sheet.width, 1f - (float)(frame.y + frame.height) / sheet.height, (float)frame.width / sheet.width, (float)frame.height / sheet.height));
        }
    }
}
