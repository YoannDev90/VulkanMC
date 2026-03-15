#version 450

layout(location = 0) in vec2 fragTexCoord;

layout(location = 0) out vec4 outColor;

layout(binding = 0) uniform sampler2D guiSampler;

void main() {
    float alpha = texture(guiSampler, fragTexCoord).r;
    if (alpha < 0.1) discard;
    outColor = vec4(1.0, 1.0, 1.0, alpha);
}
