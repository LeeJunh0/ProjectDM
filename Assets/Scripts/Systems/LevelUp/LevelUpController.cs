using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns experience, queued level-ups, available choices and selection application.</summary>
    internal sealed class LevelUpController
    {
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly RunController run;
        private readonly SkillSystem skills;

        internal LevelUpController(GameTables tables, ProgressionService progression, RunController run, SkillSystem skills)
        {
            this.tables = tables;
            this.progression = progression;
            this.run = run;
            this.skills = skills;
        }

        internal int level = 1;

        internal float experience;

        internal int experienceToNext = 7;

        internal bool choosingUpgrade;

        internal float upgradeSelectionOpenedAt;

        internal RunUpgradePresentation[] runUpgradeChoices = System.Array.Empty<RunUpgradePresentation>();

        internal void ChooseRunUpgrade(string upgrade)
        {
            if (run.runEnded || !choosingUpgrade) return;
            bool offered = false;
            foreach (RunUpgradePresentation choice in runUpgradeChoices)
                if (choice.upgrade == upgrade) offered = true;
            if (!offered) return;
            skills.runSkillRanks[upgrade] = skills.RunRank(upgrade) + 1;
            pendingRunUpgrades = Mathf.Max(0, pendingRunUpgrades - 1);
            choosingUpgrade = false;
            if (pendingRunUpgrades > 0) OpenRunUpgradeSelection();
            run.RefreshPauseState();
        }

        internal void OpenRunUpgradeSelection()
        {
            if (run.runEnded) return;
            BuildPrototypeRunChoices();
            if (runUpgradeChoices.Length == 0) { pendingRunUpgrades = 0; return; }
            choosingUpgrade = true;
            upgradeSelectionOpenedAt = Time.unscaledTime;
            onOpened?.Invoke();
            run.RefreshPauseState();
        }

        internal void TriggerDebugLevelUp()
        {
            if (choosingUpgrade || run.runEnded || run.showMetaTree)
            {
                return;
            }

            level++;
            experienceToNext = PrototypeRules.XpThreshold(tables.balance, level);
            pendingRunUpgrades++;
            OpenRunUpgradeSelection();
        }

        internal void HandleRunUpgradeShortcuts()
        {
            for (int i = 0; i < runUpgradeChoices.Length; i++)
            {
                KeyCode alphaKey = (KeyCode)((int)KeyCode.Alpha1 + i);
                KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad1 + i);
                if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
                {
                    ChooseRunUpgrade(runUpgradeChoices[i].upgrade);
                    return;
                }
            }
        }

        internal int pendingRunUpgrades;

        internal void BuildPrototypeRunChoices()
        {
            if (run.characters != null) { BuildPersonalChoices(); return; }
            List<PrototypeSkill> candidates = new();
            foreach (PrototypeSkill row in tables.skillTable.rows)
            {
                if (skills.RunRank(row.id) >= row.maxRank) continue;
                if (!string.IsNullOrEmpty(row.unlock) && progression.ActiveRank(row.unlock) < 1) continue;
                if (row.id == "proc" && skills.RunRank("chain") + skills.RunRank("splash") + skills.RunRank("meteor") == 0) continue;
                candidates.Add(row);
            }
            // Draw without replacement. When an effect is unlocked but unacquired, guarantee one skill offer.
            List<PrototypeSkill> offered = new();
            List<PrototypeSkill> acquisitions = candidates.FindAll(row => !string.IsNullOrEmpty(row.unlock) && skills.RunRank(row.id) == 0);
            if (acquisitions.Count > 0)
            {
                PrototypeSkill skill = acquisitions[Random.Range(0, acquisitions.Count)];
                offered.Add(skill); candidates.Remove(skill);
            }
            while (offered.Count < 3 && candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                offered.Add(candidates[index]); candidates.RemoveAt(index);
            }
            runUpgradeChoices = new RunUpgradePresentation[offered.Count];
            for (int i = 0; i < offered.Count; i++)
            {
                PrototypeSkill row = offered[i];
                string category = row.rarity == "gold" ? "금" : row.rarity == "silver" ? "은" : "동";
                Color color = row.rarity == "gold" ? new Color(.96f, .72f, .23f) : row.rarity == "silver" ? new Color(.72f, .76f, .81f) : new Color(.72f, .42f, .20f);
                runUpgradeChoices[i] = new RunUpgradePresentation(row.id, row.icon, category,
                    row.name + $"  {skills.RunRank(row.id) + 1}단계", DescribePrototypeSkill(row), color);
            }
        }

        internal string DescribePrototypeSkill(PrototypeSkill row)
        {
            if (row.id == "damage" || row.id == "speed") return $"이번 런 {row.name}\n+{row.step * 100:0.##}% (누적 {(skills.RunRank(row.id) + 1) * row.step * 100:0.##}%)";
            if (row.id == "proc") return $"획득한 공격 효과의 발동 확률\n+{row.step * 100:0.##}%p";
            double chance = PrototypeRules.Chance(row.chance + skills.RunRank(row.id) * row.rankChanceStep,
                progression.PermanentAmount(row.id + "Chance"), skills.RunRank("proc") * tables.SkillRow("proc").step);
            float ratio = row.damageRatio + skills.RunRank(row.id) * row.rankDamageStep;
            string scope = row.id == "chain" ? $"최대 {row.targets}명 / 연결 거리 {row.range:0.##}" : $"반경 {row.radius:0.##}";
            string delay = row.id == "meteor" ? $" / {row.delay:0.##}초 후" : "";
            return $"적중마다 {chance * 100:0.##}% 확률\n공격력 {ratio * 100:0.##}% / {scope}{delay}\n효과 피해는 재발동하지 않음";
        }

        private void BuildPersonalChoices()
        {
            CharacterDefinition character = run.characters.Current;
            List<CharacterUpgrade> candidates = new();
            foreach (CharacterUpgrade row in character.upgrades)
                if (skills.RunRank(row.Key(character.id)) < row.maxRank &&
                    !(character.id == "frost" && row.id == "chill" && skills.PersonalAmount("chill") >= character.chillThreshold - 1)) candidates.Add(row);
            List<RunUpgradePresentation> choices = new();
            while (choices.Count < 3 && candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                CharacterUpgrade row = candidates[index]; candidates.RemoveAt(index);
                string key = row.Key(character.id);
                Color color = row.rarity == "gold" ? new Color(.96f, .72f, .23f) : row.rarity == "silver" ? new Color(.72f, .76f, .81f) : new Color(.72f, .42f, .2f);
                choices.Add(new RunUpgradePresentation(key, row.icon, row.rarity == "gold" ? "금" : row.rarity == "silver" ? "은" : "동",
                    row.name + $"  {skills.RunRank(key) + 1}단계", character.name + " · 이번 런 전용\n" + row.description, color));
            }
            runUpgradeChoices = choices.ToArray();
        }

        internal Action onOpened;
        internal void Reset()
        {
            level = 1; experience = 0; experienceToNext = PrototypeRules.XpThreshold(tables.balance, level);
            pendingRunUpgrades = 0; choosingUpgrade = false;
            runUpgradeChoices = Array.Empty<RunUpgradePresentation>();
        }
        internal void AddExperience(float amount)
        {
            experience += amount;
            while (experience >= experienceToNext)
            {
                experience -= experienceToNext;
                level++;
                experienceToNext = PrototypeRules.XpThreshold(tables.balance, level);
                pendingRunUpgrades++;
            }
            if (pendingRunUpgrades > 0 && !choosingUpgrade) OpenRunUpgradeSelection();
        }
    }
}
