#version 330 core

in vec2 ScreenCoords;

out vec4 FragColor;

#define MAT_LIGHT 0
#define MAT_LAMBERTIAN 1
#define MAT_METALLIC 2
#define MAT_DIELECTRIC 3
#define PI 3.141592653589793238462643

#define EYE_PATH_COUNT 4
#define LIGHT_PATH_COUNT 2
//Ëæ»úÊý
uint m_u = uint(521288629);
uint m_v = uint(362436069);
// Structs
struct Ray {
	vec3 origin;
	vec3 direction;
};

struct Camera {
	vec3 lower_left_corner;
	vec3 horizontal;
	vec3 vertical;
	vec3 origin;
	vec3 u;
	vec3 v;
	vec3 w;
	float lens_radius;
};

struct Sphere {
	vec3 center;
	float radius;
	int material_index;
	int material_type;
};

struct World {
	int object_count;
	Sphere objects[100];
};

struct HitRecord {
	float t;
	vec3 position;
	vec3 normal;
	int material_index;
	int material_type;
};

struct Light{
	vec3 albedo;
};

struct Lambertian{
	vec3 albedo;
};

struct Metallic{
	vec3 albedo;
	float roughness;
};

struct Dielectric{
	vec3 albedo;
	float roughness;
	float ior;
};

struct LightPathNode{
	Ray ray;
	vec3 normal;
	vec3 color;
};

// uniform value
uniform vec2 screen_size;
uniform sampler2D env_map;
uniform vec3 camera_look_from;
uniform float current_frame;

Lambertian m_lambertian[10]; // m_ stand for member
Light m_light[10];
Metallic m_metallic[10];
Dielectric m_dielectric[10];

Camera camera;
// object creation functions
Ray createRay(vec3, vec3);
Sphere createSphere(vec3, float);
World createWorld();
Lambertian createLambertian(vec3);
Metallic createMetallic(vec3, float);
Dielectric createDielectric(vec3, float, float);
Light createLight(vec3);

Camera createCamera(vec3, vec3, vec3, float, float, float, float);

void buildLightPath();
LightPathNode lightPathNode[LIGHT_PATH_COUNT];
World world;
void initScene();

// methods
float hitObject_Sphere(Sphere, Ray);
bool hitWorld(World, Ray, float, float, inout HitRecord);
vec3 getRayLocation(Ray, float);
void LightScatter(in Light, in Ray, in HitRecord, out Ray, out vec3);
void LambertianScatter(in Lambertian, in Ray, in HitRecord, out Ray, out vec3);
void MetallicScatter(in Metallic, in Ray, in HitRecord, out Ray, out vec3);
void DielectricScatter(in Dielectric, in Ray, in HitRecord, out Ray, out vec3);
Ray cameraRay(Camera, vec2);
vec3 rayTrace(Ray);
vec3 getBackground(Ray);
float schlickReflectance(float, float);

// random generator functions
float RadicalInverse(uint);
vec2 Hammersley(uint, uint);
vec3 hemisphereSample_uniform(float, float);
vec3 hemisphereSample_cos(float, float);
float GetUniform();
vec3 sphereSample_uniform(float, float);
vec3 hemisphereSample_cosWeighted(float, float);

void main() {
	initScene();
	vec3 color = vec3(0.0);

	int spp = 100;
	for(int i = 0; i<spp; i++) {
		color += rayTrace(cameraRay(camera, ScreenCoords + vec2(GetUniform(), GetUniform())/ screen_size));
	}
	color /= spp;
	color = pow(color, vec3(1.0 / 2.2));
	FragColor = vec4(color, 1.0);
}

// Functions
Ray createRay(vec3 ori, vec3 dir) { 
	Ray ray;
	ray.origin = ori;
	ray.direction = normalize(dir);
	return ray;
}

