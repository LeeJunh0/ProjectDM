using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    public static class CharacterArtRegression
    {
        private static int checks;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        [MenuItem("Project DM/검증/후드 캐릭터 아트 검증")]
        public static void RunMenu()
        {
            checks = 0;
            CharacterVisuals catalog = Resources.Load<CharacterVisuals>("ProjectDM/Characters/CharacterVisuals");
            Check(catalog != null && catalog.characters.Length == 4, "Four available visual identities");
            Check(catalog.characters.Select(entry => entry.characterId).Distinct().Count() == 4, "Unique character IDs");
            foreach (string id in ProjectDMCharacterArtSetup.Ids)
            {
                CharacterVisuals.Entry entry = catalog.Find(id);
                Check(entry?.idleSprite != null && entry.controller != null, "Complete visual binding: " + id);
                string path = AssetDatabase.GetAssetPath(Pixel32ArtSetup.Original(entry.idleSprite));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.alphaIsTransparency &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed, "Crisp transparent sprite import: " + id);
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Check(sprites.Length == 16, "All directional walking frames are sliced: " + id);
                foreach (Sprite sprite in sprites)
                {
                    Check(sprite.rect.xMin >= 0 && sprite.rect.yMin >= 0 && sprite.rect.xMax <= sprite.texture.width &&
                        sprite.rect.yMax <= sprite.texture.height, "Frame within source: " + sprite.name);
                    Check(sprite.bounds.size.y > 2.5f && sprite.bounds.size.y < 3.2f, "Consistent body scale: " + sprite.name);
                }
                var controller = (AnimatorController)entry.controller;
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                Check(machine.defaultState.name == "Idle_Down", "Front idle default: " + id);
                foreach (string direction in ProjectDMCharacterArtSetup.Directions)
                {
                    AnimationClip clip = (AnimationClip)machine.states.Single(state => state.state.name == "Walk_" + direction).state.motion;
                    ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip,
                        EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                    Check(keys.Length == 4 && keys.Select(key => key.value).Distinct().Count() == 4,
                        "Four distinct walking frames: " + id + direction);
                    Check(keys.Take(4).All(key => key.value is Sprite frame && AssetDatabase.GetAssetPath(Pixel32ArtSetup.Original(frame)) == path && key.value.name.Contains("_" + direction + "_")),
                        "Correct identity and direction in animation: " + id + direction);
                    Check(AnimationUtility.GetAnimationClipSettings(clip).loopTime && Mathf.Approximately(clip.length, .5f),
                        "8fps looping walk cadence: " + id + direction);
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/Player.prefab");
            var assets = new ProjectDMRuntimeAssets { PlayerPrefab = prefab };
            var art = new WorldArtAssets { characterVisuals = catalog, runtimeAssets = assets,
                playerSprite = catalog.Find("projectile").idleSprite, playerAnimatorController = catalog.Find("projectile").controller };
            var world = new PlayerWorld(new GameSettings(), null, art, null);
            try
            {
                GameObject avatar = Object.Instantiate(prefab);
                world.player = avatar.transform;
                world.playerController = avatar.GetComponent<Player>();
                // Edit-mode fixtures explicitly run Awake; Main smoke covers normal prefab creation.
                RuntimeTestAccess.Call(avatar.GetComponent<PlayerAnimation>(), "Awake");
                RuntimeTestAccess.Call(world.playerController, "Awake");
                Animator animator = world.player.GetComponentInChildren<Animator>();
                SpriteRenderer renderer = world.player.GetComponentInChildren<SpriteRenderer>();
                foreach (string id in ProjectDMCharacterArtSetup.Ids.Concat(new[] { "projectile" }))
                {
                    world.ResetPosition(id);
                    animator.Update(0);
                    Check(animator.runtimeAnimatorController == catalog.Find(id).controller && renderer.sprite == catalog.Find(id).idleSprite,
                        "Real prefab uses selected identity after restart: " + id);
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle_Down"), "Restart always starts front idle: " + id);
                    foreach (var pair in new[] { (Vector2.left, "Left"), (Vector2.up, "Up"), (Vector2.right, "Right"), (Vector2.down, "Down") })
                    {
                        world.player.GetComponent<PlayerAnimation>().UpdateFacing(pair.Item1);
                        animator.Update(.13f);
                        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk_" + pair.Item2) &&
                            renderer.sprite.name.Contains("_" + pair.Item2 + "_"), "Actual animator faces requested direction: " + id + pair.Item2);
                    }
                    world.player.GetComponent<PlayerAnimation>().UpdateFacing(Vector2.zero);
                    animator.Update(0);
                    Check(renderer.sprite == catalog.Find(id).idleSprite, "Stopping returns to approved front pose: " + id);
                }
                art.characterVisuals = null;
                world.ResetPosition("missing"); animator.Update(0);
                Check(renderer.sprite == art.playerSprite, "Missing visual catalog retains fallback rendering");
            }
            finally { if (world.player != null) Object.DestroyImmediate(world.player.gameObject); }
            Debug.Log($"Character art: {checks} assertions PASS (four hooded identities, 64 frames, directional animation and prefab restart).");
        }

        // A real SpriteRenderer contact sheet, on two backgrounds to expose alpha fringes.
        public static void Render()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch-only isolated render.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CharacterVisuals catalog = Resources.Load<CharacterVisuals>("ProjectDM/Characters/CharacterVisuals");
            GameObject root = new GameObject("Character art render");
            Camera camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            for (int column = 0; column < 4; column++)
            {
                CharacterVisuals.Entry entry = catalog.Find(ProjectDMCharacterArtSetup.Ids[column]);
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(Pixel32ArtSetup.Original(entry.idleSprite))).OfType<Sprite>().Select(Pixel32ArtSetup.Resolve).ToArray();
                string prefix = entry.idleSprite.name.Replace("_Down_0", "");
                for (int row = 0; row < 3; row++)
                {
                    string direction = new[] { "Down", "Left", "Up" }[row];
                    GameObject actor = new GameObject(entry.characterId + direction, typeof(SpriteRenderer));
                    actor.transform.SetParent(root.transform, false);
                    actor.transform.position = new Vector3((column - 1.5f) * 4, (1 - row) * 3.35f, 0);
                    actor.GetComponent<SpriteRenderer>().sprite = frames.Single(sprite => sprite.name == $"{prefix}_{direction}_0");
                }
            }
            Directory.CreateDirectory("Logs");
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (bool light in new[] { false, true })
                {
                    camera.backgroundColor = light ? new Color(.66f, .68f, .61f) : new Color(.12f, .17f, .14f);
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    File.WriteAllBytes($"Logs/HoodedCharacters-{(light ? "light" : "dark")}.png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(root);
            }
        }
    }
}
