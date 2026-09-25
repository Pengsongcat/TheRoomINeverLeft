Shader "Hidden/TRINL/TreeShadowCookie"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _FarLight;
        sampler2D _GlowTex;
        sampler2D _BandNear;
        sampler2D _BandMid;
        sampler2D _BandFar;

        // x = margin, y = domain size (1 + 2 * margin), z = one canopy pixel in domain units, w = seed offset
        float4 _Domain;
        // x = far cells across the domain, y = far leaf half length (cells), z = clumping, w = thin-crown threshold
        float4 _Canopy;
        // xy = wind direction, z = wind strength (already ramped), w = wind time
        float4 _WindA;
        // x = gustiness, y = gust time, z = flutter, w = branch sway
        float4 _WindB;
        // x = idle sway
        float4 _WindC;
        // x = sun intensity * exposure gain, y = shadow brightness, z = contrast, w = halation
        float4 _Tone;
        // y = edge fade
        float4 _Tone2;
        float _DiscRadius;
        float _DiscTaps;
        // Which channel of the source the disc pass reads.
        float4 _ChannelMask;
        // x = radius jitter, y = along-wind stretch; both already scaled by the current wind
        float4 _DiscJitter;

        // Hash without sine (Dave Hoskins): stable on mobile GPUs where sin() hashes band.
        float Hash12(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float3 Hash32(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yxz + 33.33);
            return frac((p3.xxy + p3.yzz) * p3.zyx);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash12(i);
            float b = Hash12(i + float2(1, 0));
            float c = Hash12(i + float2(0, 1));
            float d = Hash12(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // Gusts travel across the canopy along the wind, so neighbouring leaves move together
        // and the motion reads as waves instead of every leaf jittering on its own.
        float Gust(float2 p)
        {
            float2 dir = _WindA.xy;
            float2 perp = float2(-dir.y, dir.x);
            float n = ValueNoise(float2(dot(p, dir) * 2.5 - _WindB.y, dot(p, perp) * 1.2 + 7.3));
            return smoothstep(0.3, 0.85, n);
        }

        float WindAmp(float2 p)
        {
            return _WindA.z * lerp(1.0, Gust(p) * 2.0, _WindB.x) + _WindC.x;
        }

        // Large scale variation in leaf size, so the canopy is not uniform.
        float Clump(float2 p)
        {
            float n = ValueNoise(p * 3.1 + _Domain.w) * 0.65 + ValueNoise(p * 7.3 + _Domain.w * 1.7) * 0.35;
            return lerp(1.0, 0.75 + 0.5 * n, _Canopy.z);
        }

        // Crown thickness. Gaps only survive where the crown is thin, which keeps them sparse
        // and grouped: each sun disc then holds zero or one gap and reads as a separate spot.
        float Thin(float2 p)
        {
            float n = ValueNoise(p * 9.0 + _Domain.w * 3.1) * 0.6 + ValueNoise(p * 21.0 + _Domain.w * 5.3) * 0.4;
            return smoothstep(_Canopy.w, _Canopy.w + 0.25, n);
        }

        // Whole clusters swing on their branch before individual leaves flutter.
        float2 BranchWarp(float2 p)
        {
            float amp = WindAmp(p) * _WindB.w;
            float t = _WindA.w;
            float2 wobble = float2(ValueNoise(p * 2.7 + float2(0.0, t * 0.5)),
                                   ValueNoise(p * 2.7 + float2(13.7, 5.1) + float2(t * 0.45, 0.0))) - 0.5;
            // About 0.25 Hz at wind speed 1: branches swing slower than the leaves on them.
            float swing = sin(t * 1.6 + dot(p, float2(3.7, 2.9)));
            // Warping the domain moves whole clusters, so it can be large without clipping leaves.
            return p + (_WindA.xy * swing * 0.035 + wobble * 0.03) * amp;
        }

        // Transmission of one layer of scattered leaves, 1 = open sky. p is in domain units.
        // lenScale is evaluated once per pixel by the caller; it varies slowly enough that
        // leaves do not visibly distort across it, and it saves two noise lookups per leaf.
        float LeafLayer(float2 p, float cells, float halfLen, float aspect, float chance, float2 layerSeed, float lenScale)
        {
            float2 g = p * cells;
            float2 id = floor(g);
            float pix = _Domain.z * cells;
            float trans = 1.0;
            [unroll] for (int y = -1; y <= 1; y++)
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float2 c = id + float2(x, y);
                float3 h = Hash32(c + layerSeed);
                float3 k = Hash32(c + layerSeed + 57.31);
                if (k.x > chance) continue;

                float2 center = c + 0.2 + h.xy * 0.6;
                float2 centerDomain = center / cells;
                float amp = WindAmp(centerDomain);
                float phase = k.y * 6.2831853;
                // Roughly 0.4 to 1 Hz sway per leaf at wind speed 1; flutter below runs faster.
                float t = _WindA.w * lerp(2.5, 6.0, k.z) + phase;

                float angle = h.z * 6.2831853 + amp * 0.6 * sin(t);
                center += _WindA.xy * amp * 0.22 * (0.5 + 0.5 * sin(t * 0.71 + h.x * 6.2831853));
                // Leaves tilting toward and away from edge-on make the spots flicker. The swing is
                // centred on the rest width so wind does not change the overall exposure.
                float width = aspect * (1.0 + 0.45 * sin(t * 2.3 + phase) * saturate(amp * _WindB.z));
                float len = halfLen * lerp(0.6, 1.0, k.z) * lenScale;
                if (len < 1e-3) continue;

                float2 d = g - center;
                float s, co;
                sincos(angle, s, co);
                float2 q = float2(co * d.x + s * d.y, -s * d.x + co * d.y);
                // Pointed tips instead of a plain ellipse.
                float taper = 1.0 - 0.45 * saturate(abs(q.x) / len);
                float halfW = max(len * width * taper, 1e-4);
                float e = length(float2(q.x / len, q.y / halfW));
                // Analytic antialiasing: approximate edge distance in canopy pixels.
                float cov = saturate(0.5 + (1.0 - e) * halfW / pix);
                trans *= 1.0 - cov;
            }
            return trans;
        }

        float2 Rotate(float2 p, float a)
        {
            float s, c;
            sincos(a, s, c);
            return float2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        // Far canopy only; the tree in front of it is real geometry, see the Tree pass.
        float4 FragCanopy(v2f_img i) : SV_Target
        {
            float2 p = i.uv * _Domain.y - _Domain.x;
            float2 pw = BranchWarp(p);

            // A real canopy is many overlapping leaves: four offset, rotated layers close it so
            // only small gaps remain. Those gaps are what become round sun spots.
            //
            // Where the crown is thin the leaves shrink, so the gaps there grow toward the size of
            // the sun disc and project bright spots; elsewhere the crown stays shut. That gives
            // the clusters of overlapping spots with dark shade between them seen in photos.
            float thin = Thin(pw);
            float lenScale = Clump(pw) * lerp(1.0, 0.75, thin * _Canopy.z);
            float cells = _Canopy.x;
            float len = _Canopy.y;
            float far = LeafLayer(pw, cells, len, 0.46, 1.0, float2(_Domain.w, 11.0), lenScale);
            far *= LeafLayer(Rotate(pw, 0.6) + 0.37, cells * 1.37, len, 0.44, 1.0, float2(_Domain.w, 29.0), lenScale);
            far *= LeafLayer(Rotate(pw, 1.9) + 0.71, cells * 1.83, len, 0.42, 1.0, float2(_Domain.w, 37.0), lenScale);
            far *= LeafLayer(Rotate(pw, 2.7) + 0.19, cells * 0.74, len, 0.50, 0.7, float2(_Domain.w, 43.0), lenScale);
            // Thick crown blocks nearly everything; otherwise the shade turns into a flat grey haze.
            far *= lerp(1.0, thin, saturate(_Canopy.z * 1.4));
            return float4(far, 1, 1, 1);
        }

        // Dual filtering (Kawase), the same kernels as ScreenDiffusion.
        float4 FragDown(v2f_img i) : SV_Target
        {
            float2 h = _MainTex_TexelSize.xy;
            float4 s = tex2D(_MainTex, i.uv) * 4.0;
            s += tex2D(_MainTex, i.uv - h);
            s += tex2D(_MainTex, i.uv + h);
            s += tex2D(_MainTex, i.uv + float2(h.x, -h.y));
            s += tex2D(_MainTex, i.uv - float2(h.x, -h.y));
            return s * 0.125;
        }

        float4 FragUp(v2f_img i) : SV_Target
        {
            float2 h = _MainTex_TexelSize.xy * 0.5;
            float4 s = tex2D(_MainTex, i.uv + float2(-h.x * 2.0, 0.0));
            s += tex2D(_MainTex, i.uv + float2(-h.x, h.y)) * 2.0;
            s += tex2D(_MainTex, i.uv + float2(0.0, h.y * 2.0));
            s += tex2D(_MainTex, i.uv + float2(h.x, h.y)) * 2.0;
            s += tex2D(_MainTex, i.uv + float2(h.x * 2.0, 0.0));
            s += tex2D(_MainTex, i.uv + float2(h.x, -h.y)) * 2.0;
            s += tex2D(_MainTex, i.uv + float2(0.0, -h.y * 2.0));
            s += tex2D(_MainTex, i.uv + float2(-h.x, -h.y)) * 2.0;
            return s * (1.0 / 12.0);
        }

        // Every gap is a pinhole that projects the sun, so the light is the transmission averaged
        // over the sun's disc. A uniform disc (not a gaussian) is what gives komorebi its round,
        // similar-sized spots.
        //
        // In wind, leaves and branches swing toward and away from the wall, which changes their
        // penumbra: each patch pulses between sharper and blurrier at its own pace (about 0.35 Hz
        // at wind speed 1, neighbours out of step), harder where a gust is passing. The disc also
        // stretches along the wind, like motion blur on the moving shadow.
        float4 FragDisc(v2f_img i) : SV_Target
        {
            float time = _WindA.w;
            float2 p = i.uv * _Domain.y - _Domain.x;
            float gust = lerp(1.0, Gust(p) * 2.0, _WindB.x);
            float n = ValueNoise(i.uv * 5.0 - _WindA.xy * time * 0.35 + _Domain.w);
            float pulse = sin(time * 2.2 + n * 6.2831853);
            float n2 = ValueNoise(i.uv * 9.0 - _WindA.xy * time * 0.5 + _Domain.w * 1.3);
            float radius = _DiscRadius * max(0.35, 1.0 + _DiscJitter.x * gust * pulse);
            float stretch = 1.0 + _DiscJitter.y * gust * n2;
            float2 dir = _WindA.xy;
            float2 perp = float2(-dir.y, dir.x);

            int taps = (int)_DiscTaps;
            float sum = 0.0;
            [loop] for (int k = 0; k < taps; k++)
            {
                float t = (k + 0.5) / taps;
                float a = k * 2.39996323;
                float2 o = float2(cos(a), sin(a)) * sqrt(t);
                o = dir * (dot(o, dir) * stretch) + perp * dot(o, perp);
                sum += dot(tex2Dlod(_MainTex, float4(i.uv + o * radius, 0, 0)), _ChannelMask);
            }
            return (sum / taps).xxxx;
        }

        float TotalLight(float2 uv)
        {
            return tex2D(_FarLight, uv).r * tex2D(_BandNear, uv).r * tex2D(_BandMid, uv).r * tex2D(_BandFar, uv).r;
        }

        // Half-resolution product of every layer, only used as the source of the halation.
        float4 FragCombine(v2f_img i) : SV_Target
        {
            return TotalLight(i.uv).xxxx;
        }

        // Only the bright spots bloom, so halation never lifts the shade.
        // Thresholded after exposure, i.e. on what the spot will actually look like.
        float4 FragBright(v2f_img i) : SV_Target
        {
            float v = tex2D(_MainTex, i.uv).r * _Tone.x;
            return saturate(v - 0.6).xxxx;
        }

        // Linear up to 0.8, then rolls off, so overlapping spots saturate softly instead of clipping.
        float SoftClip(float x)
        {
            return x < 0.8 ? x : 0.8 + 0.2 * (1.0 - exp(-(x - 0.8) * 5.0));
        }

        float4 FragCompose(v2f_img i) : SV_Target
        {
            float2 cuv = (i.uv + _Domain.x) / _Domain.y;
            float glow = tex2D(_GlowTex, cuv).r;
            // _Tone.x is sun intensity times the exposure gain: a single small gap only passes a
            // fraction of the sun disc, and photos of leaf light are exposed for the spots.
            float lit = TotalLight(cuv) * _Tone.x + glow * _Tone.w;
            lit = SoftClip(pow(max(lit, 0.0), _Tone.z));
            float v = lerp(_Tone.y, 1.0, saturate(lit));

            // Optional round falloff, handy for spot light cookies.
            float2 c = i.uv * 2.0 - 1.0;
            v *= lerp(1.0, 1.0 - smoothstep(0.6, 1.0, length(c)), _Tone2.y);
            // Same value in every channel: VLB can read any channel, URP reads colour.
            return v.xxxx;
        }

        // ---- Tree: branches and leaves drawn as geometry -------------------------------------

        struct TreeIn { float4 vertex : POSITION; float4 uv : TEXCOORD0; float4 color : COLOR; };
        struct TreeOut { float4 pos : SV_POSITION; float4 uv : TEXCOORD0; float4 color : COLOR; };

        TreeOut VertTree(TreeIn v)
        {
            TreeOut o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            o.color = v.color;
            return o;
        }

        // Leaf space: y runs from the attach point (-1.4) through the blade to the tip (+1).

        // One pointed lobe radiating from the blade centre. angle is measured from the tip
        // direction (+y); t runs from -1 at the centre to +1 at the lobe tip.
        float LobeSD(float2 q, float angle, float len, float halfW)
        {
            float s, c;
            sincos(angle, s, c);
            float2 dir = float2(s, c);
            float u = dot(q, dir);
            float v = dot(q, float2(dir.y, -dir.x));
            float h = len * 0.5;
            float t = (u - h) / h;
            float w = halfW * (1.0 - 0.45 * saturate(t));
            return (length(float2(t, v / max(w, 1e-3))) - 1.0) * min(h, w);
        }

        // Maple: five pointed lobes on a round centre. Not radially symmetric like a star: the top
        // lobe points up, the main side lobes splay about 48 degrees, and the two short lower
        // lobes reach out sideways, just above the stem.
        float MapleSD(float2 p)
        {
            float2 q = p - float2(0.0, -0.1);
            float d = length(q) - 0.3;
            d = min(d, LobeSD(q, 0.0, 1.0, 0.24));
            d = min(d, LobeSD(q, 0.84, 0.9, 0.22));
            d = min(d, LobeSD(q, -0.84, 0.9, 0.22));
            d = min(d, LobeSD(q, 1.75, 0.58, 0.17));
            d = min(d, LobeSD(q, -1.75, 0.58, 0.17));
            // Serrated edge, faded out toward the centre where there is no edge.
            float th = atan2(q.x, q.y);
            return d + 0.03 * sin(th * 34.0) * saturate(length(q) * 2.0);
        }

        // Plain leaf, narrowing to a point at the tip.
        float OvateSD(float2 p)
        {
            float halfW = 0.55 * (1.0 - 0.3 * p.y);
            return (length(float2(p.x / halfW, p.y)) - 1.0) * 0.55;
        }

        // Petiole from the twig into the blade.
        float StemSD(float2 p)
        {
            return p.y < -0.35 ? abs(p.x) - 0.035 : 1.0;
        }

        // Multiplied into the target (Blend DstColor Zero): each channel is one depth band and
        // ends up as that band's transmission. The vertex colour says how much of this element
        // belongs to the near / mid / far band.
        float4 FragTree(TreeOut i) : SV_Target
        {
            float cov;
            if (i.uv.z < 0.5)
            {
                // Branch strip: uv.x runs across the width. Branches thinner than a pixel fade
                // with their width instead of flickering in and out.
                float fw = max(fwidth(i.uv.x), 1e-4);
                cov = saturate(0.5 + (1.0 - abs(i.uv.x)) / fw) * saturate(2.0 / fw);
            }
            else
            {
                float blade = i.uv.z < 1.5 ? MapleSD(i.uv.xy) : OvateSD(i.uv.xy);
                float sd = min(blade, StemSD(i.uv.xy));
                cov = saturate(0.5 - sd / max(fwidth(sd), 1e-4));
            }
            return float4(1.0 - cov * i.color.rgb, 1.0);
        }
        ENDCG

        Pass
        {
            Name "Canopy"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragCanopy
            #pragma target 3.5
            ENDCG
        }
        Pass
        {
            Name "Down"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }
        Pass
        {
            Name "Up"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }
        Pass
        {
            Name "Disc"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDisc
            #pragma target 3.5
            ENDCG
        }
        Pass
        {
            Name "Compose"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragCompose
            #pragma target 3.5
            ENDCG
        }
        Pass
        {
            Name "Bright"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBright
            ENDCG
        }
        Pass
        {
            Name "Tree"
            Blend DstColor Zero
            CGPROGRAM
            #pragma vertex VertTree
            #pragma fragment FragTree
            #pragma target 3.5
            ENDCG
        }
        Pass
        {
            Name "Combine"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragCombine
            #pragma target 3.5
            ENDCG
        }
    }
}
