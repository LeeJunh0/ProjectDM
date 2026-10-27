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
            internal bool colorsDirty;
        }
        readonly struct ColorHandle
        {
            internal readonly Page page;
            internal readonly int start;
            internal ColorHandle(Page page,int start){this.page=page;this.start=start;}
        }
        readonly Dictionary<Texture,Page> pages=new();
        readonly List<ColorHandle> colorHandles;
        readonly GameObject root;
        readonly int sortingOrder;
        readonly Shader shader;
        internal int Count {get;private set;}
        internal SpriteQuadBatch(string name,int sortingOrder,Transform parent=null,HideFlags hideFlags=HideFlags.None,Shader shader=null,bool trackColors=false)
        {root=new GameObject(name){hideFlags=hideFlags};root.transform.SetParent(parent,true);this.sortingOrder=sortingOrder;this.shader=shader;colorHandles=trackColors?new List<ColorHandle>():null;}
        internal void Begin()
        {Count=0;colorHandles?.Clear();foreach(var p in pages.Values){p.vertices.Clear();p.uv.Clear();p.colors.Clear();p.triangles.Clear();p.colorsDirty=false;}}
        internal int Add(Sprite sprite,Vector3 center,Vector2 size,float rotation,Color color)
        {
            if(sprite==null)return -1;
            if(!pages.TryGetValue(sprite.texture,out var p))
            {
                var child=new GameObject("텍스처 일괄 그리기"){hideFlags=root.hideFlags};child.transform.SetParent(root.transform,false);
                p=new Page{mesh=new Mesh{name="전체 스프라이트 배치",indexFormat=IndexFormat.UInt32},renderer=child.AddComponent<MeshRenderer>()};
                p.mesh.MarkDynamic();child.AddComponent<MeshFilter>().sharedMesh=p.mesh;
                p.renderer.sharedMaterial=new Material(shader!=null?shader:Shader.Find("Sprites/Default")){mainTexture=sprite.texture};
                p.renderer.sortingOrder=sortingOrder;pages.Add(sprite.texture,p);
            }
            int n=p.vertices.Count;float r=rotation*Mathf.Deg2Rad,c=Mathf.Cos(r),s=Mathf.Sin(r);
            Vector3 x=new Vector3(c,s)*size.x*.5f,y=new Vector3(-s,c)*size.y*.5f;
            p.vertices.Add(center-x-y);p.vertices.Add(center+x-y);p.vertices.Add(center+x+y);p.vertices.Add(center-x+y);
            Rect uv=sprite.textureRect;float l=uv.xMin/sprite.texture.width,rr=uv.xMax/sprite.texture.width,b=uv.yMin/sprite.texture.height,t=uv.yMax/sprite.texture.height;
            p.uv.Add(new Vector2(l,b));p.uv.Add(new Vector2(rr,b));p.uv.Add(new Vector2(rr,t));p.uv.Add(new Vector2(l,t));
            for(int i=0;i<4;i++)p.colors.Add(color);
            colorHandles?.Add(new ColorHandle(p,n));
            p.triangles.Add(n);p.triangles.Add(n+2);p.triangles.Add(n+1);p.triangles.Add(n);p.triangles.Add(n+3);p.triangles.Add(n+2);return Count++;
        }
        internal void SetColor(int handle,Color color)
        {
            if(colorHandles==null||handle<0||handle>=colorHandles.Count)return;
            var item=colorHandles[handle];
            for(int i=0;i<4;i++)item.page.colors[item.start+i]=color;
            item.page.colorsDirty=true;
        }
        internal void CommitColors()
        {foreach(var p in pages.Values)if(p.colorsDirty){p.mesh.SetColors(p.colors);p.colorsDirty=false;}}
        internal void End()
        {
            foreach(var p in pages.Values)
            {p.renderer.enabled=p.vertices.Count>0;p.mesh.Clear();if(p.vertices.Count==0)continue;p.mesh.SetVertices(p.vertices);p.mesh.SetUVs(0,p.uv);p.mesh.SetColors(p.colors);p.mesh.SetTriangles(p.triangles,0);p.mesh.RecalculateBounds();}
        }
        internal void Clear(){Begin();End();}
        internal void Dispose()
        {foreach(var p in pages.Values){DestroyOwned(p.mesh);DestroyOwned(p.renderer.sharedMaterial);}DestroyOwned(root);pages.Clear();}
        static void DestroyOwned(Object value)
        {if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
