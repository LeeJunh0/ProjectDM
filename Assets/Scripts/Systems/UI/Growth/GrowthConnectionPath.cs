using UnityEngine;

namespace ProjectDM
{
    internal static class GrowthConnectionPath
    {
        internal static Vector2[] Points(Vector2 from, Vector2 to, bool orthogonal, float scale)
        {
            if (!orthogonal || Mathf.Approximately(from.x, to.x) || Mathf.Approximately(from.y, to.y))
                return new[] { from, to };
            float lane = (from.x + to.x) * .5f;
            return new[] { from, new Vector2(lane, from.y), new Vector2(lane, to.y), to };
        }
    }
}
