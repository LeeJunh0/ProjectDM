using System;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Native 32px runtime sprites. Source identities are strings, so archived art is not loaded with this catalog.</summary>
    public sealed class Pixel32Catalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key, sourceGuid;
            public long sourceFileId;
            public Sprite sprite;
            public float worldSize;
        }
        public Entry[] entries = Array.Empty<Entry>();
        public Sprite Find(string key) => Array.Find(entries, entry => entry.key == key)?.sprite;
    }
}
