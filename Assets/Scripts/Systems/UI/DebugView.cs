using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Displays prototype debug controls without changing the production update path.</summary>
    internal sealed class DebugView
    {
        private readonly SaveService save;
        private readonly GameTables tables;
        private readonly RunController run;
        private readonly EnemySystem enemiesSystem;
        private readonly CombatSystem combat;
        private readonly SkillSystem skills;
        private readonly BossMilestone boss;
        private readonly GameNotifications notifications;
        private readonly PlayerCombatSystem playerCombat;
        private readonly UiGraphics graphics;

        internal DebugView(SaveService save, GameTables tables, RunController run, EnemySystem enemiesSystem, CombatSystem combat, SkillSystem skills, BossMilestone boss, GameNotifications notifications, PlayerCombatSystem playerCombat, UiGraphics graphics)
        {
            this.save = save;
            this.tables = tables;
            this.run = run;
            this.enemiesSystem = enemiesSystem;
            this.combat = combat;
            this.skills = skills;
            this.boss = boss;
            this.notifications = notifications;
            this.playerCombat = playerCombat;
            this.graphics = graphics;
        }

        internal bool debugPanelOpen;

        internal void DrawPrototypeNotices()
        {
            int stage = enemiesSystem.SpawnStage;
            GUIStyle style = new GUIStyle(graphics.statStyle) { fontSize = 12, normal = { textColor = new Color(.9f, .8f, .6f) } };
            string unlock = stage < 4 ? $"{tables.balance.monsters[stage].name} {save.progress.kills[stage]}/{tables.balance.monsters[stage].nextUnlockKills}" : "일반 몬스터 모두 해금";
            GUI.Label(new Rect(22, 60, Screen.width - 245, 25), $"{stage + 1}단계 · {unlock} · 보스까지 {(tables.balance.bossElapsedSeconds - run.elapsed):0.0}초", style);
            if (skills.RunRank("chain") + skills.RunRank("splash") + skills.RunRank("meteor") > 0)
            {
                string effects = "";
                foreach (string id in new[] { "chain", "splash", "meteor" })
                    if (skills.RunRank(id) > 0) effects += $"{tables.SkillRow(id).name} {(skills.forceAttackEffects ? 100 : skills.AttackEffectChance(tables.SkillRow(id)) * 100):0.##}%  ";
                GUI.Label(new Rect(22, 85, Screen.width - 245, 25), effects, style);
            }
            if (Time.unscaledTime < notifications.noticeUntil)
                GUI.Label(new Rect(22, 112, Screen.width - 245, 36), notifications.prototypeNotice, new GUIStyle(style) { fontSize = 15, wordWrap = true });
        }

        internal void DrawPrototypeDebugTools()
        {
            if (GUI.Button(new Rect(Screen.width - 190, 100, 165, 27), debugPanelOpen ? "디버그 도구 닫기" : "디버그 도구 열기")) debugPanelOpen = !debugPanelOpen;
            if (!debugPanelOpen) return;
            Rect panel = new Rect(Screen.width - 250, 133, 225, 260);
            UiGraphics.DrawSolidRect(panel, new Color(.025f, .03f, .05f, .94f));
            GUI.Label(new Rect(panel.x + 10, panel.y + 6, 205, 24), save.sandboxThisSession ? "실험 모드 · 저장 안 됨" : "일반 모드 · 재화/처치 저장됨", graphics.statStyle);
            if (GUI.Button(new Rect(panel.x + 10, panel.y + 34, 205, 28), "재화 +1,000"))
            {
                save.metaCurrency = (int)System.Math.Min(int.MaxValue, (long)save.metaCurrency + 1000);
                save.MarkPrototypeProgressDirty(); save.FlushPrototypeProgress();
            }
            if (GUI.Button(new Rect(panel.x + 10, panel.y + 67, 205, 28), "공격 효과 3종 획득 (이번 런)"))
            {
                foreach (string id in new[] { "chain", "splash", "meteor" }) skills.runSkillRanks[id] = Mathf.Max(1, skills.RunRank(id));
            }
            skills.forceAttackEffects = GUI.Toggle(new Rect(panel.x + 10, panel.y + 101, 205, 24), skills.forceAttackEffects, "공격 효과 100% (이번 런)");
            if (GUI.Button(new Rect(panel.x + 10, panel.y + 127, 205, 28), $"스폰 단계 변경 · 현재 {enemiesSystem.SpawnStage + 1}"))
                enemiesSystem.debugStageOverride = (enemiesSystem.SpawnStage + 1) % 5;
            if (GUI.Button(new Rect(panel.x + 10, panel.y + 162, 205, 28), "최종보스 도달 미리보기")) boss.ArriveAtPrototypeBoss(true);
            if (GUI.Button(new Rect(panel.x + 10, panel.y + 195, 205, 28), "플레이어 피격 테스트 (-10)")) playerCombat.ApplyPlayerDamage(10);
            combat.forceCriticalHits = GUI.Toggle(new Rect(panel.x + 10, panel.y + 229, 205, 24), combat.forceCriticalHits, "치명타 100% (이번 런)");
        }
    }
}