Sphere createSphere(vec3 center, float radius, int material_index, int material_type) {
	Sphere sphere;
	sphere.center = center;
	sphere.radius = radius;
	sphere.material_index = material_index;
	sphere.material_type = material_type;
	return sphere;
}
World createWorld() {
	World world;
	world.object_count = 20;
//  Taichi test set
	/*
	world.objects[0] = createSphere(vec3(0.0, 5.4,  -1.0), 3.0, 0, MAT_LIGHT); // ground
//	world.objects[0] = createSphere(vec3(0.0, 1.5,  -1.0), 0.3, 0, MAT_LIGHT);
	world.objects[1] = createSphere(vec3(0.0, -100.5,  -1.0), 100.0, 0, MAT_LAMBERTIAN); // ground
	world.objects[2] = createSphere(vec3(0.0, 102.5,  -1.0), 100.0, 0, MAT_LAMBERTIAN); // ceiling
	world.objects[3] = createSphere(vec3(0.0, 1.0,  101.0), 100.0, 0, MAT_LAMBERTIAN); // back wall
	world.objects[4] = createSphere(vec3(-101.5, 0.0,  -1.0), 100.0, 1, MAT_LAMBERTIAN); // right wall
	world.objects[5] = createSphere(vec3(101.5, 0.0,  -1.0), 100.0, 2, MAT_LAMBERTIAN); // left wall
	world.objects[6] = createSphere(vec3(0.0,  -0.2, -1.5), 0.5, 0, MAT_LAMBERTIAN);
//	world.objects[7] = createSphere(vec3(-0.8, 0.2, -1.0), 0.7, 0, MAT_METALLIC);
//	world.objects[8] = createSphere(vec3(0.7, 0, -0.5), 0.5, 0, MAT_DIELECTRIC);
	*/
//  Custom test set
//	/*
	world.objects[0] = createSphere(vec3(0.0, 15.0, 0.0), 3.0, 0, MAT_LIGHT);
	world.objects[1] = createSphere(vec3(0.0, -1000.0,  -1.0), 1000.0, 4, MAT_LAMBERTIAN); // ground
	world.objects[2] = createSphere(vec3(0.0, 2.0,  0.0), 1.0, 0, MAT_DIELECTRIC); 
	world.objects[3] = createSphere(vec3(-4.0, 2.0,  0.0), 1.0, 1, MAT_DIELECTRIC); 
	world.objects[4] = createSphere(vec3(4.0, 2.0,  0.0), 1.0, 2, MAT_DIELECTRIC);
	world.objects[5] = createSphere(vec3(2.0, 2.0,  -4.0), 1.0, 3, MAT_DIELECTRIC);
	world.objects[6] = createSphere(vec3(2.0, 2.0,  4.0), 1.0, 4, MAT_DIELECTRIC);
//	world.objects[7] = createSphere(vec3(10.0, 15.0, 0.0), 3.0, 2, MAT_LIGHT);
//	world.objects[8] = createSphere(vec3(-10.0, 15.0, 0.0), 3.0, 3, MAT_LIGHT);
//	world.objects[9] = createSphere(vec3(-4.0, 15.0, 4.0), 4, 5, MAT_LIGHT);
//	*/
	return world;
}

void initScene() {
	world = createWorld();
	float aspect = screen_size.x / screen_size.y;
	vec3 look_from = camera_look_from;
	vec3 look_at = vec3(0.0);
	vec3 v_up = vec3(0.0, 1.0, 0.0);
	float aperture = 0.1;
	float focus_dist = 10.0;
	float v_fov = 30;

	camera = createCamera(look_from, look_at, v_up, v_fov, aspect, aperture, focus_dist);

	m_lambertian[0] = createLambertian(vec3(0.8, 0.8, 0.8)); // ground
	m_lambertian[1] = createLambertian(vec3(0.6, 0.0, 0.0)); 
	m_lambertian[2] = createLambertian(vec3(0.0, 0.6, 0.0));
	m_lambertian[3] = createLambertian(vec3(0.22, 0.77, 0.73));
	m_lambertian[4] = createLambertian(vec3(1.0, 1.0, 1.0));
	m_lambertian[5] = createLambertian(vec3(0.3, 0.1, 0.0));
	m_light[0] = createLight(vec3(10.0, 10.0, 10.0)); // silver
	m_light[1] = createLight(vec3(1.0, 1.0, 1.0));	// gold
	m_light[2] = createLight(vec3(0.5, 0.5, 0.5));
	m_light[3] = createLight(vec3(0.22, 0.77, 0.73));
	m_metallic[0] = createMetallic(vec3(1.0, 1.0, 1.0), 0.0);
	m_metallic[1] = createMetallic(vec3(0.22, 0.77, 0.73), 0.5);
	m_metallic[2] = createMetallic(vec3(0.75, 0.75, 0.75), 0.2);
	m_metallic[3] = createMetallic(vec3(0.83, 0.7, 0.0), 0.3);
	m_metallic[4] = createMetallic(vec3(0.5, 0.5, 0.5), 0.5);
	m_dielectric[0] = createDielectric(vec3(1.0, 1.0, 1.0), 0.0, 1.4);
	m_dielectric[1] = createDielectric(vec3(0.22, 0.77, 0.73), 0.0, 1.4);
	m_dielectric[2] = createDielectric(vec3(0.75, 0.75, 0.75), 0.0, 1.4);
	m_dielectric[3] = createDielectric(vec3(0.83, 0.7, 0.0), 0.0, 1.4);
	m_dielectric[4] = createDielectric(vec3(0.8, 0.0, 0.0), 0.0, 1.4);
}


