// Run ProjectDM.Editor.HomingArcRegression.Run in an isolated Unity project.
using System;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class HomingArcRegression
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        public static void Run()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("Homing arc regression");
                root.transform.position = new Vector3(3, 2, 0);
                root.transform.rotation = Quaternion.Euler(0, 0, 35);
                root.transform.localScale = Vector3.one * .32f;
                var art = new GameObject("Visual"); art.transform.SetParent(root.transform, false);
                var bolt = new Projectile { transform = root.transform, renderer = art.AddComponent<SpriteRenderer>(),
                    direction = Vector2.right, arcNormal = Vector2.up, arcDistance = 4, arcHeight = 1 };
                Vector3 collisionOrigin = root.transform.position;
                CombatSystem.UpdateHomingArc(bolt, 4.55f);
                Check((art.transform.position - collisionOrigin).sqrMagnitude < .00001f, "Launch begins at the muzzle");
                CombatSystem.UpdateHomingArc(bolt, 2.55f);
                Check((art.transform.position - collisionOrigin - Vector3.up).sqrMagnitude < .00001f, "Mid-flight artwork rises in world space despite pooled parent scale and rotation");
                Check(root.transform.position == collisionOrigin && bolt.direction == Vector2.right, "Visual arcs cannot change steering or the swept collision path");
                CombatSystem.UpdateHomingArc(bolt, 3.55f);
                Check(art.transform.right.y > 0, "Projectile points into the rising curve");
                CombatSystem.UpdateHomingArc(bolt, 1.55f);
                Check(art.transform.right.y < 0, "Projectile turns down along the falling curve");
                CombatSystem.UpdateHomingArc(bolt, .55f);
                Check((art.transform.position - collisionOrigin).sqrMagnitude < .00001f, "Artwork meets the target at its hit boundary");
                CombatSystem.UpdateHomingArc(bolt, 1.5f, 1.5f);
                Check((art.transform.position - collisionOrigin).sqrMagnitude < .00001f, "Larger enemies also receive an aligned impact");
                bolt.arcNormal = Vector2.down;
                CombatSystem.UpdateHomingArc(bolt, 2.55f);
                Check(art.transform.position.y < collisionOrigin.y, "Volley siblings can take opposite arcs");
                bolt.arcHeight = 0;
                CombatSystem.UpdateHomingArc(bolt, 2.55f);
                Check((art.transform.position - collisionOrigin).sqrMagnitude < .00001f, "Non-homing shots remain straight");
                bolt.arcHeight = 1; bolt.arcDistance = 0;
                CombatSystem.UpdateHomingArc(bolt, 0);
                Check((art.transform.position - collisionOrigin).sqrMagnitude < .00001f, "Point-blank shots and lost targets do not create invalid offsets");
                Debug.Log("HOMING ARC PASSED: " + checks + " assertions");
                UnityEngine.Object.DestroyImmediate(root); EditorApplication.Exit(0);
            }
            catch (Exception e) { if (root != null) UnityEngine.Object.DestroyImmediate(root); Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
