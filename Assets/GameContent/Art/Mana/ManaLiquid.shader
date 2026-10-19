Shader "ProjectDM/UI/Liquid Mana"
{
    Properties
    {
        [HideInInspector] _MainTex ("입력 텍스처", 2D) = "white" {}
        _LiquidColor ("액체 색상", Color) = (.1,.48,1,1)
        _Fill ("잔량", Range(0,1)) = 1
        _Clock ("연출 시간", Float) = 0
        _WaveSpeed ("물결 속도", Float) = 1.4
        _WaveAmplitude ("물결 강도", Float) = .07
        _WaveFrequency ("물결 빈도", Float) = 3
        _FlowSpeed ("내부 흐름", Float) = .65
        _Glow ("발광", Float) = .4
        _Reaction ("출렁임 반응", Float) = 0
        _LowPulse ("부족 경고", Float) = 0
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
            float4 _LiquidColor, _Resolution;
            float _Fill, _Clock, _WaveSpeed, _WaveAmplitude, _WaveFrequency, _FlowSpeed, _Glow, _Reaction, _LowPulse;
            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 px = max(_Resolution.xy, 1);
                float time = _Clock * _WaveSpeed;
                float waves = sin(uv.x * _WaveFrequency * 6.283 - time * 2.2)
                            + .4 * sin(uv.x * _WaveFrequency * 10 + time * 1.7);
                float surface = .84 + waves * _WaveAmplitude * (1 + _Reaction);
                float edge = _Fill + sin(uv.y * 9 + time * 3) * (1 + _Reaction * 3) / px.x * sin(_Fill * 3.14159);
                float liquid = step(uv.x, edge) * step(uv.y, surface) * step(.0001, _Fill);
                float3 background = float3(.018,.027,.045);
                float3 color = _LiquidColor.rgb * (.45 + uv.y * .5);
                float flow = sin(uv.x * 35 - _Clock * _FlowSpeed * 4 + sin(uv.y * 12 + time)) * .5 + .5;
                color += _LiquidColor.rgb * flow * .12;
                float surfaceLight = 1 - smoothstep(1 / px.y, 2.5 / px.y, abs(uv.y - surface));
                float shimmer = pow(saturate(sin(uv.x * 13 - _Clock * _FlowSpeed + uv.y * 4)), 16);
                color += lerp(_LiquidColor.rgb, float3(1,1,1), .65) * (surfaceLight * .55 + shimmer * .13) * (1 + _Glow);
                color *= 1 + _Glow * .22 + _Reaction * .18 + _LowPulse * sin(_Clock * 6);
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
