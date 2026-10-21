Shader "VertigoWheel/UI/BoardBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BaseColor ("Charcoal", Color) = (.035,.028,.040,1)
        _WarmColor ("Warm Haze", Color) = (.25,.105,.095,1)
        _CoolColor ("Plum Haze", Color) = (.095,.075,.12,1)
        _Vignette ("Edge Darkness", Range(0,1)) = .8
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
            fixed4 _Color, _BaseColor, _WarmColor, _CoolColor;
            float _Vignette;
            float4 _ClipRect;
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
                // Broad static light fields evoke the reference's defocused warm room.
                // No texture distortion, per-frame animation, or offscreen blur pass is required.
                float2 p = input.uv;
                float2 center = (p - float2(.53,.44)) / float2(.38,.34);
                float2 red = (p - float2(.74,.43)) / float2(.22,.25);
                float2 plum = (p - float2(.38,.57)) / float2(.38,.3);
                float warm = exp2(-dot(center, center) * 1.8);
                float ember = exp2(-dot(red, red) * 2.2);
                float cool = exp2(-dot(plum, plum) * 1.6);
                float2 edge = (p - .5) * 2;
                float vignette = 1 - _Vignette * smoothstep(.25, 1.4, dot(edge,edge));
                fixed3 rgb = _BaseColor.rgb + _WarmColor.rgb * (warm * .72 + ember * .5)
                    + _CoolColor.rgb * cool * .5;
                fixed4 color = fixed4(rgb * vignette, 1) * input.color;
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

