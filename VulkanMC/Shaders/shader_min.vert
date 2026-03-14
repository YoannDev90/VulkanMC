#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inColor;
layout(location = 2) in vec2 inTexCoord;

layout(location = 0) out vec3 fragColor;
layout(location = 1) out vec2 fragTexCoord;

#ifdef VULKAN
layout(push_constant) uniform PushConstant { mat4 mvp; } pc;
#define GET_MVP() (pc.mvp)
#else
uniform mat4 pc_mvp;
#define GET_MVP() (pc_mvp)
#endif

void main() {
    gl_Position = GET_MVP() * vec4(inPosition, 1.0);
    fragColor = inColor;
    fragTexCoord = inTexCoord;
}
