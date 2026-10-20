Shader "VertigoWheel/UI/SoftBorder"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Toggle] _Vignette ("Screen Edge Mode", Float) = 0
        _Aspect ("Rectangle Aspect", Float) = 1
        _HalfSize ("Inner Half Size In Height Units", Vector) = (.4,.4,0,0)
        _Radius ("Corner Radius", Float) = .025
        _Softness ("Edge Softness", Float) = .025
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            fixed4 _Color;
            float _Vignette, _Aspect, _Radius, _Softness;
            float4 _HalfSize, _ClipRect;
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float softness = max(_Softness, .0001);
                fixed4 color = input.color;
                if (_Vignette > .5)
                {
                    float edge = min(min(input.uv.x, 1 - input.uv.x), min(input.uv.y, 1 - input.uv.y));
                    color.rgb *= 1 - smoothstep(0, softness, edge);
                }
                else
                {
                    // Distance to a rounded rectangle, so the glow follows all four edges.
                    float2 position = (input.uv - .5) * float2(_Aspect, 1);
                    float2 q = abs(position) - _HalfSize.xy + _Radius;
                    float distance = length(max(q, 0)) + min(max(q.x, q.y), 0) - _Radius;
                    color.a *= exp2(-2 * distance * distance / (softness * softness));
                }
                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - .001h);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
