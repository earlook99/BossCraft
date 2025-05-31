#ifndef PIXEL_OUTLINE_MASK_INCLUDED
#define PIXEL_OUTLINE_MASK_INCLUDED

inline float PixelOutline_Core(float2 uv, float widthPX)
{
    float2 texel = _MainTex_TexelSize.xy;
    float  aC    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
    float  aMax  = aC;

    int w = (int)clamp(round(widthPX), 0, 12);
    [unroll]
    for (int r = 1; r <= w; ++r)
    {
        float2 d = texel * r;
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d.x, 0)).a);
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d.x, 0)).a);
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0 , d.y)).a);
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0 ,-d.y)).a);
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv +  d).a);
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv -  d).a);
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d.x,-d.y)).a);
        aMax = max(aMax, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d.x, d.y)).a);
    }

    return step(0.008, aMax - aC);   // ★ 리턴값!
}

/* ── Shader Graph가 부를 함수 (void 반환 및 out 파라미터 사용) ── */
// Shader Graph Custom Function 노드의 Name 필드에는 PixelOutlineMask_float2 를 사용합니다.
// Shader Graph는 내부적으로 _float를 붙여서 호출할 수 있습니다. (이 경우는 아닐 수도 있지만, void 형태로 맞추는 것이 중요)
// 가장 중요한 것은 Shader Graph가 PixelOutlineMask_float2라는 이름으로 void (float2, float, out float) 시그니처를 찾도록 하는 것입니다.

// Custom Function 노드의 이름이 PixelOutlineMask_float2 일 때, Shader Graph는
// PixelOutlineMask_float2(uv, widthPX, Out); 와 같은 호출을 생성하려 합니다.
// 따라서 HLSL에도 이와 일치하는 함수가 필요합니다.
// 생성된 코드에서 PixelOutlineMask_float2_float 으로 호출하고 있으므로, 그 이름에 맞춰줍니다.

void PixelOutlineMask_float2_float(float2 uv, float widthPX, out float Out)
{
    Out = PixelOutline_Core(uv, widthPX);
}

// 만약 다른 래퍼 함수들도 Shader Graph에서 사용한다면 유사하게 수정 필요
// 예시: void PixelOutlineMask_float_float(float uv, float widthPX, out float Out) { Out = PixelOutline_Core(float2(uv,0), widthPX); }


// 다른 래퍼 함수들은 일단 그대로 두거나, 필요하다면 유사한 방식으로 void 형태로 변경합니다.
// 하지만 지금 가장 중요한 것은 생성된 코드에서 호출하는 PixelOutlineMask_float2_float 함수를
// 올바른 시그니처로 제공하는 것입니다.

float PixelOutlineMask_float   (float  uv, float  widthPX) {return PixelOutline_Core(float2(uv,0),   widthPX);}
// float PixelOutlineMask_float2  (float2 uv, float  widthPX) {return PixelOutline_Core(uv,             widthPX);} // 원본은 주석처리
float PixelOutlineMask_float4  (float4 uv, float  widthPX) {return PixelOutline_Core(uv.xy,          widthPX);}
float PixelOutlineMask_float_float_preview(float uv, float widthPX){return PixelOutline_Core(float2(uv,0),   widthPX);} // 프리뷰용은 다른 이름 사용 권장

#endif