// Copy to an isolated project's Assets/Editor; execute ProjectDM.Editor.EnemySpecialAttacksRegression.Run.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class EnemySpecialAttacksRegression
    {
        private static int checks;
        private static readonly Bounds bounds = new(Vector3.zero, Vector3.one * 40);
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        private static object Read(object obj, string field) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static Enemy Make(MonsterAttackKind kind, Sprite sprite, Material material)
        {
            var root = new GameObject("attack test monster"); var visual = new GameObject("original sprite"); visual.transform.SetParent(root.transform);
            var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
            return new Enemy { transform = root.transform, renderer = renderer, baseSprite = sprite, hitPoints = 100,
                specialAttack = kind, kind = kind == MonsterAttackKind.Spore ? MonsterKind.Mushroom : MonsterKind.Boar };
        }
        public static void Run()
        {
            try
            {
                var tables = GameTables.LoadResources(new GameSettings());
                var meadow = Resources.Load<StageDefinition>("ProjectDM/Stages/Stage1_Meadow");
                var dungeon = Resources.Load<StageDefinition>("ProjectDM/Stages/Stage2_Dungeon");
                Check(meadow.monsters[3].attack == MonsterAttackKind.Spore && meadow.monsters[4].attack == MonsterAttackKind.Charge, "Only mushroom and boar attack in meadow");
                Check(dungeon.monsters[2].attack == MonsterAttackKind.ChainStrike && dungeon.monsters[4].attack == MonsterAttackKind.ArmorSweep, "Only ghost and armor attack in dungeon");
                int count = 0; foreach (var m in meadow.monsters) if (m.attack != 0) count++; foreach (var m in dungeon.monsters) if (m.attack != 0) count++;
                Check(count == 4, "Other six monster types must keep normal behavior");
                Vector2 tracked = EnemySpecialAttacks.Track(Vector2.right, Vector2.up, .1f);
                Check(Mathf.Abs(Vector2.Angle(Vector2.right, tracked) - 12) < .001f, "Aim tracks at 120 degrees per second");
                Check(EnemySpecialAttacks.SegmentHits(Vector2.zero, Vector2.right * 4, Vector2.right * 2, .2f), "Swept hit cannot tunnel");
                Check(!EnemySpecialAttacks.SegmentHits(Vector2.zero, Vector2.right * 4, new Vector2(2, 1), .2f), "Outside path is safe");
                var material = new Material(Shader.Find("Sprites/Default"));
                foreach (var kind in new[] { MonsterAttackKind.Spore, MonsterAttackKind.ArmorSweep, MonsterAttackKind.ChainStrike, MonsterAttackKind.Charge })
                {
                    var e = Make(kind, meadow.monsters[3].frames[0], material); var enemies = new List<Enemy> { e };
                    using (var attacks = new EnemySpecialAttacks())
                    {
                        int hits = 0; attacks.HitPlayer = _ => hits++;
                        Vector2 target = Vector2.right * (kind == MonsterAttackKind.ArmorSweep ? .8f : 1.5f);
                        attacks.Tick(enemies, target, .01f, tables.balance, bounds);
                        Check(e.specialAttacking && attacks.ActiveCount == 1 && hits == 0, kind + ": entering range starts a windup without damage");
                        float windup = EnemySpecialAttacks.Windup(kind);
                        attacks.Tick(enemies, target, windup - .1f, tables.balance, bounds);
                        var active = (IList)Read(attacks, "active"); object attack = active[0];
                        Vector2 locked = (Vector2)Read(attack, "direction");
                        var indicator = (LineRenderer)Read(attack, "indicator");
                        if (kind != MonsterAttackKind.Spore) Check(indicator.startColor.r > .9f && indicator.startColor.g < .3f, kind + ": red before attack");
                        attacks.Tick(enemies, Vector2.up * target.magnitude, .05f, tables.balance, bounds);
                        Check((Vector2)Read(attack, "direction") == locked, kind + ": red warning locks aim");
                        Check(hits == 0, kind + ": never hits before windup");
                        attacks.Tick(enemies, target, .06f, tables.balance, bounds);
                        Check(e.renderer.sprite == e.baseSprite, kind + ": original artwork retained");
                        if (kind == MonsterAttackKind.Spore)
                        {
                            Check(attacks.SporeCount == 1 && hits == 0, "Spore starts as a projectile rather than instant hit");
                            var shot = ((IList)Read(attacks, "spores"))[0]; var circle = (LineRenderer)Read(shot, "indicator");
                            Vector2 center = (Vector2)Read(shot, "position");
                            Check(Mathf.Abs(Vector2.Distance(circle.GetPosition(0), center) - EnemySpecialAttacks.SporeRadius) < .001f, "Ground circle matches spore hit radius");
                            attacks.Tick(enemies, target, .9f, tables.balance, bounds);
                            Check(hits == 1 && attacks.SporeCount == 0, "Slow spore hits once and disappears with its circle");
                        }
                        else if (kind == MonsterAttackKind.Charge)
                        {
                            attacks.Tick(enemies, target, .3f, tables.balance, bounds);
                            Check(hits == 1 && e.transform.position.x > 1, "Boar moves and swept collision hits once");
                        }
                        else Check(hits == 1, kind + ": one area strike at windup completion");
                        attacks.Tick(enemies, new Vector2(15,15), 2, tables.balance, bounds);
                        Check(!e.specialAttacking && e.renderer.transform.localScale == Vector3.one && e.renderer.transform.localPosition == Vector3.zero, kind + ": recovery restores body transforms");
                        Check(hits == 1, kind + ": recovery cannot deal repeated hits");
                        attacks.Reset(); Check(attacks.ActiveCount == 0 && attacks.SporeCount == 0, "Reset clears attacks and projectiles");
                    }
                    UnityEngine.Object.DestroyImmediate(e.transform.gameObject);
                }
                using (var attacks = new EnemySpecialAttacks())
                {
                    var e = Make(MonsterAttackKind.Spore, meadow.monsters[3].frames[0], material);
                    var enemies = new List<Enemy> { e };
                    attacks.Tick(enemies, Vector2.right * 10, .1f, tables.balance, bounds);
                    Check(attacks.ActiveCount == 0, "Spore only starts within reachable distance");
                    attacks.Tick(enemies, Vector2.right, .1f, tables.balance, bounds);
                    attacks.Tick(enemies, Vector2.right, .61f, tables.balance, bounds);
                    Check(attacks.SporeCount == 1, "Spore emitted after windup");
                    attacks.Tick(enemies, Vector2.up * 10, 1.21f, tables.balance, bounds);
                    Check(attacks.SporeCount == 0, "Missed spore expires after 1.2 seconds");
                    attacks.Reset(); e.attackCooldown = 0;
                    attacks.Tick(enemies, Vector2.right, .1f, tables.balance, bounds);
                    e.dead = true; attacks.Remove(e);
                    Check(attacks.ActiveCount == 0 && attacks.SporeCount == 0, "Death during mushroom preparation cancels emission");
                    attacks.Reset(); UnityEngine.Object.DestroyImmediate(e.transform.gameObject);
                }
                using (var attacks = new EnemySpecialAttacks())
                {
                    var enemies = new List<Enemy>(); for (int i = 0; i < 10; i++) enemies.Add(Make(MonsterAttackKind.ChainStrike, dungeon.monsters[2].frames[0], material));
                    attacks.Tick(enemies, Vector2.right, .1f, tables.balance, bounds);
                    Check(attacks.ActiveCount == 2, "Crowds allow at most two simultaneous special attacks");
                    var e = enemies[0]; e.freezeRemaining = 2;
                    var active = (IList)Read(attacks, "active"); object a = active[0]; float age = (float)Read(a, "age");
                    attacks.Tick(enemies, Vector2.right, .3f, tables.balance, bounds);
                    Check((float)Read(a,"age") == age, "Freeze pauses special attack timing");
                    e.dead = true; attacks.Remove(e);
                    Check(!e.specialAttacking, "Dying during windup cancels attack before pool return");
                    attacks.Reset(); foreach (var enemy in enemies) UnityEngine.Object.DestroyImmediate(enemy.transform.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(material);
                Debug.Log("ENEMY SPECIAL ATTACKS PASSED: " + checks + " assertions"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
