#ifndef PROJECT_DM_MANA_FLOW_INCLUDED
#define PROJECT_DM_MANA_FLOW_INCLUDED

// Shared interior currents for the HUD and growth potion; never changes the liquid boundary.
float3 ProjectDMManaFlow(float2 uv, float time, float frequency, float strength, float3 tint)
{
    float warp = sin(uv.y * 8 + time) * .8 + sin(uv.y * 17 - time * .6) * .22;
    float phase = uv.x * frequency * 6.283 - time * 3 + warp;
    float current = sin(phase) * .5 + .5;
    float ribbon = pow(saturate(sin(phase * .53 + uv.y * 7 - time * .6)), 10);
    return (tint * current * .17 + lerp(tint, float3(1,1,1), .65) * ribbon * .19) * strength;
}

#endif
