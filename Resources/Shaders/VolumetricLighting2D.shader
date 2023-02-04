Shader "Hidden/VolumetricLighting2D/VolumetricLighting2D"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Samples("Samples", Range(0,256)) = 128
        _BlurWidth("Blur Width", Range(0,1)) = 0.85
        _Intensity("Intensity", Range(0,1)) = 1
        _Center("Center", Vector) = (0.5,0.5,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #define MAX_SAMPLES 256

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            int _Samples;
            float _BlurWidth;
            float _Intensity;
            float2 _Center;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 color = fixed4(0.0f, 0.0f, 0.0f, 1.0f);

                float2 ray = i.uv - _Center.xy;
                int numSamples = min(_Samples, MAX_SAMPLES);
                for (int i = 0; i < numSamples; i++)
                {
                    float scale = 1.0f - _BlurWidth * (float(i) / float(numSamples - 1));
                    color.xyz += tex2D(_MainTex, (ray * scale) + _Center.xy).xyz / float(numSamples);
                }

                return color * _Intensity;
            }
            ENDCG
        }
    }
}
