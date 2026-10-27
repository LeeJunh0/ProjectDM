using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    internal static class SkillIconAtlas
    {
        static readonly Dictionary<string, Sprite> icons = new();
        static Texture2D atlas;
        internal static Sprite Find(string id)
        {
            if (id == null || !id.StartsWith("skill.")) return null;
            Sprite growth = GrowthIconAtlas.Find(id);
            if (growth != null) return growth;
            return FindOriginal(id);
        }

        internal static Sprite FindOriginal(string id)
        {
            if (id == null || !id.StartsWith("skill.")) return null;
            if (icons.TryGetValue(id, out var cached)) return cached;
            atlas ??= Resources.Load<Texture2D>("ProjectDM/Skills/SkillIcons");
            if (atlas == null) return null;
            string[][] cells = { new[]{"missile","beam","satellite"},new[]{"cone","firefield","meteor"},new[]{"chain","orb","thunder"},new[]{"orbit","blizzard","shatter"} };
            string[] types={"projectile","flame","lightning","frost"};
            for(int row=0;row<4;row++)for(int col=0;col<3;col++)
            {
                string key="skill."+types[row]+"."+cells[row][col];
                if(icons.ContainsKey(key))continue;
                // Round shared boundaries so no source pixel is duplicated or lost on non-divisible dimensions.
                int left=Mathf.RoundToInt(col*atlas.width/3f),right=Mathf.RoundToInt((col+1)*atlas.width/3f);
                int bottom=Mathf.RoundToInt((3-row)*atlas.height/4f),top=Mathf.RoundToInt((4-row)*atlas.height/4f);
                icons[key]=Sprite.Create(atlas,new Rect(left,bottom,right-left,top-bottom),new Vector2(.5f,.5f),Mathf.Max(right-left,top-bottom),0,SpriteMeshType.FullRect);
                icons[key].name=key;
            }
            return icons.TryGetValue(id,out var icon)?icon:null;
        }
    }
}
