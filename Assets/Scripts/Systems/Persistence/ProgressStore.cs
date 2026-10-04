using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    internal interface IProgressStore
    {
        bool HasKey(string key);
        int GetInt(string key, int fallback = 0);
        string GetString(string key);
        void SetInt(string key, int value);
        void SetString(string key, string value);
        void Save();
    }

    internal sealed class PlayerPrefsProgressStore : IProgressStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback = 0) => PlayerPrefs.GetInt(key, fallback);
        public string GetString(string key) => PlayerPrefs.GetString(key);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    // Used by isolated regression fixtures. Never touches PlayerPrefs.
    internal sealed class MemoryProgressStore : IProgressStore
    {
        internal readonly Dictionary<string, int> integers = new();
        internal readonly Dictionary<string, string> strings = new();
        internal int writes, saves;
        public bool HasKey(string key) => integers.ContainsKey(key) || strings.ContainsKey(key);
        public int GetInt(string key, int fallback = 0) => integers.TryGetValue(key, out int value) ? value : fallback;
        public string GetString(string key) => strings.TryGetValue(key, out string value) ? value : "";
        public void SetInt(string key, int value) { integers[key] = value; writes++; }
        public void SetString(string key, string value) { strings[key] = value; writes++; }
        public void Save() { saves++; }
    }
}
