using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns the independent boss clock, mana lifecycle, pause/navigation and result counters.</summary>
    internal sealed class RunController
    {
        private readonly GameSettings settings;
        private readonly SaveService save;
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly PlayerWorld playerWorld;
        private readonly Action resetWorld;
        private readonly Action spawnInitial;
        private readonly Action snapCamera;
        private readonly Action resetNavigation;
        private readonly Action closeSelection;
        private readonly Func<bool> isChoosing;

        internal readonly StageSession stages;
        internal readonly RunMana mana;
        internal readonly CharacterSession characters;

        internal RunController(GameSettings settings, SaveService save, GameTables tables, ProgressionService progression, PlayerWorld playerWorld, Action resetWorld, Action spawnInitial, Action snapCamera, Action resetNavigation, Action closeSelection, Func<bool> isChoosing, RunMana mana, StageSession stages = null)
        {
            this.settings = settings;
            this.save = save;
            this.tables = tables;
            this.progression = progression;
            this.playerWorld = playerWorld;
            this.resetWorld = resetWorld;
            this.spawnInitial = spawnInitial;
            this.snapCamera = snapCamera;
            this.resetNavigation = resetNavigation;
            this.closeSelection = closeSelection;
            this.isChoosing = isChoosing;
            this.stages = stages;
            this.mana = mana;
            characters = tables.characters == null ? null : new CharacterSession(tables.characters, save.progress);
        }

        internal int runCurrency;

        internal bool showMetaTree;

        internal float elapsed;

        internal bool runEnded;

        internal readonly int[] monsterKillCounts = new int[5];
        internal readonly int[] permanentKillsAtRunStart = new int[5];
        internal readonly int[] resultPermanentKillCounts = new int[5];
        internal int resultMonsterStage;

        internal float resultPresentationOpenedAt;

        internal void EndRun()
        {
            if (runEnded) return;
            runEnded = true;
            Array.Copy(save.progress.kills, resultPermanentKillCounts, resultPermanentKillCounts.Length);
            resultMonsterStage = PrototypeRules.Stage(tables.balance, save.progress);
            resultPresentationOpenedAt = Time.unscaledTime;
            showMetaTree = false;
            closeSelection();
            resetNavigation();
            RefreshPauseState();
            save.progress.completedRuns++;
            save.progress.playedSeconds += elapsed;
            save.MarkPrototypeProgressDirty();
            save.FlushPrototypeProgress();
            // Collected currency is already saved by pickupsSystem.Collect. Do not award it again here.
        }

        internal void ToggleMetaTree()
        {
            if (isChoosing()) return;
            showMetaTree = !showMetaTree;
            resetNavigation();
            RefreshPauseState();
        }

        internal void ReturnToGrowthMap()
        {
            showMetaTree = true;
            resetNavigation();
            RefreshPauseState();
        }

        internal void RefreshPauseState()
        {
            Time.timeScale = runEnded || isChoosing() || showMetaTree ? 0f : 1f;
        }

        internal void BeginRun()
        {
            characters?.ApplySelected();
            resetWorld();
            Array.Clear(monsterKillCounts, 0, monsterKillCounts.Length);
            Array.Copy(save.progress.kills, permanentKillsAtRunStart, permanentKillsAtRunStart.Length);
            Array.Copy(save.progress.kills, resultPermanentKillCounts, resultPermanentKillCounts.Length);
            resultMonsterStage = PrototypeRules.Stage(tables.balance, save.progress);
            runCurrency = 0;
            elapsed = 0f;
            // Former health purchases retain their save ID and now increase mana capacity.
            mana.Reset(Mathf.Max(1f, (settings.useTableStartingMana ? tables.balance.ResolvedInitialMana : settings.startingMana)
                + progression.PermanentAmount("time") + progression.PermanentAmount("health")), characters?.Current.manaCost ?? tables.balance.basicAttackManaCost);
            runEnded = showMetaTree = false;
            resetNavigation();
            playerWorld.ResetPosition(characters?.Current.id);
            snapCamera();
            RefreshPauseState();
            spawnInitial();
        }

        internal float TickClock(float deltaTime)
        {
            if (runEnded || showMetaTree || isChoosing()) return 0;
            float dt = Mathf.Min(Mathf.Max(0, deltaTime), Mathf.Max(0, tables.balance.bossElapsedSeconds - elapsed));
            elapsed += dt;
            return dt;
        }
        internal void CompleteIfDue(Action arriveAtBoss)
        {
            if (runEnded) return;
            if (PrototypeRules.BossDue(elapsed, tables.balance.bossElapsedSeconds)) arriveAtBoss();
            else if (mana.IsDepleted) EndRun();
        }
    }
}
