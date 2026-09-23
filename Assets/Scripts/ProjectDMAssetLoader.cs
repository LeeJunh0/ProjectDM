using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectDM
{
    public sealed class ProjectDMRuntimeAssets
    {
        public Texture2D PlayerSheet { get; internal set; }
        public Texture2D MonsterSheet { get; internal set; }
        public Texture2D ExtraMonsterSheet { get; internal set; }
        public Texture2D GameplaySheet { get; internal set; }
        public Texture2D FloorSheet { get; internal set; }
        public RuntimeAnimatorController PlayerAnimatorController { get; internal set; }
        public RuntimeAnimatorController SlimeAnimatorController { get; internal set; }
        public RuntimeAnimatorController SkeletonAnimatorController { get; internal set; }
        public RuntimeAnimatorController BoltAnimatorController { get; internal set; }
        public RuntimeAnimatorController ChestPickupAnimatorController { get; internal set; }
        public GameObject PlayerPrefab { get; internal set; }
        public GameObject EnemyPrefab { get; internal set; }
        public GameObject ProjectilePrefab { get; internal set; }
        public GameObject[] ExperiencePickupPrefabs { get; internal set; }
        public GameObject[] CurrencyPickupPrefabs { get; internal set; }
        public GameObject ChestPickupPrefab { get; internal set; }
        public ProjectDMSpriteCatalog SpriteCatalog { get; internal set; }
    }

    /// <summary>Loads the game's bootstrap content once and releases its handles with the game root.</summary>
    public sealed class ProjectDMAssetLoader : MonoBehaviour
    {
        private const string CatalogAddress = "project-dm/catalog";
        private readonly List<AsyncOperationHandle> handles = new();

        public ProjectDMRuntimeAssets Assets { get; } = new();
        public string Error { get; private set; }
        public bool IsReady => string.IsNullOrEmpty(Error) && Assets.PlayerSheet != null && Assets.SpriteCatalog != null;

        public IEnumerator LoadAsync()
        {
            AsyncOperationHandle<ProjectDMAssetCatalog> catalogHandle = Addressables.LoadAssetAsync<ProjectDMAssetCatalog>(CatalogAddress);
            handles.Add(catalogHandle);
            yield return catalogHandle;
            if (catalogHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Error = $"Could not load the Project DM asset catalog: {catalogHandle.OperationException}";
                yield break;
            }

            ProjectDMAssetCatalog catalog = catalogHandle.Result;
            yield return Load<Texture2D>(catalog.playerSheet, asset => Assets.PlayerSheet = asset);
            yield return Load<Texture2D>(catalog.monsterSheet, asset => Assets.MonsterSheet = asset);
            yield return Load<Texture2D>(catalog.extraMonsterSheet, asset => Assets.ExtraMonsterSheet = asset);
            yield return Load<Texture2D>(catalog.gameplaySheet, asset => Assets.GameplaySheet = asset);
            yield return Load<Texture2D>(catalog.floorSheet, asset => Assets.FloorSheet = asset);
            yield return Load<RuntimeAnimatorController>(catalog.playerAnimatorController, asset => Assets.PlayerAnimatorController = asset);
            yield return Load<RuntimeAnimatorController>(catalog.slimeAnimatorController, asset => Assets.SlimeAnimatorController = asset);
            yield return Load<RuntimeAnimatorController>(catalog.skeletonAnimatorController, asset => Assets.SkeletonAnimatorController = asset);
            yield return Load<RuntimeAnimatorController>(catalog.boltAnimatorController, asset => Assets.BoltAnimatorController = asset);
            yield return LoadOptional<RuntimeAnimatorController>(catalog.chestPickupAnimatorController, asset => Assets.ChestPickupAnimatorController = asset);
            yield return Load<ProjectDMSpriteCatalog>(catalog.spriteCatalog, asset => Assets.SpriteCatalog = asset);
            yield return Load<GameObject>(catalog.playerPrefab, asset => Assets.PlayerPrefab = asset);
            yield return Load<GameObject>(catalog.enemyPrefab, asset => Assets.EnemyPrefab = asset);
            yield return Load<GameObject>(catalog.projectilePrefab, asset => Assets.ProjectilePrefab = asset);
            yield return LoadOptionalCollection<GameObject>(catalog.experiencePickupPrefabs, assets => Assets.ExperiencePickupPrefabs = assets);
            yield return LoadOptionalCollection<GameObject>(catalog.currencyPickupPrefabs, assets => Assets.CurrencyPickupPrefabs = assets);
            yield return Load<GameObject>(catalog.chestPickupPrefab, asset => Assets.ChestPickupPrefab = asset);
        }

        private IEnumerator Load<T>(AssetReference reference, Action<T> assign) where T : UnityEngine.Object
        {
            if (!string.IsNullOrEmpty(Error))
            {
                yield break;
            }

            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Error = $"The bootstrap catalog has an unassigned {typeof(T).Name} reference.";
                yield break;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(reference);
            handles.Add(handle);
            yield return handle;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Error = $"Could not load {typeof(T).Name} '{reference.RuntimeKey}': {handle.OperationException}";
                yield break;
            }

            assign(handle.Result);
        }

        private IEnumerator LoadOptional<T>(AssetReference reference, Action<T> assign) where T : UnityEngine.Object
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                yield break;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(reference);
            handles.Add(handle);
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                assign(handle.Result);
            }
            else
            {
                Debug.LogWarning($"Project DM could not load optional {typeof(T).Name} '{reference.RuntimeKey}': {handle.OperationException}");
            }
        }

        private IEnumerator LoadOptionalCollection<T>(IEnumerable<AssetReference> references, Action<T[]> assign) where T : UnityEngine.Object
        {
            List<T> loadedAssets = new();
            if (references != null)
            {
                foreach (AssetReference reference in references)
                {
                    if (reference == null || !reference.RuntimeKeyIsValid())
                    {
                        continue;
                    }

                    AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(reference);
                    handles.Add(handle);
                    yield return handle;
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        loadedAssets.Add(handle.Result);
                    }
                    else
                    {
                        Debug.LogWarning($"Project DM could not load optional {typeof(T).Name} '{reference.RuntimeKey}': {handle.OperationException}");
                    }
                }
            }

            assign(loadedAssets.ToArray());
        }

        private void OnDestroy()
        {
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                if (handles[i].IsValid())
                {
                    Addressables.Release(handles[i]);
                }
            }

            handles.Clear();
        }
    }
}
