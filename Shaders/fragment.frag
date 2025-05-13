#version 430 core

#define EPSILON 0.001
#define BIG 1000000.0
#define MAX_TRIANGLES 16
#define MAX_SPHERES 1
#define MAX_MATERIALS 5
#define DIFFUSE 1
#define MIRROR_REFLECTION 2
#define REFRACTION 3
#define MAX_RAY_DEPTH 2
#define RAY_STACK_SIZE (MAX_RAY_DEPTH + 2)

in vec3 glPosition;
out vec4 FragColor;

struct SCamera { vec3 Position, View, Up, Side; vec2 Scale; };
struct SRay { vec3 Origin, Direction; };
struct SSphere { vec3 Center; float Radius; int MaterialIdx; };
struct STriangle { vec3 v1, v2, v3; int MaterialIdx; };
struct SMaterial { vec3 Color; vec4 LightCoeffs; float ReflectionCoef, RefractionCoef; int MaterialType; };
struct SLight { vec3 Position, Color; };
struct SIntersection { float Time; vec3 Point, Normal, Color; vec4 LightCoeffs; float ReflectionCoef, RefractionCoef; int MaterialType; };
struct STracingRay { SRay ray; float contribution; int depth; };

uniform SCamera uCamera;
uniform float uTime;

SSphere spheres[MAX_SPHERES];
STriangle triangles[MAX_TRIANGLES];
SMaterial materials[MAX_MATERIALS];
SLight light;
STracingRay rayStack[RAY_STACK_SIZE];
int stackTop;

void pushRay(STracingRay trRay) {
    if (stackTop < RAY_STACK_SIZE -1 && trRay.depth <= MAX_RAY_DEPTH && trRay.contribution > 0.01) {
        stackTop++;
        rayStack[stackTop] = trRay;
    }
}

STracingRay popRay() {
    STracingRay trRay = rayStack[stackTop];
    stackTop--;
    return trRay;
}

bool isEmpty() { return stackTop < 0; }

bool IntersectSphere(SSphere sphere, SRay ray, float start, float end, out float time) {
    vec3 oc = ray.Origin - sphere.Center;
    float a = dot(ray.Direction, ray.Direction);
    float b = 2.0 * dot(oc, ray.Direction);
    float c = dot(oc, oc) - sphere.Radius*sphere.Radius;
    float d = b*b - 4.0*a*c;
    
    time = BIG;
    if (d < 0.0) return false;
    
    float t1 = (-b - sqrt(d))/(2.0*a);
    float t2 = (-b + sqrt(d))/(2.0*a);
    bool hit = false;
    
    if(t1 > start && t1 < end) { time = t1; hit = true; }
    if(t2 > start && t2 < time) { time = t2; hit = true; }
    return hit;
}

bool IntersectTriangle(SRay ray, vec3 v0, vec3 v1, vec3 v2, out float t) {
    t = -1.0;
    vec3 edge1 = v1 - v0;
    vec3 edge2 = v2 - v0;
    vec3 h = cross(ray.Direction, edge2);
    float a = dot(edge1, h);
    
    if(a > -EPSILON && a < EPSILON) return false;
    float f = 1.0/a;
    vec3 s = ray.Origin - v0;
    float u = f * dot(s, h);
    
    if(u < 0.0 || u > 1.0) return false;
    vec3 q = cross(s, edge1);
    float v = f * dot(ray.Direction, q);
    
    if(v < 0.0 || u + v > 1.0) return false;
    t = f * dot(edge2, q);
    return t > EPSILON;
}

SRay GenerateRay(SCamera cam) {
    vec2 coords = glPosition.xy * 0.5 + 0.5;
    coords = coords * 2.0 - 1.0;
    coords *= cam.Scale;
    vec3 dir = cam.View + cam.Side * coords.x + cam.Up * coords.y;
    return SRay(cam.Position, normalize(dir));
}

