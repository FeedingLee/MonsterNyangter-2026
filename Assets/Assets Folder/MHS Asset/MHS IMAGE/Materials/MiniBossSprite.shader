// 중간보스 몸 전용: 흐릿한 반투명 테두리(알파가 _Cutoff 미만)는 잘라 내고 남은 픽셀은 불투명하게 그림
// → 어두운 그림이 바닥과 섞여 반투명·흐리게 보이는 것을 막음. 정점 색(SpriteRenderer.color)의 알파는 그대로 적용 (사망 시 서서히 사라짐)
Shader "MonNyang/MiniBoss Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cutoff ("잘라 낼 알파", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed _Cutoff;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                clip(c.a - _Cutoff);
                c.a = 1;
                return c * i.color;
            }
            ENDCG
        }
    }
}
