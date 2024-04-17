Shader "Custom/IndividualShell" {
    SubShader {
        Tags {
            "LightMode" = "ForwardBase"
        }

        Pass {
            // need backface culling off because hair is see-through!
            Cull Off

                CGPROGRAM

                // vertex and fragment shader names
#pragma vertex vp
#pragma fragment fp

                // these don't flat-out do the lighting, see below for our actual lighting
#include "UnityPBSLighting.cginc" // this gives us UnityObjectToWorldNormal

                struct VertexData {
                    float4 vertex : POSITION;
                    float3 normal : NORMAL;
                    float2 uv : TEXCOORD0;
                };

            struct world_vertex { // puts everything in world position for the fragment shader
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1; // world position now
                float3 worldPos : TEXCOORD2; // calculated externally
            };

            int _ShellIndex; // current shell layer being acted on
            int _NumShells;
            float _ShellDistance; // distance covered by a shell
            float _Density; // initializes the noise with more or less seeds
            float _Thickness;
            float _Occlusion; // exponentially darkens lower shells to approximate light occlusion
            float _OcclusionBias; // Adds a constant to ambient occlusion so the lighting is less harsh and it looks like in-scattering is happening
            float _HeightBias;
            float _Curvature; // in practice it's just stiffness
            float _DisplacementStrength; // complicated
            float3 _ShellColor; // somehow also complicated!
            float3 _DisplacementVector;
            float _DebugOutVal;

            // it seems that all performant programs use a hash function to generate noise. Here is one recommended by a graphics guy online
            float hash(uint n) {
                n = (n << 13U) ^ n;
                n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
                return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
            }
            // TODO: try perlin's ImprovedNoise function:
            //https://cs.nyu.edu/~perlin/noise/
            // another option is simplex noise
            // or just ANYTHING on shadertoy

            //vertex shader
            world_vertex vp(VertexData v) {
                world_vertex i;
                i.uv = v.uv;
                i.normal = normalize(UnityObjectToWorldNormal(v.normal));

                // normalize the shell hight between 0 and 1 and apply the bias
                float shellHeight = (float)_ShellIndex / (float)_NumShells;
                shellHeight = pow(shellHeight, _HeightBias);

                // we can't just set our y value to be an addition of the surface anymore, so we calculate a normal to the object
                v.vertex.xyz += v.normal.xyz * _ShellDistance * shellHeight;

                // this is how we get our "physics"
                float k = pow(shellHeight, _Curvature); // power affects tips more than base
                v.vertex.xyz += _DisplacementVector * k * _DisplacementStrength; 
                //v.vertex.xyz += _DisplacementVector * k * _DisplacementStrength * -1; // invert gravity

                i.pos = UnityObjectToClipPos(v.vertex);
                i.worldPos = mul(unity_ObjectToWorld, v.vertex);

                return i;
            }

            // fragment shader
            float4 fp(world_vertex i) : SV_TARGET {
                // you have to do this multiplication to get more than exactly one strand of hair
                float2 newUV = i.uv *_Density;

                // just a ton of typeCasts to make everything line up nicely
                uint2 tid = newUV;
                uint seed = tid.x + 100 * tid.y + 100 * 2; // messing with these parameters creates interesting combing
                float shellIndex = _ShellIndex;
                float shellCount = _NumShells;
                float h = shellIndex / shellCount; // h is (0-1) so we can do 1-h

                float2 localUV = frac(newUV) * 2 - 1; //local UV is from -1 to 1

                //<sometype> angle = angle(localUV) // figure out how to calculate this
                // do some math to multiply h bu the angle so we get a spirally structure!

                float localDistanceFromCenter = length(localUV);

                float rand = hash(seed);
                // float rand = 0.5; // just for testing

                // some better options for whatnot
                float h_pow = pow(h, 1);
                float h_mult = h_pow * 30; // sin 10
                float sin_h = h * sin(h_mult);


                //rand is the random value under us
                // (rand - h) is (0-1) - 10-1

                // discard pixels that aren't in the hair thickness (this is what gives us round tapered strands)

                int out_of_scope = (localDistanceFromCenter) > (_Thickness * (rand - h)); // this is hair
                //int out_of_scope = (localDistanceFromCenter) > (_Thickness * (rand - (1-h))); // inverted structure
                //int out_of_scope = (localDistanceFromCenter) > (_Thickness * (rand - (sin(30 * pow(h,1))))); // bubbles?
                //int out_of_scope = (localDistanceFromCenter) > (_Thickness * (rand -sin_h)); // wavies
                // to add curls, you'll need something to do with the angle on LocalDistanceFromCenter
                if (out_of_scope && _ShellIndex > 0) discard;

                // lighting stolen from valve, it's their half-lambert
                float half_lambert = DotClamped(i.normal, _WorldSpaceLightPos0) * 0.5f + 0.5f;
                half_lambert = half_lambert * half_lambert;

                // occlusion
                float ambientOcclusion = pow(h, _Occlusion);
                ambientOcclusion += _OcclusionBias;
                // TODO: can we do something to offset this occlusion for the base of strands on "top"? They shouldn't have any shadows on them!

                ambientOcclusion = saturate(ambientOcclusion);

                // to see how it changes the lighting and shadowing.
                return float4(_ShellColor * half_lambert * ambientOcclusion, 1.0);
            }

            ENDCG
        }
    }
}