void initializeDefaultScene() {
    triangles[0] = STriangle(vec3(-5,-5,5), vec3(-5,5,5), vec3(-5,5,-5), 0);
    triangles[1] = STriangle(vec3(-5,-5,5), vec3(-5,5,-5), vec3(-5,-5,-5), 0);
    triangles[2] = STriangle(vec3(-5,-5,5), vec3(5,-5,5), vec3(5,5,5), 0);
    triangles[3] = STriangle(vec3(-5,-5,5), vec3(5,5,5), vec3(-5,5,5), 0);
    triangles[4] = STriangle(vec3(5,-5,-5), vec3(5,5,5), vec3(5,5,-5), 0);
    triangles[5] = STriangle(vec3(5,-5,-5), vec3(5,-5,5), vec3(5,5,5), 0);
    triangles[6] = STriangle(vec3(-5,-5,-5), vec3(5,-5,-5), vec3(5,5,-5), 0);
    triangles[7] = STriangle(vec3(-5,-5,-5), vec3(5,5,-5), vec3(-5,5,-5), 0);
    triangles[8] = STriangle(vec3(-5,5,-5), vec3(5,5,5), vec3(5,5,-5), 1);
    triangles[9] = STriangle(vec3(-5,5,-5), vec3(-5,5,5), vec3(5,5,5), 1);
    triangles[10] = STriangle(vec3(-5,-5,5), vec3(5,-5,5), vec3(5,-5,-5), 0);
    triangles[11] = STriangle(vec3(-5,-5,5), vec3(5,-5,-5), vec3(-5,-5,-5), 0);
    vec3 center = vec3(-1.5, -3, 1);
    float size = 1.5;
    vec3 v0 = center + vec3(0, 0, size);
    vec3 v1 = center + vec3(size, 0, -size);
    vec3 v2 = center + vec3(-size, 0, -size);
    vec3 v3 = center + vec3(0, size, 0);
    triangles[12] = STriangle(v0, v1, v2, 2);
    triangles[13] = STriangle(v0, v1, v3, 2);
    triangles[14] = STriangle(v1, v2, v3, 2);
    triangles[15] = STriangle(v2, v0, v3, 2);
    spheres[0] = SSphere(vec3(2, -2.5, 3), 1.5, 3);
}

void initializeDefaultLightMaterials() {
    light = SLight(vec3(0, 4.5, -4), vec3(1, 1, 0.9));
    materials[0] = SMaterial(vec3(0.7, 0.7, 0.7), vec4(0.3, 0.6, 0.5, 64), 0.5, 0.0, MIRROR_REFLECTION);
    materials[1] = SMaterial(vec3(0.8, 0.8, 0.8), vec4(0.2, 0.8, 0.0, 10), 0.0, 0.0, DIFFUSE);
    materials[2] = SMaterial(vec3(0.7, 0.7, 0.8), vec4(0.2, 0.5, 0.8, 100), 0.0, 0.0, DIFFUSE);
    materials[3] = SMaterial(vec3(0.8, 0.8, 0.9), vec4(0.1, 0.9, 0.9, 200), 0.9, 0.0, MIRROR_REFLECTION);
}

bool Raytrace(SRay ray, float start, float end, inout SIntersection intersect) {
    bool hit = false;
    intersect.Time = end;
    
    for(int i=0; i<MAX_SPHERES; i++) {
        float t;
        if(IntersectSphere(spheres[i], ray, start, intersect.Time, t)) {
            intersect.Time = t;
            intersect.Point = ray.Origin + ray.Direction*t;
            intersect.Normal = normalize(intersect.Point - spheres[i].Center);
            SMaterial mat = materials[spheres[i].MaterialIdx];
            intersect.Color = mat.Color;
            intersect.LightCoeffs = mat.LightCoeffs;
            intersect.ReflectionCoef = mat.ReflectionCoef;
            intersect.RefractionCoef = mat.RefractionCoef;
            intersect.MaterialType = mat.MaterialType;
            hit = true;
        }
    }
    
    for(int i=0; i<MAX_TRIANGLES; i++) {
        float t;
        if(IntersectTriangle(ray, triangles[i].v1, triangles[i].v2, triangles[i].v3, t)) {
            if(t < intersect.Time) {
                intersect.Time = t;
                intersect.Point = ray.Origin + ray.Direction*t;
                vec3 edgeA = triangles[i].v2 - triangles[i].v1;
                vec3 edgeB = triangles[i].v3 - triangles[i].v1;
                intersect.Normal = normalize(cross(edgeA, edgeB));
                if(dot(intersect.Normal, ray.Direction) > 0.0) intersect.Normal *= -1.0;
                SMaterial mat = materials[triangles[i].MaterialIdx];
                intersect.Color = mat.Color;
                intersect.LightCoeffs = mat.LightCoeffs;
                intersect.ReflectionCoef = mat.ReflectionCoef;
                intersect.RefractionCoef = mat.RefractionCoef;
                intersect.MaterialType = mat.MaterialType;
                hit = true;
            }
        }
    }
    return hit;
}

