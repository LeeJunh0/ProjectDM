using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Displays the boss-arrival milestone and its navigation controls.</summary>
    internal sealed class BossArrivalView
    {
        private readonly GameTables tables;
        private readonly RunController run;
        private readonly BossMilestone boss;
        private readonly UiGraphics graphics;

        internal BossArrivalView(GameTables tables, RunController run, BossMilestone boss, UiGraphics graphics)
        {
            this.tables = tables;
            this.run = run;
            this.boss = boss;
            this.graphics = graphics;
        }

        internal void DrawPrototypeBossArrival()
        {
            UiGraphics.DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(.015f, .02f, .035f, .45f));
            GUIStyle heading = new GUIStyle(graphics.titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 32, normal = { textColor = new Color(1f, .76f, .38f) } };
            GUIStyle body = new GUIStyle(graphics.statStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 14, wordWrap = true, normal = { textColor = Color.white } };
            float y = Screen.height * .6f;
            GUI.Label(new Rect(20, y, Screen.width - 40, 48), boss.bossArrivalIsDebug ? "최종보스 도달 (미리보기)" : "최종보스 도달", heading);
            GUI.Label(new Rect(20, y + 52, Screen.width - 40, 56),
                $"실제 런 진행 시간 {run.elapsed:0.00}초 / 리미트 {tables.balance.bossElapsedSeconds:0.##}초\n현재는 도달 확인용입니다. 보스 전투·난이도는 아직 적용하지 않았습니다.", body);
            float width = Mathf.Min(180, (Screen.width - 64) / 3f);
            float x = (Screen.width - width * 3 - 20) * .5f;
            if (GUI.Button(new Rect(x, y + 118, width, 36), "성장 지도")) run.ReturnToGrowthMap();
            if (GUI.Button(new Rect(x + width + 10, y + 118, width, 36), "통계 확인")) boss.showBossStatistics = true;
            if (GUI.Button(new Rect(x + (width + 10) * 2, y + 118, width, 36), "다시 시작")) run.BeginRun();
        }
    }
}
