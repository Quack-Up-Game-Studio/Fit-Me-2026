Shader "FitMe/CellBaseOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _CellSize ("Cell size", Vector) = (1,1,0,0)
        _Edges ("Exposed left right top bottom", Vector) = (1,1,1,1)
        _Diagonals ("Existing top-left top-right bottom-left bottom-right", Vector) = (0,0,0,0)
        _Thickness ("Outward thickness", Float) = .075
        _Radius ("Convex corner radius", Float) = .12
        _SeamInsetDepth ("Exterior join inset depth", Float) = .025
        _SeamInsetWidth ("Exterior join inset half-width", Float) = .16
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" "DisableBatching"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _CellSize, _Edges, _Diagonals;
            float _Thickness, _Radius, _SeamInsetDepth, _SeamInsetWidth;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 p:TEXCOORD0; fixed4 color:COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // The full-rect sprite is one unit wide; CPU expands geometry and bounds.
                o.p = v.vertex.xy * (_CellSize.xy + 2 * _Thickness);
                o.color = v.color;
                return o;
            }
            float roundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p = i.p;
                float2 beyond = step(_CellSize.xy * .5, abs(p));
                float diagonal = p.x < 0 ? (p.y < 0 ? _Diagonals.z : _Diagonals.x)
                    : (p.y < 0 ? _Diagonals.w : _Diagonals.y);
                // Do not paint a concave join into an existing diagonal cell, even
                // where its rounded fill is transparent. This is board topology,
                // not the alpha silhouette of separate individual sprites.
                clip(.5 - beyond.x * beyond.y * diagonal);
                float horizontal = p.x < 0 ? _Edges.x : _Edges.y;
                float vertical = p.y < 0 ? _Edges.w : _Edges.z;
                // Round only exposed convex corners. Shared corners stay square, so there
                // are no individual-cell outline scallops or red holes inside the board.
                float convex = horizontal * vertical;
                float radius = _Radius * convex;
                // Continue the analytic shape past shared sides. Clipping it at each
                // cell boundary causes AA notches along an otherwise continuous rim.
                float4 continuation = (1 - _Edges) * (_Thickness + _Radius);
                float2 innerShift = float2(continuation.y - continuation.x, continuation.z - continuation.w) * .5;
                float2 innerSize = _CellSize.xy * .5 + float2(continuation.x + continuation.y, continuation.z + continuation.w) * .5;
                float inner = roundedBox(p - innerShift, innerSize, radius);
                float2 shift = float2(_Edges.y - _Edges.x, _Edges.z - _Edges.w) * _Thickness * .5;
                float2 expansion = float2(_Edges.x + _Edges.y, _Edges.z + _Edges.w) * _Thickness * .5;
                float outer = roundedBox(p - innerShift - shift, innerSize + expansion,
                    radius + _Thickness * convex);
                // Only indent a straight exposed rim where its tangent neighbour exists
                // and the outside diagonal does not (the neighbour shares this rim).
                // The same symmetric smoothstep on both cells makes a rounded trough,
                // with zero slope at its centre and ends; never a triangular cut.
                if (_SeamInsetDepth > 0 && _SeamInsetWidth > 0)
                {
                    float2 distanceToJoin = abs(abs(p) - _CellSize.xy * .5);
                    float2 profile = 1 - smoothstep(0, _SeamInsetWidth, distanceToJoin);
                    float depth = min(_SeamInsetDepth, _Thickness * .8);
                    float insetY = depth * profile.x * vertical * (1 - horizontal) * (1 - diagonal);
                    float insetX = depth * profile.y * horizontal * (1 - vertical) * (1 - diagonal);
                    // Change only the outer contour. The inner ring boundary stays at
                    // the original fill bounds, and the depth cap leaves a solid rim.
                    if (insetY > 0) outer = max(outer, abs(p.y) - _CellSize.y * .5 - _Thickness + insetY);
                    if (insetX > 0) outer = max(outer, abs(p.x) - _CellSize.x * .5 - _Thickness + insetX);
                }
                float aa = max(fwidth(outer), .00001);
                // Subtract coverage (do not multiply masks): coincident shared edges
                // must cancel even at anti-aliased four-cell intersections.
                float alpha = saturate(smoothstep(-aa, aa, inner) - smoothstep(-aa, aa, outer));
                fixed4 color = i.color;
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}
