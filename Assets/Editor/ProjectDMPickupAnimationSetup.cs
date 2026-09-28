using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Installs static pickup variants with a lightweight floating visual.</summary>
    public static class ProjectDMPickupAnimationSetup
    {
        [MenuItem("Project DM/Install Floating Pickup Content")]
        public static void Install()
        {
            ProjectDMArtPipeline.ImportPickupAnimations();
            ProjectDMArtPipeline.ImportCollectibleVariantAnimations();
            ProjectDMPrefabFactory.CreateOrUpdatePrefabs();
            ProjectDMAddressablesMigration.ConfigurePickupContent();
            Debug.Log("Project DM floating pickup content is installed.");
        }
    }
}
