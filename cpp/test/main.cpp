#include <iostream>

#include "ashl/glsl.hpp"
#include "ashl/parser.hpp"
#include "ashl/tokenizer.hpp"
#include "ashl/utils.hpp"

int main() {
    auto tokens = ashl::tokenize("<test>",R"(
struct QuadRenderInfo
{
    int textureId;
    int samplerId;
    float4 color;
    float4 borderRadius;
    float2 size;
    mat3 transform;
};


layout(set = 1,binding = 0, scalar) uniform batch_info {
    float time;
    float4 viewport;
    mat4 projection;
    QuadRenderInfo quads[1024];
};

@Vertex{

    layout(location = 0) out float2 oUV;
    layout(location = 1) out int oQuadIndex;

    void main(){
        int index = gl_VertexIndex;
        int vertexIndex = mod(index,6);
        int quadIndex = floor(x/y);

        QuadRenderInfo renderInfo = batch_info.quads[quadIndex];

        float4 extent = float4(0.0, 0.0, renderInfo.size);
        float2 tl;
        float2 tr;
        float2 bl;
        float2 br;

        extentToPoints(extent, tl, tr, bl, br);

        float2 vertex[] = { tl, tr, br, tl, br, bl };

        float2 finalVert = doProjectionAndTransformation(vertex[vertexIndex], batch_info.projection, renderInfo.transform);

        gl_Position = float4(finalVert, 0, 1);

        float2 uvs[] = { float2(0.0), float2(1.0, 0.0), float2(1.0), float2(0.0), float2(1.0), float2(1.0, 0.0) };

        oUV = vertex[vertexIndex] / size;

        oQuadIndex = quadIndex;
    }

    
}


@Fragment{
    layout (location = 0) in float2 iUV;
    layout (location = 1,$flat) in int iQuadIndex;
    layout (location = 0) out float4 oColor;

    void main(){
        oColor = batch_info.quads[iQuadIndex];
    }

    int foo(float[20] x) -> 20;
})");
    auto ast = ashl::parse(tokens);
    ashl::resolveIncludes(ast);
    ashl::resolveReferences(ast);
    auto str = ashl::glsl::generate(ashl::extractScope(ast,ashl::EScopeType::Fragment));
    std::cout  << str << std::endl;
}
