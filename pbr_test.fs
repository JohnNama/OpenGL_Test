#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
in vec3 WorldPos;
in vec3 Normal;

// material parameters
uniform vec3 albedo;
uniform float metallic;
uniform float roughness;
uniform float ao;
uniform vec3 diffuse;
uniform int sampleCount;
uniform float anisotropic;
// uniform vec3 ior;
// lights
//uniform vec3 lightpositions[4];
//uniform vec3 lightcolors[4];
//uniform int lightnumber;
uniform sampler2D perlinNoise;
uniform sampler2D tangentMap;
uniform sampler2D diffuseMap;

uniform vec3 camPos;
uniform int sampleNumber;
// uniform sampler2D albedoMap;

uniform vec3 lightDirection;

#define SAMPLE_COUNT 1024
#define BLOCKER_SEARCH_NUM_SAMPLES SAMPLE_COUNT
#define PCF_NUM_SAMPLES SAMPLE_COUNT
#define NUM_RINGS 20

#define PI 3.141592653589793
#define PI2 6.283185307179586
#define a2 roughness * roughness
vec3 sample_vectors[SAMPLE_COUNT];

float D_GGXaniso(vec3 H, vec3 N, float roughness)
{
    float aspect = sqrt(1.0 - 0.95 * anisotropic);
    float ax = a2 / aspect;
    float ay = a2 * aspect;

    vec3 up_vector =  N.z < 0.99999 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    vec3 T = cross(up_vector, N); // tangent T
    vec3 B = cross(N, T);   // bitangent B
    float XoH = dot(T, H);
    float YoH = dot(B, H);
    float NoH = dot(N, H);

    float d = XoH*XoH / (ax*ax) + YoH*YoH / (ay*ay) + NoH*NoH;
	return 1.0f / ( PI * ax*ay * d*d );
}

float Vis_Charlie_L(float x, float r)
{
	r = clamp(r, 0.0, 1.0);
	r = 1.0 - (1. - r) * (1. - r);

	float a = mix(25.3245 , 21.5473 , r);
	float b = mix( 3.32435,  3.82987, r);
	float c = mix( 0.16801,  0.19823, r);
	float d = mix(-1.27393, -1.97760, r);
	float e = mix(-4.85967, -4.32054, r);

	return a * 1 / ( (1 + b * pow(x, c)) + d * x + e);
}

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float alpha = pow(roughness, 2.0);
    float numerator = pow(alpha, 2.0);
    float NdotH = dot(N, H); // NdotH (cos(theta))
    float denominator = PI * pow((pow(NdotH, 2.0) * (pow(alpha, 2.0) - 1.0) + 1.0), 2.0);
    return numerator / denominator;
}

float GeometrySchlickGGX(float Ndot, float roughness)
{
    float k = pow(roughness + 1.0, 2.0) / 8.0;
    float numerator = Ndot;
    float denominator = Ndot * (1.0 - k) + k;
    return numerator / denominator;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float GGX_V = GeometrySchlickGGX(max(dot(N, V), 0.0), roughness);
    float GGX_L = GeometrySchlickGGX(max(dot(N, L), 0.0), roughness);
    return GGX_V * GGX_L;
}

vec3 fresnelSchlick(vec3 F0, vec3 V, vec3 H)
{
    float cos_theta = dot(V, H);
    return F0 + (1.0 - F0) * pow(1.0 - cos_theta, 5.0);
}

float rand_1to1(float x) {
  // -1 - +1
  return fract(sin(x) * 10000.0);
}

float rand_2to1(vec2 uv) {
  // 0 - 1
  const float a = 12.9898, b = 78.233, c = 43758.5453;
  float dt = dot(uv.xy, vec2(a, b)), sn = mod(dt, PI);
  return fract(sin(sn) * c);
}


