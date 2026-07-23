// VRF-TEST
// SPIR-V source (2632 bytes), GLSL reflection with SPIRV-Cross by KhronosGroup

#version 460

struct _1125
{
    uint _m0;
    uint _m1;
    uint _m2;
    uint _m3;
    uint _m4;
    uint _m5;
    uint _m6;
    float _m7;
};

struct _730
{
    vec4 _m0[3];
};

struct _2419
{
    _730 _m0;
};

struct anon_g_matWorldToProjection
{
    vec4 _m0[4];
};

layout(set = 0, binding = 32, std430) readonly buffer g_instanceBuffer
{
    _1125 _m0[];
} g_instanceBuffer_1;

layout(set = 0, binding = 30, std430) readonly buffer g_transformBuffer
{
    _2419 _m0[];
} g_transformBuffer_1;

struct _2734
{
    anon_g_matWorldToProjection g_matWorldToProjection;
    vec4 g_vWorldToCameraOffset;
};

layout(set = 0) uniform _2734 PerViewConstantBuffer_t;

layout(location = 0) in vec3 vPositionOs;
layout(location = 1) in uint nInstanceIdx;

void main()
{
    vec4 _24787 = (vec4((vec4(vPositionOs.xyz, 1.0) * mat3x4(g_transformBuffer_1._m0[g_instanceBuffer_1._m0[nInstanceIdx]._m1]._m0._m0[0], g_transformBuffer_1._m0[g_instanceBuffer_1._m0[nInstanceIdx]._m1]._m0._m0[1], g_transformBuffer_1._m0[g_instanceBuffer_1._m0[nInstanceIdx]._m1]._m0._m0[2])).xyz, 1.0) + (PerViewConstantBuffer_t.g_vWorldToCameraOffset * 1.0)).xyzw * mat4(vec4(PerViewConstantBuffer_t.g_matWorldToProjection._m0[0].x, PerViewConstantBuffer_t.g_matWorldToProjection._m0[1].x, PerViewConstantBuffer_t.g_matWorldToProjection._m0[2].x, PerViewConstantBuffer_t.g_matWorldToProjection._m0[3].x), vec4(PerViewConstantBuffer_t.g_matWorldToProjection._m0[0].y, PerViewConstantBuffer_t.g_matWorldToProjection._m0[1].y, PerViewConstantBuffer_t.g_matWorldToProjection._m0[2].y, PerViewConstantBuffer_t.g_matWorldToProjection._m0[3].y), vec4(PerViewConstantBuffer_t.g_matWorldToProjection._m0[0].z, PerViewConstantBuffer_t.g_matWorldToProjection._m0[1].z, PerViewConstantBuffer_t.g_matWorldToProjection._m0[2].z, PerViewConstantBuffer_t.g_matWorldToProjection._m0[3].z), vec4(PerViewConstantBuffer_t.g_matWorldToProjection._m0[0].w, PerViewConstantBuffer_t.g_matWorldToProjection._m0[1].w, PerViewConstantBuffer_t.g_matWorldToProjection._m0[2].w, PerViewConstantBuffer_t.g_matWorldToProjection._m0[3].w));
    _24787.y = -_24787.y;
    gl_Position = _24787;
}