Light createLight(vec3 albedo) {
	Light m_light;
	m_light.albedo = albedo;
	return m_light;
}
Lambertian createLambertian(vec3 albedo) {
	Lambertian m_lambertian;
	m_lambertian.albedo = albedo;
	return m_lambertian;
}
Metallic createMetallic(vec3 albedo, float roughness) {
	Metallic m_metallic;
	m_metallic.albedo = albedo;
	m_metallic.roughness = roughness;
	return m_metallic;
}
Dielectric createDielectric(vec3 albedo, float roughness, float ior) {
	Dielectric m_dielectric;
	m_dielectric.albedo = albedo;
	m_dielectric.roughness = roughness;
	m_dielectric.ior = ior;
	return m_dielectric;
}

Camera createCamera(vec3 look_from, vec3 look_at, vec3 v_up, float v_fov, float aspect, float aperture, float focus_dist) {
	Camera camera;
	camera.origin = look_from;
	float theta = radians(v_fov);
	float half_height = tan(theta / 2.0);
	float half_width = aspect * half_height;
	camera.w = -normalize(look_at - look_from); // facing to lookAt position
	camera.u = normalize(cross(v_up, camera.w));
	camera.v = cross(camera.w, camera.u);

	camera.lower_left_corner = camera.origin - half_width * camera.u - half_height * camera.v - camera.w;
	camera.horizontal = 2 * half_width * camera.u;
	camera.vertical = 2 * half_height * camera.v;
	return camera;
}

void buildLightPath()
{
	Ray ray = createRay(world.objects[0].center, hemisphereSample_cosWeighted(GetUniform(), GetUniform()));

	HitRecord hit_record;
	Ray scattered_ray;
	vec3 color_buffer = vec3(0.0);
	vec3 color = vec3(0.0);
	vec3 brightness = vec3(1.0);
	for(int i =0; i < LIGHT_PATH_COUNT; i++) {
		if(hitWorld(world, ray, 0.001, 100000.0, hit_record))
		{
			if(hit_record.material_type == MAT_LIGHT)
			{	
				ray.origin = hit_record.position;
				i--;
				continue;
			}
			if(hit_record.material_type == MAT_LAMBERTIAN) {
				LambertianScatter(m_lambertian[hit_record.material_index], ray, hit_record, scattered_ray, color);
			}
			else if(hit_record.material_type == MAT_METALLIC) {
				MetallicScatter(m_metallic[hit_record.material_index], ray, hit_record, scattered_ray, color);
			}
			else if(hit_record.material_type == MAT_DIELECTRIC) {
				DielectricScatter(m_dielectric[hit_record.material_index], ray, hit_record, scattered_ray, color);
			}
			ray = scattered_ray;
			brightness *= color;
			lightPathNode[i].ray = scattered_ray;
			lightPathNode[i].normal = hit_record.normal;
			lightPathNode[i].color = brightness;
		}
		else
		{
			lightPathNode[i].color = vec3(0.0);
		}

	}

}

vec3 getRayLocation(Ray ray, float t) {
	return ray.origin + t * ray.direction;
}

bool hitObject_Sphere(Sphere sphere, Ray ray, float t_min, float t_max, inout HitRecord hit_record) {
	float a = dot(ray.direction, ray.direction);
	float b = 2.0 * dot(ray.direction, ray.origin - sphere.center);
	float c = dot(ray.origin - sphere.center, ray.origin - sphere.center) - sphere.radius * sphere.radius;
	float d = b * b - 4.0 * a * c;
	if(d > 0.0)	{
		float temp = (-b - sqrt(d)) / (2.0 * a);
		if(temp < t_max && temp > t_min) {
			hit_record.t = temp;
			hit_record.position = getRayLocation(ray, temp);
			hit_record.normal = (hit_record.position - sphere.center) / sphere.radius;
			hit_record.material_index = sphere.material_index;
			hit_record.material_type = sphere.material_type;
			return true;
		}
		temp = (-b + sqrt(d)) / (2.0 * a);
		if(temp < t_max && temp > t_min) {
			hit_record.t = temp;
			hit_record.position = getRayLocation(ray, temp);
			hit_record.normal = (hit_record.position - sphere.center) / sphere.radius;
			hit_record.material_index = sphere.material_index;
			hit_record.material_type = sphere.material_type;
			return true;
		}
	}
	
	return false;
}

