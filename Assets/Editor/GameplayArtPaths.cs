namespace ProjectDM.Editor
{
    /// <summary>Source locations shared by native export and the resource preview.</summary>
    internal static class GameplayArtPaths
    {
        internal const string Characters = "Assets/GameContent/Art/Characters";
        internal const string CharacterAnimations = "Assets/GameContent/Animation/Characters";
        internal const string CharacterCatalog = "Assets/Resources/ProjectDM/Characters/CharacterVisuals.asset";
        internal const string Monsters = "Assets/GameContent/Art/Monsters";
        internal const string Prefabs = "Assets/GameContent/Prefabs";
        internal static readonly string[] CharacterNames = { "Missile", "Flame", "Lightning", "Frost" };
        internal static readonly string[] Biomes = { "Meadow", "Dungeon" };
        internal static string MonsterWalkSheet(string biome) => $"{Monsters}/ProjectDM_{biome}_Walk_4Frame_BlackEyes_v5.png";
        internal static string MonsterBossSheet(string biome) => biome == "Dungeon"
            ? $"{Monsters}/ProjectDM_DungeonDenizens_Retro_2Frame_v1.png"
            : $"{Monsters}/ProjectDM_Meadow_Retro_2Frame_v1.png";
    }
}
