using System;
using UnityEngine;

namespace ProjectDM
{
    [CreateAssetMenu(menuName = "Project DM/Character Visuals", fileName = "CharacterVisuals")]
    public sealed class CharacterVisuals : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string characterId;
            public Sprite idleSprite;
            public RuntimeAnimatorController controller;
        }

        public Entry[] characters = Array.Empty<Entry>();

        public Entry Find(string characterId) => Array.Find(characters, entry => entry != null && entry.characterId == characterId);
    }
}
