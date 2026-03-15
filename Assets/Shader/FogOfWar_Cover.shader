Shader "Custom/FOV_PZ_Night_Overlay" {
    Properties {
        _Color ("Night Color", Color) = (0,0,0,0.8)
    }
    SubShader {
        // Overlay giúp nó vẽ sau cùng, ZTest Always ép nó đè lên tất cả
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always 

        Pass {
            Stencil {
                Ref 1
                Comp NotEqual // Chỗ nào có Stencil 1 thì KHÔNG vẽ màu đen
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                return _Color;
            }
            ENDCG
        }
    }
}