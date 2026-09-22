using UnityEngine;

namespace ProjectDM
{
    /// <summary>Named runtime view of manually sliced character and monster frames.</summary>
    [CreateAssetMenu(menuName = "Project DM/Sprite Catalog", fileName = "ProjectDMSpriteCatalog")]
    public sealed class ProjectDMSpriteCatalog : ScriptableObject
    {
        public Sprite[] playerLeftFrames;
        public Sprite[] playerDownFrames;
        public Sprite[] playerRightFrames;
        public Sprite[] playerUpFrames;
        public Sprite[] slimeFrames;
        public Sprite[] skeletonFrames;
    }
}
