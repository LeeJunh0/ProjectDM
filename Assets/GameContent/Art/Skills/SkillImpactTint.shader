Shader "ProjectDM/Skill Impact Tint"
{
    Properties
    {
        [PerRendererData] _MainTex ("스프라이트 텍스처", 2D) = "white" {}
        _Color ("색상", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("픽셀 정렬", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment ImpactFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            fixed4 ImpactFrag(v2f input) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(input.texcoord);
                // Reuse the approved aqua impact silhouette with warm meteor colors.
                // Preserve its alpha instead of painting an opaque square around the effect.
                fixed value = max(source.r, max(source.g, source.b));
                fixed4 color = fixed4(value, value, value, source.a) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
