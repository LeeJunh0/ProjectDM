using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ProjectDM
{
    [CreateAssetMenu(menuName = "Project DM/Asset Catalog", fileName = "ProjectDMAssetCatalog")]
    public sealed class ProjectDMAssetCatalog : ScriptableObject
    {
        [Header("Sprite sheets")]
        public AssetReferenceTexture2D playerSheet;
        public AssetReferenceTexture2D monsterSheet;
        public AssetReferenceTexture2D extraMonsterSheet;
        public AssetReferenceTexture2D gameplaySheet;
        public AssetReferenceTexture2D floorSheet;

        [Header("Animation controllers")]
        public AssetReference playerAnimatorController;
        public AssetReference slimeAnimatorController;
        public AssetReference skeletonAnimatorController;
        public AssetReference boltAnimatorController;
    }
}
