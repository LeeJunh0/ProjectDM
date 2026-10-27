Shader "ProjectDM/SceneryDither"
{
    Properties { [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            float bayer2(float2 p) { return 2 * p.x + 3 * p.y - 4 * p.x * p.y; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 texel = floor(i.uv * _MainTex_TexelSize.zw);
                float2 cell = fmod(texel, 4.0);
                float threshold = (4 * bayer2(fmod(cell, 2.0)) + bayer2(floor(cell / 2.0)) + .5) / 16.0;
                clip(i.color.a - threshold);
                fixed4 color = tex2D(_MainTex, i.uv);
                color.rgb *= i.color.rgb;
                return color;
            }
            ENDCG
        }
    }
}
