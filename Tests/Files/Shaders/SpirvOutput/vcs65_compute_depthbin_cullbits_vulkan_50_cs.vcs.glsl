// VRF-TEST
// SPIR-V source (2540 bytes), GLSL reflection with SPIRV-Cross by KhronosGroup

#version 460
layout(local_size_x = 32, local_size_y = 1, local_size_z = 1) in;

struct anon_g_Batches
{
    uint _m0;
    uint _m1;
    uint _m2;
    uint _m3;
};

struct anon_g_Items
{
    float _m0;
    float _m1;
};

struct _2270
{
    float g_fDepthBinWidth;
    float g_fEpsilon;
    float g_fNearPlane;
    anon_g_Batches g_Batches[2];
    anon_g_Items g_Items[448];
};

layout(set = 0) uniform _2270 BinCullParams_t;

layout(set = 0, binding = 158, std430) writeonly buffer undetermined
{
    uint _m0[];
} undetermined_1;

void main()
{
    float _15320 = (BinCullParams_t.g_fNearPlane + (float(gl_GlobalInvocationID.x) * BinCullParams_t.g_fDepthBinWidth)) - BinCullParams_t.g_fEpsilon;
    float _23639 = (BinCullParams_t.g_fNearPlane + (float(gl_GlobalInvocationID.x + 1u) * BinCullParams_t.g_fDepthBinWidth)) + BinCullParams_t.g_fEpsilon;
    for (uint _23131 = 0u; _23131 < 2u; _23131++)
    {
        uint _13033;
        for (uint _9864 = BinCullParams_t.g_Batches[_23131]._m2, _13686 = 0u; _13686 < BinCullParams_t.g_Batches[_23131]._m1; _9864 = _13033, _13686++)
        {
            uint _11175;
            _11175 = 0u;
            _13033 = _9864;
            uint _10540;
            for (uint _6708 = 0u; (_6708 < 32u) && (_13033 < BinCullParams_t.g_Batches[_23131]._m3); _11175 = _10540, _13033++, _6708++)
            {
                if ((BinCullParams_t.g_Items[_13033]._m0 <= _23639) && (BinCullParams_t.g_Items[_13033]._m1 >= _15320))
                {
                    _10540 = _11175 | (1u << _6708);
                }
                else
                {
                    _10540 = _11175;
                }
            }
            undetermined_1._m0[(BinCullParams_t.g_Batches[_23131]._m0 + (gl_GlobalInvocationID.x * BinCullParams_t.g_Batches[_23131]._m1)) + _13686] = _11175;
        }
    }
}


