// VRF-TEST
// SPIR-V source, GLSL reflection with SPIRV-Cross by KhronosGroup
// Dynamic combos: D_WARP, D_WARP_UVS_ON_MESH

#version 460

layout(location = 0) in vec2 vPositionPs;
layout(location = 1) in vec2 vTexCoordR;
layout(location = 2) in vec2 vTexCoordG;
layout(location = 3) in vec2 vTexCoordB;
layout(location = 0) noperspective out vec2 output_0;
layout(location = 1) noperspective out vec2 output_1;
layout(location = 2) noperspective out vec2 output_2;

void main()
{
    vec4 _18779 = vec4(vPositionPs, 0.0, 1.0);
    output_0 = vTexCoordR;
    output_1 = vTexCoordG;
    output_2 = vTexCoordB;
    _18779.y = -vPositionPs.y;
    gl_Position = _18779;
}


