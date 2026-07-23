// VRF-TEST
// SPIR-V source (904 bytes), GLSL reflection with SPIRV-Cross by KhronosGroup

#version 460

struct _1017
{
    vec4 g_vInvTexDim;
};

layout(set = 1) uniform _1017 _Globals_;

layout(set = 1, binding = 30) uniform texture2D g_tInputBuffer;
layout(set = 1, binding = 14) uniform sampler AddressU_Clamp_AddressV_Clamp_Filter_MinMagLinearMipPoint;

layout(location = 0) out vec4 output_0;

void main()
{
    output_0 = textureLod(sampler2D(g_tInputBuffer, AddressU_Clamp_AddressV_Clamp_Filter_MinMagLinearMipPoint), (gl_FragCoord.xy * _Globals_.g_vInvTexDim.xy).xy, 0.0);
}