bool hitWorld(World world, Ray ray, float t_min, float t_max, inout HitRecord hit_record) {
	HitRecord temp_record;
	bool hitted = false;
	float cloest_hit_pos = t_max;
	for(int i = 0; i < world.object_count; i++)	{
		if(hitObject_Sphere(world.objects[i], ray, t_min, cloest_hit_pos, temp_record)) {
			hit_record = temp_record;
			hitted = true;
			cloest_hit_pos = hit_record.t;
		}
	}
	return hitted;
}

float getWeightForPath( int s, int l ) {
    return float(s + l + 2);
}

vec3 rayTrace(Ray ray) {
	World world = createWorld();
	HitRecord hit_record;
	vec3 brightness = vec3(1.0);
	vec3 color_buffer = vec3(0.0);
	float russian_roulette = 0.8;
	Ray scattered_ray;
	vec3 color = vec3(0.0);
	
	buildLightPath();
	
	for(int i = 0; i < EYE_PATH_COUNT; i++) {
		if(hitWorld(world, ray, 0.001, 100000.0, hit_record)) {
			if(hit_record.material_type == MAT_LIGHT) {
				LightScatter(m_light[hit_record.material_index], ray, hit_record, scattered_ray, color);
				color_buffer = brightness * color;
				break;
			}
			else {
				if(hit_record.material_type == MAT_LAMBERTIAN) {
					LambertianScatter(m_lambertian[hit_record.material_index], ray, hit_record, scattered_ray, color);
				}
				else if(hit_record.material_type == MAT_METALLIC) {
					MetallicScatter(m_metallic[hit_record.material_index], ray, hit_record, scattered_ray, color);
				}
				else if(hit_record.material_type == MAT_DIELECTRIC) {
					DielectricScatter(m_dielectric[hit_record.material_index], ray, hit_record, scattered_ray, color);
				}
				ray = scattered_ray;
				brightness *= color;
			}
			// light path
//			/*
			for(int j = 0; j < LIGHT_PATH_COUNT - 1; j++)
			{
				vec3 dir = lightPathNode[j].ray.direction;
				float hit_dist = distance(lightPathNode[j].ray.origin, hit_record.position);
				HitRecord light_hit_record;
				if(hitWorld(world, lightPathNode[j].ray, 0.001, 100000.0, light_hit_record))
				{
					float light_travel_dist = distance(lightPathNode[j].ray.origin, light_hit_record.t * lightPathNode[j].ray.direction);
					if(hit_dist <= light_travel_dist + 0.000001)
					{
						color_buffer += lightPathNode[j].color * dot(lightPathNode[j].ray.direction, lightPathNode[j].normal);
					}
				}

			}
//			*/
			
		}
		else {
			color_buffer =  vec3(0.0);//getBackground(ray);// 
			break;
		}
	}

	return brightness * color_buffer;
}

vec3 getBackground(Ray ray) {
	/*
	float phi = acos(ray.direction.y) / PI;
	float theta = (atan(ray.direction.x, ray.direction.z) + PI / 2.0 )/ PI;
	return texture(env_map, vec2(theta, phi)).rgb;
	*/
	float t = (ray.direction.y + 1.0) * 0.5;
	return (1.0 - t) * vec3(1.0) + t * vec3(0.5, 0.7, 1.0);
}


Ray cameraRay(Camera camera, vec2 uv) {
	Ray ray = createRay(camera.origin, 
			camera.lower_left_corner 
			+ uv.x * camera.horizontal 
			+ uv.y * camera.vertical - camera.origin);
	return ray;
}

void LightScatter(in Light light, in Ray incident, in HitRecord hit_record, out Ray scattered, out vec3 color) {
	color = light.albedo;
	scattered.origin = hit_record.position;
	scattered.direction = hit_record.normal + sphereSample_uniform(GetUniform(), GetUniform());
}

void LambertianScatter(in Lambertian lambertain, in Ray incident, in HitRecord hit_record, out Ray scattered, out vec3 color) {
	color = lambertain.albedo;
	scattered.origin = hit_record.position;
	scattered.direction = hit_record.normal + sphereSample_uniform(GetUniform(), GetUniform());
}

