using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectDM
{
    /// <summary>One dynamic mesh per texture. Every submitted sprite stays visible without one GameObject per sprite.</summary>
    internal sealed class SpriteQuadBatch
    {
        sealed class Page
        {
            internal Mesh mesh;
            internal MeshRenderer renderer;
            internal readonly List<Vector3> vertices=new();
            internal readonly List<Vector2> uv=new();
            internal readonly List<Color> colors=new();
            internal readonly List<int> triangles=new();
        }
        readonly Dictionary<Texture,Page> pages=new();
        readonly GameObject root;
        readonly int sortingOrder;
        internal int Count {get;private set;}
        internal SpriteQuadBatch(string name,int sortingOrder)
        {root=new GameObject(name);this.sortingOrder=sortingOrder;}
        internal void Begin()
        {Count=0;foreach(var p in pages.Values){p.vertices.Clear();p.uv.Clear();p.colors.Clear();p.triangles.Clear();}}
        internal void Add(Sprite sprite,Vector3 center,Vector2 size,float rotation,Color color)
        {
            if(sprite==null)return;
            if(!pages.TryGetValue(sprite.texture,out var p))
            {
                var child=new GameObject("텍스처 일괄 그리기");child.transform.SetParent(root.transform,false);
                p=new Page{mesh=new Mesh{name="전체 스프라이트 배치",indexFormat=IndexFormat.UInt32},renderer=child.AddComponent<MeshRenderer>()};
                p.mesh.MarkDynamic();child.AddComponent<MeshFilter>().sharedMesh=p.mesh;
                p.renderer.sharedMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=sprite.texture};
                p.renderer.sortingOrder=sortingOrder;pages.Add(sprite.texture,p);
            }
            int n=p.vertices.Count;float r=rotation*Mathf.Deg2Rad,c=Mathf.Cos(r),s=Mathf.Sin(r);
            Vector3 x=new Vector3(c,s)*size.x*.5f,y=new Vector3(-s,c)*size.y*.5f;
            p.vertices.Add(center-x-y);p.vertices.Add(center+x-y);p.vertices.Add(center+x+y);p.vertices.Add(center-x+y);
            Rect uv=sprite.textureRect;float l=uv.xMin/sprite.texture.width,rr=uv.xMax/sprite.texture.width,b=uv.yMin/sprite.texture.height,t=uv.yMax/sprite.texture.height;
            p.uv.Add(new Vector2(l,b));p.uv.Add(new Vector2(rr,b));p.uv.Add(new Vector2(rr,t));p.uv.Add(new Vector2(l,t));
            for(int i=0;i<4;i++)p.colors.Add(color);
            p.triangles.Add(n);p.triangles.Add(n+2);p.triangles.Add(n+1);p.triangles.Add(n);p.triangles.Add(n+3);p.triangles.Add(n+2);Count++;
        }
        internal void End()
        {
            foreach(var p in pages.Values)
            {p.renderer.enabled=p.vertices.Count>0;p.mesh.Clear();if(p.vertices.Count==0)continue;p.mesh.SetVertices(p.vertices);p.mesh.SetUVs(0,p.uv);p.mesh.SetColors(p.colors);p.mesh.SetTriangles(p.triangles,0);p.mesh.RecalculateBounds();}
        }
        internal void Clear(){Begin();End();}
        internal void Dispose()
        {foreach(var p in pages.Values){Object.Destroy(p.mesh);Object.Destroy(p.renderer.sharedMaterial);}Object.Destroy(root);pages.Clear();}
    }
}
