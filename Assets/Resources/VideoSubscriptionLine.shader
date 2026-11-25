Shader "Kodimodi/Video Subscription Line"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct VertOutput
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
            };

            VertOutput vert(appdata_full input)
            {
                VertOutput result;
                result.position = UnityObjectToClipPos(input.vertex);
                result.color = input.color;
                return result;
            }

            fixed4 frag(VertOutput input) : SV_Target
            {
                return input.color;
            }
            ENDCG
        }
    }
}