void MetallicScatter(in Metallic metallic, in Ray incident, in HitRecord hit_record, out Ray scattered, out vec3 color) {
	color = metallic.albedo;
	scattered.origin = hit_record.position;
	scattered.direction = reflect(incident.direction, hit_record.normal) +  metallic.roughness * sphereSample_uniform(GetUniform(), GetUniform());
}

void DielectricScatter(in Dielectric dielectric, in Ray incident, in HitRecord hit_record, out Ray scattered, out vec3 color) {
	color = dielectric.albedo;
	scattered.origin = hit_record.position;
	vec3 normal = hit_record.normal;
	float ior = dielectric.ior;
	if(dot(incident.direction, hit_record.normal) < 0.0) {
		ior = 1.0 / ior;
	}
	else
		normal = -normal;

	float cos_theta = min(-dot(incident.direction, normal), 1.0);
	float sin_theta = sqrt(1.0 - cos_theta * cos_theta);
	
	if(ior * sin_theta > 1.0 || schlickReflectance(cos_theta, ior) > GetUniform()) {
		scattered.direction = reflect(incident.direction, normal) + dielectric.roughness * sphereSample_uniform(GetUniform(), GetUniform());
	}
	else {
		scattered.direction = refract(incident.direction, normal, ior) + dielectric.roughness * sphereSample_uniform(GetUniform(), GetUniform());
	}
}

float schlickReflectance(float cos_theta, float ref_index) {
	float r0 = (1.0 - ref_index) / (1.0 + ref_index);
	r0 = r0 * r0;
	return r0 + (1 - r0) * pow(1.0 - cos_theta, 5.0);
}
// random generator
float RadicalInverse( uint bits ){
    bits = (bits << 16u) | (bits >> 16u); 
    bits = ((bits & uint(0x55555555)) << 1u) | ((bits & uint(0xAAAAAAAA)) >> 1u);
    bits = ((bits & uint(0x33333333)) << 2u) | ((bits & uint(0xCCCCCCCC)) >> 2u);
    bits = ((bits & uint(0x0F0F0F0F)) << 4u) | ((bits & uint(0xF0F0F0F0)) >> 4u);
    bits = ((bits & uint(0x00FF00FF)) << 8u) | ((bits & uint(0xFF00FF00)) >> 8u);
    return  float(bits) * 2.3283064365386963e-10;
}

vec2 Hammersley(uint i,uint N) {
   return vec2(float(i) / float(N), RadicalInverse(i));
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

vec3 sphereSample_uniform(float u, float v) {
	float phi = u * 2.0 * PI;
    float theta = v * PI;
    return vec3(cos(phi)*sin(theta), sin(phi)*sin(theta), cos(theta));
}

vec3 hemisphereSample_cosWeighted(float u, float v) {
    float phi = u * 2.0 * PI;
    float cosTheta = sqrt(v * PI);
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
    return vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
}


uint GetUintCore(inout uint u, inout uint v){
	v = uint(36969) * (v & uint(65535)) + (v >> 16);
	u = uint(18000) * (u & uint(65535)) + (u >> 16);
	return (v << 16) + u;
}

float GetUniformCore(inout uint u, inout uint v){
	uint z = GetUintCore(u, v);
	return float(z) / uint(4294967295);
}

float GetUniform(){
	return GetUniformCore(m_u, m_v);
}

vec2 get_random_numbers(inout uvec2 seed) {
    seed = 1664525u * seed + 1013904223u;
    seed.x += 1664525u * seed.y;
    seed.y += 1664525u * seed.x;
    seed ^= (seed >> 16u);
    seed.x += 1664525u * seed.y;
    seed.y += 1664525u * seed.x;
    seed ^= (seed >> 16u);
    return vec2(seed) * 2.32830643654e-10;
}



void mainImage(out vec4 O, vec2 F) {
    vec2   g  = screen_size,
           o  = (F+F-g)/g.y/.7; 
    float  l  = 0., 
           f  = current_frame*.4-2.;
    
    for (O *= l; l++ < 55.;
	O += 0.005/abs(length(o + vec2(cos(l*(cos(f*.5)*.5+.6)+f), sin(l+f)))-
        (sin(l+f*4.)*.04+.02))*(cos(l+length(o)*4.+vec4(0,1,2,0))+1.));
}