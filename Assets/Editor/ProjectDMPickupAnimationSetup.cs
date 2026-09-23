using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Installs the animated pickup content without requiring manual importer setup.</summary>
    public static class ProjectDMPickupAnimationSetup
    {
        [MenuItem("Project DM/Install Animated Pickup Content")]
        public static void Install()
        {
            ProjectDMArtPipeline.ImportPickupAnimations();
            ProjectDMPrefabFactory.CreateOrUpdatePrefabs();
            ProjectDMAddressablesMigration.Configure();
            Debug.Log("Project DM animated pickup content is installed.");
        }
    }
}
