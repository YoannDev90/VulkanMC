#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec2 inTexCoord;

layout(location = 0) out vec2 fragTexCoord;

layout(push_constant) uniform PushConstant {
    vec2 scale;
    vec2 translate;
} pc;

void main() {
    gl_Position = vec4(inPosition * pc.scale + pc.translate, 0.0, 1.0);
    fragTexCoord = inTexCoord;
}
