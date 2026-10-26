using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectDM.Editor
{
    internal static class GrowthTreeLayout
    {
        const float StepX = 210f, StepY = 160f;

        // Each arm is a branching route of compact grids. Only display data changes.
        internal static void Arrange(GrowthTreeDefinition tree)
        {
            string[][] routes = {
                new[] { "damage", "speed", "criticalChance", "criticalDamage" },
                new[] { "time", "extension", "health", "invulnerability" },
                new[] { "yield", "silverDropChance", "goldDropChance", "emeraldDropChance", "amethystDropChance" },
                new[] { "xp", "experienceGradeChance", "magnet", "movement", "population", "respawn", "luck", "populationCap", "spawnBatch", "magnetDropChance" }
            };
            var arms = Enumerable.Range(0, 4).Select(_ => new List<GrowthTreeNodeSettings[]>()).ToArray();
            CharacterDefinition character = null;
            if (tree.id != "common")
            {
                var data = Resources.Load<TextAsset>("ProjectDM/Data/PrototypeCharacters");
                if (data != null) character = Array.Find(JsonUtility.FromJson<CharacterTable>(data.text).characters,c=>c.id==tree.id);
            }
            if(character?.skills != null)
            {
                for(int skill=0;skill<character.skills.Length;skill++)
                {
                    string id=character.skills[skill].id;
                    var members=tree.nodes.Where(n=>n.enabled && Array.Find(character.upgrades,u=>u.Key(character.id)==n.growthId)?.skill==id).ToArray();
                    int columns=Mathf.CeilToInt(Mathf.Sqrt(members.Length)),sx=skill==2?-1:1,sy=skill==0?-1:1;
                    for(int n=0;n<members.Length;n++)
                    {
                        int row=n/columns,col=n%columns;if(row%2==1)col=columns-1-col;
                        var node=members[n];node.x=sx*(1+col)*StepX;node.y=sy*(1+row)*StepY;
                        node.displayParent=n==0?"ROOT":members[n-1].id;node.displayOrthogonal=false;
                        node.displayLinks=members.Take(n).Where(other=>other.id!=node.displayParent &&
                            ((other.x==node.x && Mathf.Abs(other.y-node.y)==StepY)||(other.y==node.y && Mathf.Abs(other.x-node.x)==StepX))).Select(other=>other.id).ToArray();
                    }
                }
                return;
            }
            int fallback = 0;
            foreach (var group in tree.nodes.Where(n => n.enabled).GroupBy(n => n.growthId)
                .OrderBy(g => { int arm = Array.FindIndex(routes, r => r.Contains(g.Key)); return arm < 0 ? 100 : arm * 10 + Array.IndexOf(routes[arm], g.Key); }))
            {
                int arm = Array.FindIndex(routes, r => r.Contains(group.Key));
                if(character?.skills != null)
                {
                    var upgrade=Array.Find(character.upgrades,u=>u.Key(character.id)==group.Key);
                    if(upgrade != null) arm=Array.FindIndex(character.skills,s=>s.id==upgrade.skill);
                }
                arms[arm < 0 ? fallback++ % 4 : arm].Add(group.ToArray());
            }
            for (int arm = 0; arm < arms.Length; arm++)
            {
                var groups = arms[arm];
                if (groups.Count == 0) continue;
                int sx = arm < 2 ? 1 : -1, sy = arm == 0 || arm == 3 ? -1 : 1;
                var columns = groups.Select(g => Mathf.CeilToInt(Mathf.Sqrt(g.Length))).ToArray();
                var rows = groups.Select((g, i) => (g.Length + columns[i] - 1) / columns[i]).ToArray();
                var spans = new int[groups.Count];
                for (int i = groups.Count - 1; i >= 0; i--)
                {
                    int first = i * 2 + 1, second = first + 1;
                    int children = first < groups.Count ? spans[first] : 0;
                    if (second < groups.Count) children += 2 + spans[second];
                    spans[i] = Math.Max(rows[i], children);
                }

                void Place(int at, int x, int y, GrowthTreeNodeSettings[] parent)
                {
                    var nodes = groups[at];
                    int top = parent == null ? y : y + (spans[at] - rows[at]) / 2;
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        int row = i / columns[at], column = i % columns[at];
                        if (row % 2 != 0) column = columns[at] - 1 - column;
                        nodes[i].x = sx * (x + column) * StepX;
                        nodes[i].y = sy * (top + row) * StepY;
                        nodes[i].displayParent = i == 0 ? "ROOT" : nodes[i - 1].id;
                        nodes[i].displayOrthogonal = false;
                        nodes[i].displayLinks = Array.Empty<string>();
                    }
                    if (parent != null)
                    {
                        float edge = parent.Max(n => sx * n.x);
                        var exit = parent.Where(n => sx * n.x == edge).OrderBy(n => Mathf.Abs(n.y - nodes[0].y)).First();
                        nodes[0].displayParent = exit.id;
                        nodes[0].displayOrthogonal = true;
                    }
                    // The snake gives every node one visual parent. Extra edges close the grid cells.
                    for (int i = 0; i < nodes.Length; i++)
                        nodes[i].displayLinks = nodes.Take(i).Where(n => n.id != nodes[i].displayParent &&
                            ((n.x == nodes[i].x && Mathf.Abs(n.y - nodes[i].y) == StepY) ||
                             (n.y == nodes[i].y && Mathf.Abs(n.x - nodes[i].x) == StepX))).Select(n => n.id).ToArray();
                    int child = at * 2 + 1, nextX = x + columns[at] + 2;
                    if (child < groups.Count) Place(child, nextX, y, nodes);
                    if (child + 1 < groups.Count) Place(child + 1, nextX, y + spans[child] + 2, nodes);
                }
                Place(0, 1, 1, null);
            }
        }
    }
}
