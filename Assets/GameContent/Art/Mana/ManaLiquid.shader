Shader "ProjectDM/UI/Liquid Mana"
{
    Properties
    {
        [HideInInspector] _MainTex ("입력 텍스처", 2D) = "white" {}
        _LiquidColor ("액체 색상", Color) = (.1,.48,1,1)
        _Fill ("잔량", Range(0,1)) = 1
        _Clock ("연출 시간", Float) = 0
        _WaveSpeed ("물결 속도", Float) = 1.4
        _WaveAmplitude ("흐름 굴곡 강도", Float) = .07
        _WaveFrequency ("흐름 빈도", Float) = 3
        _FlowSpeed ("내부 흐름", Float) = .65
        _Glow ("발광", Float) = .4
        _Reaction ("출렁임 반응", Float) = 0
        _LowPulse ("부족 경고", Float) = 0
        _HitImpact ("피격 충격", Float) = 0
        _HitClock ("피격 경과", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ManaFlow.cginc"
            float4 _LiquidColor, _Resolution;
            float _Fill, _Clock, _WaveSpeed, _WaveAmplitude, _WaveFrequency, _FlowSpeed, _Glow, _Reaction, _LowPulse;
            float _HitImpact, _HitClock;
            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 px = max(_Resolution.xy, 1);
                float time = _Clock * _WaveSpeed;
                float shock = sin((uv.x - _Fill) * 32 + _HitClock * 30) * exp(-abs(uv.x - (_Fill - _HitClock * 1.8)) * 5);
                // Remaining MP fills the full height. Flow and hits animate inside a fixed boundary.
                float liquid = step(uv.x, _Fill) * step(.0001, _Fill);
                float3 background = float3(.018,.027,.045);
                float3 color = _LiquidColor.rgb * (.60 + uv.y * .25);
                color += ProjectDMManaFlow(uv, time * _FlowSpeed, _WaveFrequency, (1 + _WaveAmplitude * 4) * (1 + _Reaction * .4), _LiquidColor.rgb);
                float edgeLight = 1 - smoothstep(1 / px.x, 2.5 / px.x, abs(uv.x - _Fill));
                color += lerp(_LiquidColor.rgb, float3(1,1,1), .65) * edgeLight * .18 * (1 + _Glow);
                color *= 1 + _Glow * .22 + _Reaction * .18 + _LowPulse * sin(_Clock * 6);
                color = lerp(color, float3(.24,.72,1), _HitImpact * .45);
                color += float3(.35,.75,1) * pow(saturate(shock), 4) * _HitImpact * .6;
                // Sparse deterministic bubbles. No textures or gameplay random numbers are sampled.
                for (int i = 0; i < 4; i++)
                {
                    float bx = frac(.17 + i * .237 + sin(_Clock * .3 + i) * .018);
                    float by = frac(_Clock * .12 * _FlowSpeed + i * .31);
                    float2 difference = (uv - float2(bx, by)) * px;
                    color += exp(-dot(difference, difference) / 2) * .28;
                }
                float3 result = lerp(background, color, liquid);
                float border = step(uv.x * px.x, 1) + step((1-uv.x)*px.x,1)
                             + step(uv.y*px.y,1) + step((1-uv.y)*px.y,1);
                result = lerp(result, float3(.22,.27,.36), saturate(border) * .7);
                return fixed4(result, 1);
            }
            ENDCG
        }
    }
}