void sampleVector(const in vec2 uv)
{
    float randNum = rand_2to1(uv);
    float sampleX = rand_1to1(randNum);
    float sampleY = rand_1to1(sampleX);

    float phi = 2.0 * sampleX * PI;
    float cos_theta = 1.0 - 2 * sampleY;
    float sin_theta = sqrt(1.0 - cos_theta * cos_theta);

    for (int i = 0; i < SAMPLE_COUNT; i++) 
    {
        sample_vectors[i] = vec3(cos(phi)*sin_theta, sin(phi) * sin_theta, cos_theta);

        sampleX = rand_1to1(sampleY);
        sampleY = rand_1to1(sampleX);

        phi = 2.0 * sampleX * PI;
        cos_theta = 1.0 - 2 * sampleY;
        sin_theta = sqrt(1.0 - cos_theta * cos_theta);
    }
}

vec4 uniformSampleSphere(vec2 E) {
    float phi = 2.0 *  E.x * PI;
    float cos_theta = 1.0 - 2 * E.y;
    float sin_theta = sqrt(1.0 - cos_theta * cos_theta);
    float PDF = 1.0 / (4 * PI);
    return vec4(cos(phi)*sin_theta, sin(phi) * sin_theta, cos_theta, PDF);
}

float RadicalInverse( uint bits ){
    //reverse bit
    //高低16位换位置
    bits = (bits << 16u) | (bits >> 16u); 
//    uint a = (bits & uint(0xAAAAAAAA));
    //A是5的按位取反
    bits = ((bits & uint(0x55555555)) << 1u) | ((bits & uint(0xAAAAAAAA)) >> 1u);
    //C是3的按位取反
    bits = ((bits & uint(0x33333333)) << 2u) | ((bits & uint(0xCCCCCCCC)) >> 2u);
    bits = ((bits & uint(0x0F0F0F0F)) << 4u) | ((bits & uint(0xF0F0F0F0)) >> 4u);
    bits = ((bits & uint(0x00FF00FF)) << 8u) | ((bits & uint(0xFF00FF00)) >> 8u);
    return  float(bits) * 2.3283064365386963e-10;
}

vec2 Hammersley(uint i,uint N)
{
   return vec2(float(i) / float(N), RadicalInverse(i));
}

vec2 Hammersley_UE(uint i,uint N, uint random1, uint random2)
{
   float E1 = float(i) / N + float(random1 & uint(0xFFFF)) / (1u<<16u);
   float E2 = float(uint(RadicalInverse(i)) ^ random2) * 2.328064365386963e-10;
   return vec2(E1, E2);
}

vec3 hemisphereSample_uniform(float u, float v) {
     float phi = v * 2.0 * PI;
     float cosTheta = 1.0 - u;
     float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
     return vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
}
    
vec3 hemisphereSample_cos(float u, float v) {
     float phi = v * 2.0 * PI;
     float cosTheta = sqrt(1.0 - u);
     float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
     return vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
}


vec3 importantceSampleGGX(vec2 Xi, vec3 N, float roughness) {
    float a = pow(roughness, 2);
    float theta = atan(a * sqrt(Xi.x) / sqrt(1 - Xi.x));
    float phi = 2 * PI * Xi.y;
    vec3 H = vec3(sin(phi) * cos(theta), cos(phi) * sin(theta), cos(theta));
    vec3 up_vector = N.z < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 T = cross(up_vector, N); // tangent x
    vec3 B = cross(N, T);   // bitangent y
    return vec3(T * H.x + B * H.y + N * H.z);
}



