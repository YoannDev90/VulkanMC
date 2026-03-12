#version 450

layout(location = 0) in vec3 fragColor;
layout(location = 1) in vec2 fragTexCoord;

layout(location = 0) out vec4 outColor;

layout(binding = 0) uniform sampler2D texSampler;

void main() {
    vec4 texColor = texture(texSampler, fragTexCoord);
    if (texColor.a < 0.1) discard; // Transparence pour les feuilles ou autres
    outColor = vec4(fragColor, 1.0) * texColor;
}
