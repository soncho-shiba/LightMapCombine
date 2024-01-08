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
           //RenderTexture書き込み用
            Cull off
            ZTest Always
            ZWrite Off
            
            //Debug用
//            Cull Back
//            ZTest Less
//            ZWrite On
           
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityGlobalIllumination.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normalWorld : TEXCOORD1;
                float4 vertex : SV_POSITION;
                
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _Lightmap;
            float4 _Lightmap_ST;
            half4 _Lightmap_HDR;
            
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
                o.normalWorld = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // MainTextureは 新しい値を取得
                // Main texture color
                fixed4 mainCol = tex2D(_MainTex, i.uv);
                
                // Lightmap UVs
                float2 lightmapUV = i.uv;
                lightmapUV.x = lightmapUV.x * _ScaleU + _OffsetU;
                lightmapUV.y = lightmapUV.y * _ScaleV + _OffsetV;

                // Lightmap color
                half4 bakedLightmapCol = tex2D(_Lightmap, lightmapUV);
                // 注意：プロジェクトをリニアワークフローに設定しておかないとライトマップテクスチャは sRGB とマークされないので、
                // シェーダーが使用する最終値 (サンプリングおよびデコード後) はリニア色空間にならない
                // 標準関数を使ってデコードしないとRGBMとsLDRの両対応がシンプルにできない
                bakedLightmapCol.rgb = DecodeLightmap(bakedLightmapCol, _Lightmap_HDR);
                
                fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER (unity_LightmapInd, unity_Lightmap, lightmapUV);
                
                // Calculate final color
                //没
                float lightIntensity = max(max(bakedLightmapCol.r, bakedLightmapCol.g), bakedLightmapCol.b);
                half3 finalCol = mainCol.rgb  * sqrt(bakedLightmapCol.rgb)  + sqrt(bakedLightmapCol.rgb) ;

                //half3 normalWorld = i.normalWorld;
                //half3 directionalLightMap = DecodeDirectionalLightmap ( bakedLightmapCol.rgb, bakedDirTex, normalWorld);
                //BRDFと同じように合成する
                //half3 finalCol = mainCol.rgb  * directionalLightMap.rgb + directionalLightMap.rgb ;
                
                // Use the alpha channel from the main texture
                return fixed4(finalCol, mainCol.w);
            }
            ENDCG
        }
    }
}