void main(void)
{
    vec3 N = normalize(Normal);

    //vec3 up_vector = N.z < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    //vec3 T = cross(up_vector, N); // tangent X/T
    //vec3 B = cross(N, T);   // bitangent Y/B
    //N = texture(tangentMap, TexCoords).rgb * mat3(T, B, N);
    //vec3 N = texture(tangentMap, TexCoords).rgb;
    vec3 V = normalize(camPos - WorldPos);
    vec3 L = normalize(-lightDirection);
    vec3 ior = vec3(1.4);
    vec3 F0 = abs((1.0 - ior) / (1.0 + ior)); 
    F0 = F0 * F0;
    F0 = mix(F0, albedo, metallic);

    // sampleVector(L.xy);

    vec3 Lo = vec3(0.0);
    vec3 Ks = vec3(0.0);
    vec3 radiance = vec3(0.0);
    float weight = 0.0;

    // cosPhi = dot(vTangent, H)
    // A = pow(cosPhi,anisotropy) // F D G A 

    int shader_type = 3;
    vec3 diffuse_color = albedo; //texture(diffuseMap, TexCoords).rgb;
    if(shader_type == 0) {
        vec3 H = normalize(V + L);
        float NdotV = max(dot(N, V), 0.0);
        float NdotL = max(dot(N, L), 0.0); 
        vec3 radiance = vec3(1.0);
        float NDF = DistributionGGX(N, H, roughness);   
        float G   = GeometrySmith(N, V, L, roughness); 
        vec3 F = fresnelSchlick(F0, V, H);

        vec3 numerator    = NDF * G * F; 
        float denominator = max((4.0 * NdotL * NdotV), 0.001);
        vec3 BRDF = numerator / denominator;
        Ks = F;
        vec3 Kd = (1.0 - Ks) * (1.0 - metallic);
        Lo = (Kd * albedo / PI + BRDF) * NdotL;
    }
    else if(shader_type == 1) {
    
        for(uint i = 0u; i < 1024u; i++){
            vec2 E = Hammersley_UE(uint(i), uint(1024u), 
                uint(fract(cos(i) * 10000)), uint(fract(sin(i) * 10000)));
            vec3 L = uniformSampleSphere(E).xyz;
            vec3 H = normalize(V + L);

            float NdotV = max(dot(N, V), 0.0);
            float NdotL = max(dot(N, L), 0.0); 

            float NDF = DistributionGGX(N, H, roughness);   
            float G   = GeometrySmith(N, V, L, roughness); 
            vec3 F = fresnelSchlick(F0, V, H);

            vec3 numerator    = NDF * G * F; 
            float denominator = max((4.0 * NdotL * NdotV), 0.001);
            vec3 BRDF = numerator / denominator;

            Ks = F;
            vec3 Kd = (1.0 - Ks) * (1.0 - metallic);
            if(NdotL > 0.0) {
                weight += NdotL;
                Lo += (Kd * albedo / PI + BRDF) * NdotL;
            }
        }
    }
    else if(shader_type == 2) {
        for(uint i = 0u; i < 1024u; i++) {
            vec2 Xi = Hammersley(i, 1024u);
            vec3 H = importantceSampleGGX(Xi, N, roughness);
          //  vec3 L  = normalize(2.0 * dot(V, H) * H - V);

            float NdotV = max(dot(N, V), 0.0);
            float NdotL = max(dot(N, L), 0.0); 

            float NDF = DistributionGGX(N, H, roughness);   
            float G   = GeometrySmith(N, V, L, roughness); 
            vec3 F = fresnelSchlick(F0, V, H);

            vec3 numerator    = NDF * G * F; 
            float denominator = max((4.0 * NdotL * NdotV), 0.001);
            vec3 BRDF = numerator / denominator;

            Ks = F;
            vec3 Kd = (1.0 - Ks) * (1.0 - metallic);
            if(NdotL > 0.0) {
                weight += NdotL;
                Lo += (Kd * albedo / PI + BRDF) * NdotL;
            }
        }
    }
    else {
        float NdotV = max(dot(N, V), 0.0);
        float NdotL = max(dot(N, L), 0.0); 
        vec3 radiance = vec3(1.0);
        vec3 H = normalize(V + L);
        //float r = texture(perlinNoise, TexCoords).r;
        float r = roughness;
        float D = D_GGXaniso(H, N, r);
        float G = GeometrySmith(N, V, L, r); 
        vec3 F = fresnelSchlick(F0, V, H);

        vec3 numerator    = D * G * F; 
        float denominator = max((4.0 * NdotL * NdotV), 0.001);
        vec3 BRDF = numerator / denominator;
        Ks = F;
        vec3 Kd = (1.0 - Ks) * (1.0 - metallic);
        Lo = (Kd * diffuse_color / PI + BRDF) * NdotL;
    }

    vec3 ambient = vec3(0.05) * diffuse_color * 1.0f;

    vec3 color = Lo;

    if(shader_type == 0)
        color = Lo;
    else if(shader_type < 3)
        color = Lo / weight;
    else
        color = Lo;

    color = color / (color + 1.0);
    color = pow(color, vec3(1.0/2.2));
    FragColor = vec4(color, 1.0);
}