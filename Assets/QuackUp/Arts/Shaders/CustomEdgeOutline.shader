Shader "Sprites/CustomOutlinePerObject_Fixed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 0.5)) = 0.05
        
        // Default all edges to ON
        [Toggle] _OutlineTop ("Top Outline", Float) = 1
        [Toggle] _OutlineBottom ("Bottom Outline", Float) = 1
        [Toggle] _OutlineLeft ("Left Outline", Float) = 1
        [Toggle] _OutlineRight ("Right Outline", Float) = 1
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _SpriteUVRect;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineTop;
            float _OutlineBottom;
            float _OutlineLeft;
            float _OutlineRight;
            
            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // Sliced sprites occupy only part of the texture UV range.
                float2 localUV = (IN.texcoord - _SpriteUVRect.xy)
                    / max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(0.000001, 0.000001));
                float distToLeft = localUV.x;
                float distToRight = 1.0 - localUV.x;
                float distToBottom = localUV.y;
                float distToTop = 1.0 - localUV.y;
                
                // Check ALL edges explicitly (no shader_feature)
                bool showTop = (_OutlineTop > 0.5) && (distToTop < _OutlineWidth);
                bool showBottom = (_OutlineBottom > 0.5) && (distToBottom < _OutlineWidth);
                bool showLeft = (_OutlineLeft > 0.5) && (distToLeft < _OutlineWidth);
                bool showRight = (_OutlineRight > 0.5) && (distToRight < _OutlineWidth);
                
                if (showTop || showBottom || showLeft || showRight) {
                    return _OutlineColor;
                }
                
                return c;
            }
            ENDCG
        }
    }
}