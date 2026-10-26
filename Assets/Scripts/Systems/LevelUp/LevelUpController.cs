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
        private readonly Dictionary<string, int> offeredRankGains = new();

        internal float Luck => PlayerStats.Calculate(tables.balance, "luck", progression.PermanentAmount("luck"));
        internal UpgradeGradeRules.Chances GradeChances => UpgradeGradeRules.ForLuck(Luck);

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
            int gain = offeredRankGains.TryGetValue(upgrade, out int offeredGain) ? offeredGain : 1;
            int maximum = run.characters == null ? tables.SkillRow(upgrade).maxRank :
                Array.Find(run.characters.Current.upgrades, row => row.Key(run.characters.Current.id) == upgrade).maxRank;
            skills.runSkillRanks[upgrade] = Mathf.Min(maximum, skills.RunRank(upgrade) + gain);
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
            offeredRankGains.Clear();
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
                string grade = UpgradeGradeRules.Roll(Luck, Random.value);
                int gain = Mathf.Min(UpgradeGradeRules.RankGain(grade), row.maxRank - skills.RunRank(row.id));
                offeredRankGains[row.id] = gain;
                runUpgradeChoices[i] = new RunUpgradePresentation(row.id, row.icon, GradeName(grade),
                    row.name + $"  {skills.RunRank(row.id) + gain}단계", DescribePrototypeSkill(row, gain), GradeColor(grade));
            }
        }

        internal string DescribePrototypeSkill(PrototypeSkill row, int gain = 1)
        {
            if (row.id == "damage" || row.id == "speed") return $"이번 런 {row.name}\n+{row.step * gain * 100:0.##}% (누적 {(skills.RunRank(row.id) + gain) * row.step * 100:0.##}%)";
            if (row.id == "proc") return $"획득한 공격 효과의 발동 확률\n+{row.step * gain * 100:0.##}%p";
            double chance = PrototypeRules.Chance(row.chance + Mathf.Max(0, skills.RunRank(row.id) + gain - 1) * row.rankChanceStep,
                progression.PermanentAmount(row.id + "Chance"), skills.RunRank("proc") * tables.SkillRow("proc").step);
            float ratio = row.damageRatio + Mathf.Max(0, skills.RunRank(row.id) + gain - 1) * row.rankDamageStep;
            string scope = row.id == "chain" ? $"최대 {row.targets}명 / 연결 거리 {row.range:0.##}" : $"반경 {row.radius:0.##}";
            string delay = row.id == "meteor" ? $" / {row.delay:0.##}초 후" : "";
            return $"적중마다 {chance * 100:0.##}% 확률\n공격력 {ratio * 100:0.##}% / {scope}{delay}\n효과 피해는 재발동하지 않음";
        }

        private void BuildPersonalChoices()
        {
            CharacterDefinition character = run.characters.Current;
            List<CharacterUpgrade> candidates = new();
            foreach (CharacterUpgrade row in character.upgrades)
                if (!row.unlock && skills.IsSkillUnlocked(row.skill) && skills.RunRank(row.Key(character.id)) < row.maxRank &&
                    (row.effectiveCap<=0 || skills.PersonalAmount(row.id)<row.effectiveCap-.00001f) &&
                    !(character.id == "frost" && row.id == "chill" && skills.PersonalAmount("chill") >= character.chillThreshold - 1)) candidates.Add(row);
            List<RunUpgradePresentation> choices = new();
            while (choices.Count < 3 && candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                CharacterUpgrade row = candidates[index]; candidates.RemoveAt(index);
                string key = row.Key(character.id);
                string grade = UpgradeGradeRules.Roll(Luck, Random.value);
                int gain = Mathf.Min(UpgradeGradeRules.RankGain(grade), row.maxRank - skills.RunRank(key));
                if(row.effectiveCap>0)gain=Mathf.Min(gain,Mathf.CeilToInt((row.effectiveCap-skills.PersonalAmount(row.id))/row.step));
                if (character.id == "frost" && row.id == "chill")
                    gain = Mathf.Min(gain, Mathf.CeilToInt((character.chillThreshold - 1 - skills.PersonalAmount("chill")) / row.step));
                offeredRankGains[key] = gain;
                choices.Add(new RunUpgradePresentation(key, row.icon, GradeName(grade),
                    row.name + $"  {skills.RunRank(key) + gain}단계", DescribePersonalUpgrade(character, row, gain), GradeColor(grade)));
            }
            runUpgradeChoices = choices.ToArray();
        }

        private static string GradeName(string grade) => grade == "gold" ? "금" : grade == "silver" ? "은" : "동";
        private static Color GradeColor(string grade) => grade == "gold" ? new Color(.96f,.72f,.23f) :
            grade == "silver" ? new Color(.72f,.76f,.81f) : new Color(.72f,.42f,.20f);

        private string DescribePersonalUpgrade(CharacterDefinition character, CharacterUpgrade row, int gain)
        {
            float amount = row.step * gain;
            if(row.effectiveCap>0)amount=Mathf.Min(amount,row.effectiveCap-skills.PersonalAmount(row.id));
            string effect = row.id switch
            {
                "damage" => $"피해 +{amount * 100:0.##}%",
                "speed" => $"시전 속도 +{amount * 100:0.##}%",
                "count" => character.id == "projectile" ? $"시전당 투사체 +{amount:0.##}개 · 추가 탄 피해 70%" : $"공전 조각 +{amount:0.##}개 · 시전 비용은 그대로",
                "pierce" => $"관통 대상 +{amount:0.##}명 · 동일 대상 중복 타격 없음",
                "homing" => $"유도 회전 속도 +{amount:0.##}도/초",
                "jumps" => $"연쇄 대상 +{amount:0.##}명 · 연쇄 피해 50%",
                "range" => $"{row.name} +{amount:0.##}유닛",
                "echo" => $"50% 피해 재타격 확률 +{amount * 100:0.##}%p · 재귀 발동 없음",
                "width" => $"부채꼴 전체 각도 +{amount:0.##}도",
                "burn" => $"2초간 초당 화염 피해 +{amount * 100:0.##}%",
                "orbitSpeed" => $"얼음 회전 속도 +{amount:0.##}도/초",
                "size" => $"얼음 조각의 시각·타격 반경 +{amount:0.##}유닛",
                "chill" => $"타격당 냉기 +{amount:0.##}중첩 · {character.chillThreshold}중첩에서 빙결",
                "freezeDuration" => $"빙결 지속시간 +{amount:0.##}초",
                _ => $"{row.name} +{(row.unit == "%" || row.unit == "%p" ? amount * 100 : amount):0.##}{row.unit}"
            };
            return $"{character.name} · 이번 런 전용\n{effect}\n+{gain}단계 · {skills.RunRank(row.Key(character.id)) + gain}/{row.maxRank}단계";
        }

        internal Action onOpened;
        internal void Reset()
        {
            level = 1; experience = 0; experienceToNext = PrototypeRules.XpThreshold(tables.balance, level);
            pendingRunUpgrades = 0; choosingUpgrade = false;
            runUpgradeChoices = Array.Empty<RunUpgradePresentation>();
            offeredRankGains.Clear();
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
