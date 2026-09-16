#ifndef CUSTOM_ADDITIONAL_LIGHTS_INCLUDED
#define CUSTOM_ADDITIONAL_LIGHTS_INCLUDED

void AdditionalLights_float(
    float3 PositionWS,
    float3 NormalWS,
    out float LightMask,
    out float3 LightColor)
{
#if defined(SHADERGRAPH_PREVIEW)

    LightMask = 0.5;
    LightColor = float3(1.0, 1.0, 1.0);

#else

    LightMask = 0.0;
    LightColor = float3(0.0, 0.0, 0.0);

    uint lightCount = GetAdditionalLightsCount();

    for (uint i = 0u; i < lightCount; ++i)
    {
        Light light = GetAdditionalLight(i, PositionWS);

        float3 L = normalize(light.direction);

        float NdotL = saturate(
            dot(normalize(NormalWS), L)
        );

        float attenuation =
            light.distanceAttenuation *
            light.shadowAttenuation;

        float contribution =
            NdotL * attenuation;

        LightMask += contribution;

        LightColor +=
            light.color *
            contribution;
    }

    LightMask = saturate(LightMask);

#endif
}

#endif