using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns persistent progress, migrations, sandbox policy and save flushing.</summary>
    internal sealed class SaveService
    {
        private readonly GameTables tables;
        private readonly IProgressStore store;

        internal SaveService(GameTables tables, IProgressStore store, bool sandbox)
        {
            this.tables = tables;
            this.store = store;
            sandboxThisSession = sandbox;
        }

        // Keep the legacy key so existing incremental progress remains intact.
        private const string MetaCurrencyKey = "PROJECT_DM_META_GOLD";

        internal const string DamageKey = "PROJECT_DM_DAMAGE_LEVEL";

        internal const string HasteKey = "PROJECT_DM_HASTE_LEVEL";

        internal const string FortuneKey = "PROJECT_DM_FORTUNE_LEVEL";

        internal int metaCurrency;

        internal int damageLevel;

        internal int hasteLevel;

        internal int fortuneLevel;

        internal void LoadMetaProgress()
        {
            metaCurrency = store.GetInt(MetaCurrencyKey, 0);
            damageLevel = store.GetInt(DamageKey, 0);
            hasteLevel = store.GetInt(HasteKey, 0);
            fortuneLevel = store.GetInt(FortuneKey, 0);
        }

        internal void SaveMetaProgress()
        {
            MarkPrototypeProgressDirty();
        }

        internal const string PrototypeSaveKey = "PROJECT_DM_PROTOTYPE_V2";

        internal const string PrototypePreCompressionBackupKey = "PROJECT_DM_PROTOTYPE_V2_BEFORE_COMPRESSION";

        internal const string PrototypePreRadialBackupKey = "PROJECT_DM_PROTOTYPE_V2_BEFORE_RADIAL";

        internal PrototypeSave progress;

        internal bool prototypeDirty;

        internal readonly bool sandboxThisSession;

        internal float lastPrototypeSaveAt;

        internal void MarkPrototypeProgressDirty() { prototypeDirty = true; }

        internal void FlushPrototypeProgressIfDue()
        {
            if (prototypeDirty && Time.unscaledTime - lastPrototypeSaveAt >= 1f) FlushPrototypeProgress();
        }

        internal void FlushPrototypeProgress()
        {
            if (progress == null || !prototypeDirty) return;
            if (!sandboxThisSession)
            {
                store.SetString(PrototypeSaveKey, JsonUtility.ToJson(progress));
                store.SetInt(MetaCurrencyKey, metaCurrency);
                // Original rank keys remain untouched and recoverable after migration.
                store.Save();
            }
            prototypeDirty = false;
            lastPrototypeSaveAt = Time.unscaledTime;
        }

        internal void Load()
        {
            try { LoadCore(); }
            catch { progress = null; prototypeDirty = false; throw; }
        }

        private void LoadCore()
        {
            LoadMetaProgress();
            if (store.HasKey(PrototypeSaveKey))
            {
                progress = JsonUtility.FromJson<PrototypeSave>(store.GetString(PrototypeSaveKey));
                if (progress == null || progress.version < 1 || progress.version > 3 || progress.kills == null || progress.kills.Length != 5 || progress.ranks == null)
                    throw new InvalidOperationException("저장 데이터 형식이 맞지 않습니다. 기존 저장은 변경하지 않습니다.");
                Dictionary<string, PrototypeRank> normalizedRanks = new();
                foreach (PrototypeRank entry in progress.ranks)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                    if (double.IsNaN(entry.partialRank) || double.IsInfinity(entry.partialRank) || entry.partialRank < 0 || entry.partialRank >= 1)
                        throw new InvalidOperationException("저장된 성장 잔여 단계가 잘못되었습니다.");
                    if (!normalizedRanks.TryGetValue(entry.id, out PrototypeRank old) ||
                        entry.rank + entry.partialRank > old.rank + old.partialRank) normalizedRanks[entry.id] = entry;
                }
                progress.ranks.Clear();
                foreach (var entry in normalizedRanks) progress.SetRank(entry.Key, entry.Value.rank, entry.Value.partialRank);
                for (int i = 0; i < progress.kills.Length; i++) progress.kills[i] = Mathf.Max(0, progress.kills[i]);
            }
            else
            {
                progress = new PrototypeSave { version = 1 };
                // One-time migration: retain currency and old ranks, but do not double-apply old flat bonuses.
                progress.SetRank("damage", Mathf.Max(0, damageLevel));
                progress.SetRank("speed", Mathf.Max(0, hasteLevel));
                progress.SetRank("yield", Mathf.Max(0, fortuneLevel));
            }
            int sourceVersion = progress.version;
            string oldSnapshot = progress.version < 3 && store.HasKey(PrototypeSaveKey)
                ? store.GetString(PrototypeSaveKey) : null;
            PrototypeRules.MigrateGrowthRanks(progress, tables.growthTable);
            if (oldSnapshot != null && sourceVersion == 1 && !sandboxThisSession && !store.HasKey(PrototypePreCompressionBackupKey))
                store.SetString(PrototypePreCompressionBackupKey, oldSnapshot);
            if (oldSnapshot != null && !sandboxThisSession && !store.HasKey(PrototypePreRadialBackupKey))
                store.SetString(PrototypePreRadialBackupKey, oldSnapshot);
            metaCurrency = Mathf.Max(0, metaCurrency);
            MarkPrototypeProgressDirty();
            FlushPrototypeProgress();

        }
    }
}
