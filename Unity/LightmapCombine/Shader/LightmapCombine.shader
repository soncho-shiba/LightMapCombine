Shader "LightmapCombine"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR]_Lightmap ("Lightmap", 2D) = "white" {}
        _ScaleFactor ("Scale Factor", Range(1, 10)) = 2
        _ScaleU ("Scale U", Float) = 1.0
        _ScaleV ("Scale V", Float) = 1.0
        _OffsetU ("Offset U", Float) = 0.0
        _OffsetV ("Offset V", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {   
//            //RenderTexture書き込み用
//            Cull off
//            ZTest Always
//            ZWrite Off
            
            //Debug用
            Cull Back
            ZTest Less
            ZWrite On

            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _Lightmap;
            float4 _Lightmap_ST;
            float4 _Lightmap_HDR;
            
            float _ScaleU;
            float _ScaleV;
            float _OffsetU;
            float _OffsetV;

            float _ScaleFactor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Main texture color
                fixed4 mainCol = tex2D(_MainTex, i.uv);
                
                // Lightmap UVs
                float2 lightmapUV = i.uv;
                lightmapUV.x = lightmapUV.x *_ScaleU + _OffsetU;
                lightmapUV.y = lightmapUV.y * _ScaleV + _OffsetV;

                // Lightmap color
                fixed4 lightmapCol = tex2D(_Lightmap, lightmapUV);
                lightmapCol.rgb = DecodeHDR(lightmapCol, _Lightmap_HDR);
                
                // Combine main texture and lightmap
                fixed4 finalCol = mainCol * (lightmapCol/_ScaleFactor);
 
                return fixed4(finalCol.xyz,mainCol.w);
            }
            ENDCG
        }
    }
}
