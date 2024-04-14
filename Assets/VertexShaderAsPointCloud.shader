Shader "Custom/PointCloudMagic" { // I didn't rename it, should I rename it? I don't know!
    SubShader {
        Tags {
            "LightMode" = "ForwardBase"
        }

        Pass {
            // need backface culling off because hair is see-through!
            Cull Off

                CGPROGRAM

                // vertex and fragment shader
#pragma vertex vp
#pragma fragment fp

                // these don't flat-out do the lighting, see below for our actual lighting
#include "UnityPBSLighting.cginc"
#include "AutoLight.cginc"

                // This is the struct that holds all the data that vertices contain when being passed into the gpu, such as the initial vertex position,
                // the normal, and the uv coordinates
                struct VertexData {
                    float4 vertex : POSITION;
                    float3 normal : NORMAL;
                    float2 uv : TEXCOORD0;
                };

            // everything the vertex shader passes to the fragment shader
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1; // world position now
                float3 worldPos : TEXCOORD2; // calculated externally
            };

            int _ShellIndex; // current shell layer being acted on
            int _ShellCount;
            float _ShellLength; // distance covered by a shell
            float _Density;  // initializes the noise with more or less seeds
            float _NoiseMin, _NoiseMax;
            float _Thickness;
            float _Attenuation; // exponent on shell height. This gives a fake ambient occlusion effect.
            float _OcclusionBias; // very important. Adds a constant to ambient occlusion so the lighting is less harsh and it looks like in-scattering is happening
            float _ShellDistanceAttenuation;
            float _Curvature; // in practice it's just stiffness
            float _DisplacementStrength; // complicated
            float3 _ShellColor; // somehow also complicated!
            float3 _ShellDirection; // The direction that shells move. Usually gravity but if an object is moving you want to change this. CPU does this per-frame
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
            v2f vp(VertexData v) {
                v2f i;
                i.uv = v.uv;
                i.normal = normalize(UnityObjectToWorldNormal(v.normal));

                // normalize the shell hight between 0 and 1 and apply the attenuation (to bias more shells towards the base)
                float shellHeight = (float)_ShellIndex / (float)_ShellCount;
                shellHeight = pow(shellHeight, _ShellDistanceAttenuation);

                // we can't just set our y value to be an addition of the surface anymore, so we calculate a normal to the object
                v.vertex.xyz += v.normal.xyz * _ShellLength * shellHeight;

                // this is how we get our "physics"
                float k = pow(shellHeight, _Curvature); // power affects tips more than base
                v.vertex.xyz += _ShellDirection * k * _DisplacementStrength; 

                i.pos = UnityObjectToClipPos(v.vertex);
                i.worldPos = mul(unity_ObjectToWorld, v.vertex);

                return i;
            }

            // fragment shader
            float4 fp(v2f i) : SV_TARGET {
                // you have to do this multiplication to get more than exactly one strand of hair
                float2 newUV = i.uv *_Density;

                // just a ton of typeCasts to make everything line up nicely
                uint2 tid = newUV;
                uint seed = tid.x + 100 * tid.y + 100 * 10;
                float shellIndex = _ShellIndex;
                float shellCount = _ShellCount;
                float h = shellIndex / shellCount; // normalize again

                float2 localUV = frac(newUV) * 2 - 1; //local UV is from -1 to 1
                float localDistanceFromCenter = length(localUV);

                float rand = hash(seed); // whether there's a seed under me

                // see if you are not a hair strand
                int notStrand = (localDistanceFromCenter) > (_Thickness * (rand - h));
                if (notStrand && _ShellIndex > 0){
                    // not part of a hair strand. For shell texturing we would discard it, but let's see if we can't do something fun here:

                    if (h < 0.5){
                        if (localUV.x > -0.9 && localUV.x < 0.1 && localUV.y > -0.9 && localUV.y < 0.1){ // if it's orthogonally close
                            int rsta = 4;
                        }
                        else discard;
                    }
                    else discard;
                }

                // lighting stolen from valve, it's their half-lambert
                float ndotl = DotClamped(i.normal, _WorldSpaceLightPos0) * 0.5f + 0.5f;
                ndotl = ndotl * ndotl;

                // occlusion
                float ambientOcclusion = pow(h, _Attenuation);
                ambientOcclusion += _OcclusionBias;
                // TODO: can we do something to offset this occlusion for the base of strands on "top"? They shouldn't have any shadows on them!
                // something to do with making the occlusion affect not just the height but height * displacement would be smart -- but the displacement is not yet sent to the fragment shader

                ambientOcclusion = saturate(ambientOcclusion);

                // to see how it changes the lighting and shadowing.
                return float4(_ShellColor * ndotl * ambientOcclusion, 1.0);
            }

            ENDCG
        }
    }
}
