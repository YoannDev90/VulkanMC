#version 450

layout(location = 0) in vec3 fragColor;
layout(location = 1) in vec2 fragTexCoord;
layout(location = 2) in vec3 fragWorldPos;

layout(location = 0) out vec4 outColor;

layout(binding = 0) uniform sampler2D texSampler;

void main() {
    vec4 texColor = texture(texSampler, fragTexCoord);
    if (texColor.a < 0.1) discard;

    vec3 base = texColor.rgb * fragColor;

    // Bliss-inspired atmosphere: colored distance fog + low-altitude haze.
    float dist = length(fragWorldPos.xz);
    float distFog = smoothstep(48.0, 300.0, dist);
    float altitudeHaze = smoothstep(40.0, -8.0, fragWorldPos.y);
    float fogFactor = clamp(max(distFog, altitudeHaze * 0.55), 0.0, 1.0);

    vec3 horizonFog = vec3(0.60, 0.72, 0.88);
    vec3 skyFog = vec3(0.43, 0.62, 0.86);
    vec3 fogColor = mix(horizonFog, skyFog, clamp(fragWorldPos.y / 96.0, 0.0, 1.0));

    // Very light warm tonemapping to emulate Bliss-like grading.
    vec3 graded = mix(base, base * vec3(1.05, 1.00, 0.94), 0.18);
    vec3 finalRgb = mix(graded, fogColor, fogFactor);

    outColor = vec4(finalRgb, texColor.a);
}
