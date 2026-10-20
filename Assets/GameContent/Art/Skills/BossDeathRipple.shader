Shader "ProjectDM/Boss Death Space Ripple"
{
    Properties
    {
        _Progress ("Progress", Range(0,1)) = 0
        _Tint ("Quiet Tint", Color) = (.48,.64,.7,.14)
        _Distortion ("Refraction Pixels", Range(0,6)) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_ProjectDMBossRippleBackground" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _ProjectDMBossRippleBackground;
            float4 _ProjectDMBossRippleBackground_TexelSize;
            float _Progress, _Distortion;
            fixed4 _Tint;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv;
                o.screen = ComputeGrabScreenPos(o.vertex); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 offset = (i.uv - .5) * 2;
                float radius = length(offset);
                float2 direction = offset / max(radius, .001);
                float envelope = 0, refraction = 0;
                [unroll] for (int ring = 0; ring < 2; ring++)
                {
                    float t = _Progress * 1.18 - ring * .18;
                    float enabled = step(0, t) * (1 - step(1, t));
                    float distance = (radius - (.05 + .88 * saturate(t))) / .035;
                    float band = exp(-distance * distance * 2) * enabled;
                    float fade = smoothstep(0, .1, t) * (1 - smoothstep(.55, 1, t));
                    envelope += band * fade;
                    refraction += -distance * band * fade;
                }
                clip(envelope - .002);
                float2 uv = i.screen.xy / i.screen.w;
                uv += direction * refraction * _Distortion * _ProjectDMBossRippleBackground_TexelSize.xy;
                fixed3 background = tex2D(_ProjectDMBossRippleBackground, uv).rgb;
                return fixed4(lerp(background, _Tint.rgb, _Tint.a), saturate(envelope) * .8);
            }
            ENDCG
        }
    }
}
