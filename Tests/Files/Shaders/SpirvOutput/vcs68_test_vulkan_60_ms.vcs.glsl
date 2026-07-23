// VRF-TEST
// SPIR-V source (2492 bytes), GLSL reflection with SPIRV-Cross by KhronosGroup
// Dynamic combos: D_MS_TEST

#version 460
#extension GL_EXT_mesh_shader : require
layout(local_size_x = 4, local_size_y = 1, local_size_z = 1) in;
layout(max_vertices = 4, max_primitives = 2, triangles) out;

float _24362;

layout(set = 3, binding = 31, std430) readonly buffer g_inputVB
{
    float _m0[];
} g_inputVB_1;

layout(location = 0) out vec3 output_0[4];
layout(location = 1) out vec2 output_1[4];

void main()
{
    SetMeshOutputsEXT(4u, 2u);
    int _15567 = int(gl_GlobalInvocationID.x * 36u);
    output_0[gl_GlobalInvocationID.x].x = g_inputVB_1._m0[(_15567 + 12) / 4];
    output_0[gl_GlobalInvocationID.x].y = g_inputVB_1._m0[(_15567 + 16) / 4];
    output_0[gl_GlobalInvocationID.x].z = g_inputVB_1._m0[(_15567 + 20) / 4];
    output_1[gl_GlobalInvocationID.x].x = g_inputVB_1._m0[(_15567 + 28) / 4];
    output_1[gl_GlobalInvocationID.x].y = g_inputVB_1._m0[(_15567 + 32) / 4];
    vec4 _15926 = vec4(g_inputVB_1._m0[_15567 / 4], _24362, g_inputVB_1._m0[(_15567 + 8) / 4], 1.0);
    _15926.y = g_inputVB_1._m0[(_15567 + 4) / 4];
    gl_MeshVerticesEXT[gl_GlobalInvocationID.x].gl_Position = _15926;
    if (gl_GlobalInvocationID.x == 0u)
    {
        gl_PrimitiveTriangleIndicesEXT[0u] = uvec3(2u, 1u, 0u);
    }
    else
    {
        if (gl_GlobalInvocationID.x == 1u)
        {
            gl_PrimitiveTriangleIndicesEXT[1u] = uvec3(2u, 0u, 3u);
        }
    }
}


