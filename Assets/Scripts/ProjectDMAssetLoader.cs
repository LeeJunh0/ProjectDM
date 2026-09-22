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
    }

    /// <summary>Loads the game's bootstrap content once and releases its handles with the game root.</summary>
    public sealed class ProjectDMAssetLoader : MonoBehaviour
    {
        private const string CatalogAddress = "project-dm/catalog";
        private readonly List<AsyncOperationHandle> handles = new();

        public ProjectDMRuntimeAssets Assets { get; } = new();
        public string Error { get; private set; }
        public bool IsReady => string.IsNullOrEmpty(Error) && Assets.PlayerSheet != null;

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
