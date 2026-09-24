// Flat-shaded voxel material: vertex colour × (ambient + directional), no textures.
// Lives in Resources so it ships in every build without a material asset.
Shader "Ronriku/VoxelLit"
{
    Properties
    {
        _LightDir ("Light direction (world)", Vector) = (0.35, 0.9, -0.45, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.55
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _LightDir;
            float _Ambient;
            fixed4 _Tint;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float l = saturate(dot(n, normalize(_LightDir.xyz)));
                float shade = _Ambient + (1.0 - _Ambient) * l;
                o.color = fixed4(v.color.rgb * shade * _Tint.rgb, 1);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
