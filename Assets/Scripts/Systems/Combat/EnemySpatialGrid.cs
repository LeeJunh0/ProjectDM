using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Shared non-allocating broad phase, rebuilt after movement. Dead entries are filtered at query time.</summary>
    internal sealed class EnemySpatialGrid
    {
        const float Cell = 2;
        readonly Dictionary<long,List<Enemy>> buckets = new();
        readonly List<long> used = new();
        readonly Stack<List<Enemy>> pool = new();
        static long Key(int x,int y) => ((long)x<<32) ^ (uint)y;
        internal void Rebuild(List<Enemy> enemies)
        {
            foreach(long key in used){var list=buckets[key];list.Clear();pool.Push(list);}
            buckets.Clear();used.Clear();
            foreach(var e in enemies)
            {
                if(!EnemySystem.IsLive(e))continue;Vector3 p=e.transform.position;
                long key=Key(Mathf.FloorToInt(p.x/Cell),Mathf.FloorToInt(p.y/Cell));
                if(!buckets.TryGetValue(key,out var list)){list=pool.Count>0?pool.Pop():new List<Enemy>(16);buckets.Add(key,list);used.Add(key);}
                list.Add(e);
            }
        }
        internal void Bounds(Vector3 min,Vector3 max,List<Enemy> output)
        {
            output.Clear();
            int x0=Mathf.FloorToInt(min.x/Cell),x1=Mathf.FloorToInt(max.x/Cell),y0=Mathf.FloorToInt(min.y/Cell),y1=Mathf.FloorToInt(max.y/Cell);
            for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++)if(buckets.TryGetValue(Key(x,y),out var list))
                foreach(var e in list)if(EnemySystem.IsLive(e))output.Add(e);
        }
    }
}
