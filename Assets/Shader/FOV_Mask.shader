Shader "Custom/FOV_Stencils_Window" {
    SubShader {
        // Vẽ thật sớm để chuẩn bị lỗ thủng
        Tags { "Queue"="Geometry-1" }
        ColorMask 0 // Không vẽ màu xám xịt như trong hình nữa
        ZWrite Off

        Pass {
            Stencil {
                Ref 1
                Comp Always
                Pass Replace
            }
        }
    }
}