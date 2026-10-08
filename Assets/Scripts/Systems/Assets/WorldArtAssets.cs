using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Loads gameplay sprites and animator references, retaining existing atlas slices.</summary>
    internal sealed class WorldArtAssets
    {
        internal WorldArtAssets() { }
        internal StageDefinition stage;
        internal string StageName => stage != null ? stage.displayName : "프로토타입";
        internal string MonsterName(int slot, PrototypeBalance balance) => stage != null ? stage.monsters[slot].displayName : balance.monsters[slot].name;
        internal RuntimeAnimatorController MonsterController(MonsterKind kind) => stage != null ? stage.monsters[(int)kind].animatorController
            : kind switch { MonsterKind.Slime => slimeController, MonsterKind.Skeleton => skeletonController, _ => null };
        // An Animator and the manual frame player must never both write the same renderer.
        internal Sprite MonsterAlternate(MonsterKind kind) => MonsterController(kind) != null ? null : stage != null ? stage.monsters[(int)kind].frames[1]
            : kind switch { MonsterKind.Goblin => goblinFrame2, MonsterKind.Mushroom => mushroomFrame2, MonsterKind.Boar => boarFrame2, _ => null };

        internal Sprite playerSprite;

        internal Sprite slimeSprite;

        internal Sprite skeletonSprite;

        internal Sprite goblinSprite;

        internal Sprite goblinFrame2;

        internal Sprite mushroomSprite;

        internal Sprite mushroomFrame2;

        internal Sprite boarSprite;

        internal Sprite boarFrame2;

        internal Sprite gemSprite;

        internal Sprite coinSprite;

        internal Sprite boltSprite;
        internal SkillVisualAssets skillVisualAssets;

        internal RuntimeAnimatorController slimeController;

        internal RuntimeAnimatorController skeletonController;

        internal RuntimeAnimatorController boltController;

        internal RuntimeAnimatorController playerAnimatorController;

        internal ProjectDMRuntimeAssets runtimeAssets;

        internal void LoadSprites(ProjectDMRuntimeAssets assets)
        {
            skillVisualAssets = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals");
            ProjectDMSpriteCatalog spriteCatalog = assets.SpriteCatalog;
            if (spriteCatalog != null)
            {
                playerSprite = FirstFrame(spriteCatalog.playerDownFrames);
                slimeSprite = FirstFrame(spriteCatalog.slimeFrames);
                skeletonSprite = FirstFrame(spriteCatalog.skeletonFrames);
            }
            if (assets.GameplaySheet != null)
            {
                gemSprite = Slice(assets.GameplaySheet, .385f, .025f, .080f, .180f, 64f);
                // First coin only: measured in the 1774 x 887 source atlas, excluding the next frame.
                coinSprite = Slice(assets.GameplaySheet, 960f / 1774f, 58f / 887f, 82f / 1774f, 88f / 887f, 64f);
                boltSprite = Slice(assets.GameplaySheet, .020f, .025f, .100f, .180f, 64f);
            }

            playerAnimatorController = assets.PlayerAnimatorController;
            slimeController = assets.SlimeAnimatorController;
            skeletonController = assets.SkeletonAnimatorController;
            boltController = assets.BoltAnimatorController;
            Texture2D extras = assets.ExtraMonsterSheet;
            if (extras != null)
            {
                goblinSprite = Slice(extras, 0f, 2f / 3f, .5f, 1f / 3f, 96f); goblinFrame2 = Slice(extras, .5f, 2f / 3f, .5f, 1f / 3f, 96f);
                mushroomSprite = Slice(extras, 0f, 1f / 3f, .5f, 1f / 3f, 96f); mushroomFrame2 = Slice(extras, .5f, 1f / 3f, .5f, 1f / 3f, 96f);
                boarSprite = Slice(extras, 0f, 0f, .5f, 1f / 3f, 96f); boarFrame2 = Slice(extras, .5f, 0f, .5f, 1f / 3f, 96f);
            }

            playerSprite ??= PixelSprite(new Color(0.45f, 0.18f, 0.8f), new Color(0.05f, 0.9f, 1f));
            slimeSprite ??= PixelSprite(new Color(0.18f, 0.9f, 0.22f), new Color(0.7f, 1f, 0.15f));
            skeletonSprite ??= PixelSprite(new Color(0.75f, 0.70f, 0.62f), new Color(0.22f, 0.12f, 0.14f));
            gemSprite ??= PixelSprite(new Color(0.08f, 0.55f, 1f), Color.white);
            coinSprite ??= PixelSprite(new Color(1f, 0.68f, 0.08f), Color.white);
            boltSprite ??= PixelSprite(new Color(0.85f, 0.2f, 1f), Color.white);
        }

        internal static Sprite Slice(Texture2D sheet, float x, float y, float width, float height, float pixelsPerUnit = 32f)
        {
            return Sprite.Create(
                sheet,
                new Rect(sheet.width * x, sheet.height * y, sheet.width * width, sheet.height * height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        internal static Sprite FirstFrame(Sprite[] frames)
        {
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        internal static Sprite PixelSprite(Color fill, Color core)
        {
            const int size = 16;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
                    texture.SetPixel(x, y, distance < 7.3f ? (distance < 3.5f ? core : fill) : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        internal void CreateBackdrop()
        {
            for (int i = 0; i < 80; i++)
            {
                GameObject star = new("Ambient pixel");
                star.transform.position = new Vector3(Random.Range(-10f, 10f), Random.Range(-6f, 6f), 3f);
                SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelSprite(new Color(0.12f, 0.06f, 0.25f), new Color(0.18f, 0.08f, 0.32f));
                renderer.color = new Color(1f, 1f, 1f, Random.Range(0.15f, 0.55f));
                renderer.sortingOrder = -5;
                star.transform.localScale = Vector3.one * Random.Range(0.018f, 0.05f);
            }
        }

        internal Sprite MonsterSprite(MonsterKind kind) => stage != null ? stage.monsters[(int)kind].frames[0] : kind switch
        {
            MonsterKind.Skeleton => skeletonSprite, MonsterKind.Goblin => goblinSprite,
            MonsterKind.Mushroom => mushroomSprite, MonsterKind.Boar => boarSprite, _ => slimeSprite
        };

        internal Sprite PrototypeIcon(string id) => id switch
        {
            "coin" => coinSprite, "gem" => gemSprite, "slime" => slimeSprite, _ => boltSprite
        };
    }
}
