#ifndef APPLE_REST_VEGETATION_WIND_INCLUDED
#define APPLE_REST_VEGETATION_WIND_INCLUDED

// Runtime-only per-renderer data: world base height, inverse height, height, phase.
float4 _AppleRestVegetationData;
float4 _AppleRestWindSettings; // world X/Z direction, speed, relative strength
float _AppleRestWindTime;

float3 AppleRestWindPosition(float3 positionOS)
{
    float3 world = TransformObjectToWorld(positionOS);
    float h = saturate((world.y - _AppleRestVegetationData.x) * _AppleRestVegetationData.y);
    // The bottom 15% is stationary. Only upper vertices receive appreciable displacement.
    float weight = smoothstep(0.15, 1.0, h);
    weight *= weight;
    float phase = _AppleRestVegetationData.w;
    float t = _AppleRestWindTime * _AppleRestWindSettings.z;
    float breeze = sin(t * 1.35 + phase) * 0.65 + sin(t * 2.17 + phase * 0.73) * 0.25;
    float flutter = sin(t * 3.4 + world.x * 2.1 + world.z * 1.7 + phase) * 0.1;
    float amount = (breeze + flutter * h) * weight * _AppleRestVegetationData.z * _AppleRestWindSettings.w;
    world.xz += _AppleRestWindSettings.xy * amount;
    return TransformWorldToObject(world);
}
#endif
