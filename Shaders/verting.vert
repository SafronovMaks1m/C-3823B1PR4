#version 430 core

in vec3 vPosition;
out vec3 glPosition;

void main() {
    glPosition = vPosition;
    gl_Position = vec4(vPosition, 1.0);
}