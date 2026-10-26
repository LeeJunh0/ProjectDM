Shader "Hidden/ProjectDM/GrowthPotion"
{
    Properties
    {
        [HideInInspector] _MainTex ("Source", 2D) = "white" {}
        _LiquidColor ("Liquid", Color) = (.9,.8,.5,1)
        _Kind ("Element", Float) = 0
        _Clock ("Unscaled clock", Float) = 0
        _Fill ("Growth completion", Range(0,1)) = 0
        _Completed ("Completed glow", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Assets/GameContent/Art/Mana/ManaFlow.cginc"
            float4 _LiquidColor;
            float _Kind, _Clock, _Fill, _Completed;
            float hash(float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float ellipse(float2 p, float2 r) { return (length(p / r) - 1) * min(r.x, r.y); }
            float box(float2 p, float2 b, float r)
            { float2 q = abs(p) - b + r; return length(max(q, 0)) + min(max(q.x, q.y), 0) - r; }
            float smoothUnion(float a, float b, float k)
            { float h = saturate(.5 + .5 * (b - a) / k); return lerp(b, a, h) - k * h * (1 - h); }
            // Neck points 35 degrees above horizontal. Only the bottle rotates; liquid stays level.
            float2 bottleSpace(float2 p)
            {
                p.y += .12;
                return float2(.573576 * p.x - .819152 * p.y, .819152 * p.x + .573576 * p.y - .12);
            }
            float bottle(float2 p)
            { return smoothUnion(ellipse(p - float2(0, -.12), float2(.58, .58)), box(p - float2(0, .43), float2(.165, .235), .045), .09); }
            float segment(float2 p, float2 a, float2 b)
            { float2 v = b - a; return length(p - a - v * saturate(dot(p - a, v) / max(dot(v, v), .00001))); }
            float vineCurve(float2 p, float2 a, float2 b, float2 c)
            {
                float d = 10; float2 previous = a;
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8.0;
                    float2 next = lerp(lerp(a, b, t), lerp(b, c, t), t);
                    d = min(d, segment(p, previous, next)); previous = next;
                }
                return d;
            }
            float vineStem(float2 p)
            {
                // A broad neck wrap with a curled tip, plus one short wrap around the belly.
                float d = vineCurve(p, float2(.21,.44), float2(-.10,.27), float2(-.20,.43));
                d = min(d, vineCurve(p, float2(-.20,.43), float2(-.25,.54), float2(-.23,.66)));
                d = min(d, vineCurve(p, float2(-.23,.66), float2(-.28,.84), float2(-.43,.72)));
                d = min(d, vineCurve(p, float2(-.43,.72), float2(-.55,.52), float2(-.36,.51)));
                d = min(d, vineCurve(p, float2(-.36,.51), float2(-.23,.50), float2(-.29,.65)));
                d = min(d, vineCurve(p, float2(.53,-.22), float2(.39,-.34), float2(.18,-.35)));
                return min(d, vineCurve(p, float2(.18,-.35), float2(.10,-.42), float2(.23,-.52)));
            }
            void over(inout float4 dst, float3 color, float alpha)
            {
                alpha = saturate(alpha); float a = alpha + dst.a * (1 - alpha);
                dst.rgb = (color * alpha + dst.rgb * dst.a * (1 - alpha)) / max(.00001, a); dst.a = a;
            }
            float discharge(float2 p, float2 start, float2 end, float seed, float intensity)
            {
                float cycle = floor(_Clock * 5 + seed);
                float2 previous = start; float d = 10;
                for (int i = 1; i <= 7; i++)
                {
                    float t = i / 7.0;
                    float2 next = lerp(start, end, t);
                    next.x += (hash(cycle * 9 + seed * 21 + i * 1.7) - .5) * .24 * sin(t * 3.14159);
                    d = min(d, segment(p, previous, next));
                    if (i == 2 || i == 4)
                    {
                        float2 branch = next + float2((hash(seed + i * 11) < .5 ? -1 : 1) * .2, -.14);
                        float2 bend = lerp(next, branch, .5) + float2(.025, .04);
                        d = min(d, segment(p, next, bend) * 1.25);
                        d = min(d, segment(p, bend, branch) * 1.25);
                    }
                    previous = next;
                }
                return (exp(-d * 160) * 1.5 + exp(-d * 29) * .22) * intensity;
            }
            float snowflake(float2 p, float2 center, float size, float rotation)
            {
                p = (p - center) / size; float d = 10;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 1.047198 + rotation; float2 v = float2(cos(a), sin(a)), side = float2(-v.y, v.x);
                    d = min(d, segment(p, 0, v));
                    d = min(d, segment(p, v * .52, v * .77 + side * .22));
                    d = min(d, segment(p, v * .52, v * .77 - side * .22));
                }
                return exp(-d * size * 180);
            }
            float elementSymbol(float2 p, float time)
            {
                // A readable central emblem, with the smaller elemental motion behind it.
                float2 q = (p - float2(0, -.15)) / .30;
                if (_Kind < .5)
                {
                    float diamond = abs(abs(q.x) * 1.35 + abs(q.y) - .9) / 1.68;
                    float ring = abs(length(q * float2(1,.72)) - .9);
                    float star = saturate(1 - (abs(q.x) * 2.8 + abs(q.y) * 1.8));
                    return exp(-diamond * 23) + exp(-ring * 27) * .35 + star * .9;
                }
                if (_Kind < 1.5)
                {
                    q.x += sin(q.y * 3 - time * 1.4) * .065;
                    float flame = min(ellipse(q - float2(0,-.22), float2(.49,.62)),
                        min(ellipse(q - float2(-.25,.15), float2(.22,.61)), ellipse(q - float2(.29,-.13), float2(.22,.48))));
                    float hollow = ellipse(q - float2(.04,-.42), float2(.20,.29));
                    return (1 - smoothstep(-.03,.03,flame)) * smoothstep(-.02,.02,hollow);
                }
                if (_Kind < 2.5)
                {
                    float d = segment(q,float2(.22,.92),float2(-.34,.10));
                    d = min(d,segment(q,float2(-.34,.10),float2(.23,.10)));
                    d = min(d,segment(q,float2(.23,.10),float2(-.25,-.90)));
                    d = min(d,segment(q,float2(.12,-.1),float2(.62,-.28)) * 1.5);
                    d = min(d,segment(q,float2(-.11,.44),float2(-.57,.59)) * 1.5);
                    return exp(-d * 18) * (.88 + sin(time * 3.5) * .12);
                }
                return pow(snowflake(p, float2(0,-.15), .28, sin(time * .35) * .05), .55);
            }
            float4 frag(v2f_img input) : SV_Target
            {
                float2 p = input.uv * 2 - 1; float time = _Clock;
                // A screen-aligned pixel grid shapes the glass, cork and highlights only.
                float2 glassPoint = (floor(input.uv * 64) + .5) / 64 * 2 - 1;
                float2 bottlePoint = bottleSpace(glassPoint);
                float sd = bottle(bottlePoint), aa = .004;
                float glass = 1 - smoothstep(-aa, aa, sd);
                float4 result = 0; float3 tint = _LiquidColor.rgb;
                float glow = .07 + _Completed * (.34 + sin(time * 1.6) * .045);
                over(result, tint, exp(-max(sd, 0) * lerp(18, 11, _Completed)) * glow);
                // The rear loop passes behind the glass. The front stem is drawn over it below.
                float rear = vineCurve(bottlePoint, float2(.21,.44), float2(.40,.19), float2(.53,-.22));
                over(result, float3(.16,.30,.07), (1 - step(.030, rear)) * .8);
                over(result, float3(.19,.25,.32) + tint * .08, glass * .3);
                float surface = lerp(-.70, .56, saturate(_Fill)) + sin(p.x * 7 + time * .8) * .010 + sin(p.x * 11 - time * .6) * .004;
                // Completion fills the entire inner silhouette, including the tilted neck.
                float occupied = lerp(1 - smoothstep(surface - .008, surface + .008, p.y), 1, _Completed);
                float liquid = (1 - smoothstep(-.042, -.033, sd)) * occupied * step(.00001, _Fill);
                float depth = saturate((p.y + .76) / .9);
                float3 fluid = tint * (.38 + depth * .48);
                fluid += ProjectDMManaFlow(input.uv, time * .91, 3, .85, tint);
                fluid += tint * exp(-dot((p - float2(-.12,-.25)) * float2(2,1), (p - float2(-.12,-.25)) * float2(2,1)) * 7) * .10;
                over(result, fluid, liquid * .96);
                float energy = 0; float3 energyColor = lerp(tint, float3(1,1,1), .65);
                if (_Kind < .5)
                {
                    for (int i = 0; i < 3; i++) for (int tail = 0; tail < 5; tail++)
                    {
                        float a = -time * 1.4 + i * 2.0944 + tail * .12;
                        float2 q = float2(cos(a) * .29, -.32 + sin(a) * .2);
                        energy += exp(-dot(p - q, p - q) * (tail == 0 ? 1700 : 2600)) * (1 - tail * .17);
                    }
                }
                else if (_Kind < 1.5)
                {
                    float h = saturate((p.y + .7) / .8);
                    for (int i = 0; i < 3; i++)
                    {
                        float center = (i - 1) * .19 + sin(p.y * 15 - time * 2.4 + i * 2) * .055;
                        float width = .07 * (1 - h * .78);
                        energy += exp(-pow((p.x - center) / width, 2)) * sin(h * 3.14159) * (.7 + .3 * sin(time * 2.8 + i));
                    }
                    energyColor = lerp(float3(1,.32,.035), float3(1,.96,.55), saturate(energy));
                }
                else if (_Kind < 2.5)
                {
                    float flash = .12 + pow(saturate(sin(time * 2.8)), 7) * .88;
                    energy = discharge(p, float2(-.17,.04), float2(.18,-.64), 1, flash)
                        + discharge(p, float2(.26,-.05), float2(-.27,-.57), 4, .12 + pow(saturate(sin(time * 2.8 + 1.7)), 7) * .8);
                    energyColor = lerp(tint, float3(.95,.96,1), saturate(energy * 1.6));
                }
                else
                {
                    energy = snowflake(p, float2(-.14, -.28), .16, time * .12) * .9
                        + snowflake(p, float2(.24,-.49), .105, -time * .17) * .75
                        + snowflake(p, float2(.17,-.09), .065, time * .1) * .65;
                    energyColor = float3(.78,.96,1);
                }
                over(result, energyColor, saturate(energy) * liquid * .18);
                float2 symbolPoint = (floor(input.uv * 96) + .5) / 96 * 2 - 1;
                float symbol = saturate(elementSymbol(symbolPoint, time));
                float symbolBreath = .90 + sin(time * 1.8) * .04 + _Completed * .06;
                float3 symbolColor = _Kind > .5 && _Kind < 1.5 ? float3(1,.85,.35) : lerp(tint, float3(1,1,1), .75);
                // The emblem belongs to the bottle, so the rising liquid never clips it.
                float symbolInsideGlass = 1 - smoothstep(-.042, -.033, sd);
                over(result, symbolColor, symbol * symbolInsideGlass * symbolBreath);
                for (int i = 0; i < 7; i++)
                {
                    float2 q = float2((hash(i + 4) - .5) * .7, -.72 + frac(time * (.055 + i * .003) + hash(i + 12)) * .83);
                    float d = abs(length(p - q) - (.012 + hash(i + 8) * .012));
                    over(result, lerp(tint, float3(1,1,1), .65), exp(-d * 350) * liquid * .08);
                }
                over(result, lerp(tint, float3(1,1,1), .5), exp(-abs(p.y - surface) * 180) * liquid * .32);
                // Stepped glass rim and small highlight clusters blend pixel art into the bottle.
                float rim = step(-.034, sd) * glass;
                float lighting = floor(saturate(.4 - bottlePoint.x + bottlePoint.y * .5) * 4) / 4;
                over(result, lerp(float3(.23,.33,.43), float3(.68,.82,.9), lighting), rim * .8);
                over(result, lerp(tint,float3(1,1,1),.25), rim * _Completed * (.45 + sin(time * 1.6) * .06));
                float stripe = exp(-pow((bottlePoint.x + .38 + sin(bottlePoint.y * 3) * .025) / .025, 2));
                stripe *= smoothstep(-.53,-.32,bottlePoint.y) * (1 - smoothstep(.05,.21,bottlePoint.y)) * glass;
                stripe = floor(stripe * 4) / 4;
                over(result, float3(.93,.97,1), stripe * .5);
                float2 reflection = (bottlePoint - float2(.31,-.39)) * float2(1,2);
                float glint = floor(exp(-dot(reflection, reflection) * 170) * 3) / 3;
                over(result, float3(.9,.96,1), glint * glass * .7);
                float stem = vineStem(bottlePoint);
                over(result, float3(.045,.10,.025), 1 - step(.055,stem));
                float band = floor(saturate(.55 + bottlePoint.y * .5 - bottlePoint.x * .6) * 3) / 3;
                float3 stemColor = lerp(float3(.23,.46,.035), float3(.58,.83,.12), band);
                over(result, floor(stemColor * 12 + .5) / 12, 1 - step(.033,stem));
                // Cork and warm metal collar make the silhouette read as a potion at small sizes.
                float cork = 1 - smoothstep(-.003,.006,box(bottlePoint - float2(0,.73), float2(.18,.08), .016));
                float grain = .85 + .15 * sin(bottlePoint.y * 160 + sin(bottlePoint.x * 32));
                float3 corkColor = float3(.42,.24,.12) * grain + float3(.18,.12,.065) * saturate(-bottlePoint.x * 3);
                over(result, floor(corkColor * 8 + .5) / 8, cork);
                float collar = 1 - smoothstep(-.003,.005,box(bottlePoint - float2(0,.61), float2(.205,.04), .012));
                float3 metal = lerp(float3(.35,.23,.12), float3(.85,.66,.36), saturate(.6 - bottlePoint.x));
                over(result, floor(metal * 8 + .5) / 8, collar);
                over(result, float3(.95,.81,.54), step(abs(bottlePoint.y - .635), .014) * collar * .9);
                return result;
            }
            ENDCG
        }
    }
}
