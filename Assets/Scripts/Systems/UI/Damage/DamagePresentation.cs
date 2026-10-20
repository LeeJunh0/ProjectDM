using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns damage-number lifetime and rendering independently of damage calculation.</summary>
    internal sealed class DamagePresentation
    {
        private readonly GameSettings settings;

        internal DamagePresentation(GameSettings settings)
        {
            this.settings = settings;
        }

        internal readonly CombatDamageDisplay combatDamageDisplay = new();

        internal void ShowCombatDamage(Vector3 position, float damage, CombatDamageKind kind)
        {
            if (settings.showCombatDamage) combatDamageDisplay.Show(position, damage, kind, settings.combatDamageMaximum);
        }

        internal void ShowElementalDamage(Vector3 position, float damage, CombatDamageKind kind, CombatDamageElement element)
        {
            if (settings.showCombatDamage) combatDamageDisplay.ShowElemental(position, damage, kind, settings.combatDamageMaximum, element);
        }

        internal void DrawCombatDamage()
        {
            if (!settings.showCombatDamage) return;
            combatDamageDisplay.Draw(settings.gameplayCamera, settings.combatDamageDuration, settings.combatDamageHeight, settings.combatDamageScatter,
                settings.combatDamageRise, settings.combatDamageFontSize, settings.combatCriticalScale, settings.combatAttackColor, settings.combatReceivedColor, settings.combatCriticalColor,
                settings.combatDamageStartScale, settings.combatDamagePeakScale, settings.combatDamageEndScale, settings.combatDamageGrowDuration,
                settings.combatDamagePeakHoldDuration, settings.combatDamageShrinkDuration, settings.combatDamageScaleSpeed);
        }

        internal void Reset() { combatDamageDisplay.Clear(); }
        internal void Tick(float dt) { combatDamageDisplay.Tick(dt, settings.combatDamageDuration); }
    }
}
