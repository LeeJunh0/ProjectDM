using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns boss-arrival state and the existing non-combat preview.</summary>
    internal sealed class BossMilestone
    {
        private readonly SaveService save;
        private readonly WorldArtAssets art;
        private readonly PlayerWorld playerWorld;
        private readonly RunController run;

        internal BossMilestone(SaveService save, WorldArtAssets art, PlayerWorld playerWorld, RunController run)
        {
            this.save = save;
            this.art = art;
            this.playerWorld = playerWorld;
            this.run = run;
        }

        internal bool bossArrived;

        internal bool showBossStatistics;

        internal bool bossArrivalIsDebug;

        internal Transform prototypeBoss;

        internal void ArriveAtPrototypeBoss(bool debugPreview = false)
        {
            if (bossArrived || run.runEnded) return;
            bossArrived = true;
            bossArrivalIsDebug = debugPreview;
            if (!debugPreview) save.progress.reachedBoss = true;
            if (prototypeBoss == null)
            {
                GameObject instance = new GameObject("최종보스 도달 미리보기 (전투 미구현)");
                prototypeBoss = instance.transform;
                SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 8;
            }
            SpriteRenderer bossRenderer = prototypeBoss.GetComponent<SpriteRenderer>();
            bossRenderer.sprite = art.stage != null ? art.stage.bossFrames[0] : art.boarSprite ?? art.slimeSprite;
            bossRenderer.color = art.stage != null ? Color.white : new Color(1f, .55f, .35f);
            prototypeBoss.name = (art.stage != null ? art.stage.bossName : "보스") + " 도달 미리보기 (전투 미구현)";
            prototypeBoss.gameObject.SetActive(true);
            prototypeBoss.position = playerWorld.player.position + Vector3.up * 2f;
            prototypeBoss.localScale = Vector3.one * (art.stage != null ? art.stage.bossScale : 1.8f);
            run.EndRun(); // Arrival milestone only; boss combat is outside the current prototype.
        }

        internal void Reset()
        {
            bossArrived = showBossStatistics = bossArrivalIsDebug = false;
            if (prototypeBoss != null) prototypeBoss.gameObject.SetActive(false);
        }
    }
}