float Shadow(SLight light, SIntersection intersect) {
    vec3 L = normalize(light.Position - intersect.Point);
    float dist = distance(light.Position, intersect.Point);
    SRay ray = SRay(intersect.Point + intersect.Normal*EPSILON, L);
    
    SIntersection shadowIntersect;
    return Raytrace(ray, EPSILON, dist, shadowIntersect) ? 0.0 : 1.0;
}

vec3 Phong(SIntersection intersect, SLight light, float shadow) {
    vec3 ambient = intersect.LightCoeffs.x * intersect.Color;
    vec3 N = intersect.Normal;
    vec3 L = normalize(light.Position - intersect.Point);
    float NdotL = max(dot(N, L), 0.0);
    vec3 diffuse = intersect.LightCoeffs.y * NdotL * intersect.Color * light.Color;
    vec3 V = normalize(uCamera.Position - intersect.Point);
    vec3 R = reflect(-L, N);
    float RdotV = max(dot(R, V), 0.0);
    vec3 specular = intersect.LightCoeffs.z * pow(RdotV, intersect.LightCoeffs.w) * light.Color;
    return ambient + (diffuse + specular) * shadow;
}

void main() {
    stackTop = -1;
    initializeDefaultScene();
    initializeDefaultLightMaterials();
    
    SRay ray = GenerateRay(uCamera);
    vec3 color = vec3(0.0);
    pushRay(STracingRay(ray, 1.0, 0));
    
    while(!isEmpty()) {
        STracingRay trRay = popRay();
        SRay currentRay = trRay.ray;
        float contrib = trRay.contribution;
        int depth = trRay.depth;
        
        SIntersection intersect;
        if(Raytrace(currentRay, EPSILON, BIG, intersect)) {
            float shadow = Shadow(light, intersect);
            vec3 surfaceColor = vec3(0.0);
            
            if(intersect.MaterialType == DIFFUSE) {
                surfaceColor = Phong(intersect, light, shadow);
                color += contrib * surfaceColor * (1.0 - intersect.ReflectionCoef);
                
                if(intersect.ReflectionCoef > EPSILON && depth < MAX_RAY_DEPTH) {
                    vec3 R = reflect(currentRay.Direction, intersect.Normal);
                    pushRay(STracingRay(SRay(intersect.Point + intersect.Normal*EPSILON, R), 
                           contrib * intersect.ReflectionCoef, depth+1));
                }
            }
            else if(intersect.MaterialType == MIRROR_REFLECTION) {
                if(intersect.ReflectionCoef < 1.0-EPSILON) 
                    color += contrib * (1.0 - intersect.ReflectionCoef) * Phong(intersect, light, shadow);
                
                if(depth < MAX_RAY_DEPTH && intersect.ReflectionCoef > EPSILON) {
                    vec3 R = reflect(currentRay.Direction, intersect.Normal);
                    pushRay(STracingRay(SRay(intersect.Point + intersect.Normal*EPSILON, R), 
                           contrib * intersect.ReflectionCoef, depth+1));
                }
            }
            else if(intersect.MaterialType == REFRACTION && depth < MAX_RAY_DEPTH) {
                vec3 N = intersect.Normal;
                vec3 I = currentRay.Direction;
                float n1 = 1.0, n2 = intersect.RefractionCoef;
                
                if(dot(I, N) > 0.0) { N = -N; float temp = n1; n1 = n2; n2 = temp; }
                float eta = n1/n2;
                vec3 T = refract(I, N, eta);
                
                if(dot(T, T) > 0.0) {
                    pushRay(STracingRay(SRay(intersect.Point - N*EPSILON, T), contrib, depth+1));
                } else {
                    vec3 R = reflect(I, N);
                    pushRay(STracingRay(SRay(intersect.Point + N*EPSILON, R), contrib, depth+1));
                }
            }
        } else {
	    vec3 bg = mix(vec3(0.5,0.7,1.0), vec3(0.1), clamp(currentRay.Direction.y,0.0,1.0));
            color += contrib * bg;
        }
    }
    
    FragColor = vec4(clamp(color, 0.0, 1.0), 1.0);
}