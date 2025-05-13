using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OpenTK.Mathematics;
using System;

namespace Shaders
{
    public partial class Form1 : Form
    {
        private View view;
        private System.Windows.Forms.Timer renderTimer;
        private bool[] keysPressed = new bool[256];
        private Point lastMousePos;
        private bool firstMove = true;
        private float cameraPitch = 0.0f;
        private float cameraYaw = 0.0f;
        private Vector3 targetCameraPosition;
        private Vector3 targetCameraView;
        private const float RotationSmoothness = 0.2f;
        private const float MovementSmoothness = 0.1f;

        public Form1()
        {
            InitializeComponent();
            this.KeyPreview = true;
            glControl1.MouseWheel += glControl1_MouseWheel;
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            view = new View();
            view.SetupViewport(glControl1.Width, glControl1.Height);

            view.InitShaders();
            view.InitBuffers();

            targetCameraPosition = new Vector3(0.0f, 0.0f, -5);
            targetCameraView = new Vector3(0.0f, 0.0f, 1.0f);

            view.SetCameraProperties(
                targetCameraPosition,
                targetCameraView,
                new Vector3(0.0f, 1.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                glControl1.Width / (float)glControl1.Height
            );

            renderTimer = new System.Windows.Forms.Timer();
            renderTimer.Interval = 16;
            renderTimer.Tick += (s, args) =>
            {
                UpdateCameraPosition();
                UpdateCamera();
                glControl1.Invalidate();
            };
            renderTimer.Start();
        }

        private void UpdateCamera()
        {
            view.CameraPosition = Vector3.Lerp(view.CameraPosition, targetCameraPosition, MovementSmoothness);
            view.CameraView = Vector3.Lerp(view.CameraView, targetCameraView.Normalized(), RotationSmoothness);

            view.CameraSide = Vector3.Cross(view.CameraView, Vector3.UnitY).Normalized();
            view.CameraUp = Vector3.Cross(view.CameraSide, view.CameraView).Normalized();
        }

        private void UpdateCameraPosition()
        {
            float moveSpeed = 0.1f;

            if (keysPressed[(int)Keys.W])
                targetCameraPosition += moveSpeed * targetCameraView;
            if (keysPressed[(int)Keys.S])
                targetCameraPosition -= moveSpeed * targetCameraView;
            if (keysPressed[(int)Keys.A])
                targetCameraPosition -= moveSpeed * Vector3.Cross(targetCameraView, Vector3.UnitY).Normalized();
            if (keysPressed[(int)Keys.D])
                targetCameraPosition += moveSpeed * Vector3.Cross(targetCameraView, Vector3.UnitY).Normalized();
            if (keysPressed[(int)Keys.Q])
                targetCameraPosition -= moveSpeed * Vector3.UnitY;
            if (keysPressed[(int)Keys.E])
                targetCameraPosition += moveSpeed * Vector3.UnitY;
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (view == null) return;
            view.Render();
            glControl1.SwapBuffers();
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (view == null) return;

            view.SetupViewport(glControl1.Width, glControl1.Height);
            view.UpdateCameraAspectRatio(glControl1.Width / (float)glControl1.Height);
            glControl1.Invalidate();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            renderTimer?.Stop();
            renderTimer?.Dispose();

            if (view != null)
            {
                try
                {
                    glControl1.MakeCurrent();
                    view.Cleanup();
                }
                catch { }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((int)e.KeyCode >= 0 && (int)e.KeyCode < keysPressed.Length)
            {
                keysPressed[(int)e.KeyCode] = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if ((int)e.KeyCode >= 0 && (int)e.KeyCode < keysPressed.Length)
            {
                keysPressed[(int)e.KeyCode] = false;
            }
        }

        private void glControl1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (firstMove)
                {
                    lastMousePos = new Point(e.X, e.Y);
                    firstMove = false;
                }
                else
                {
                    float deltaX = e.X - lastMousePos.X;
                    float deltaY = e.Y - lastMousePos.Y;
                    lastMousePos = new Point(e.X, e.Y);

                    float sensitivity = 0.002f;
                    cameraYaw -= deltaX * sensitivity;
                    cameraPitch = MathHelper.Clamp(cameraPitch - deltaY * sensitivity, -MathHelper.PiOver2 + 0.1f, MathHelper.PiOver2 - 0.1f);

                    targetCameraView = new Vector3(
                        (float)(Math.Cos(cameraPitch) * Math.Sin(cameraYaw)),
                        (float)Math.Sin(cameraPitch),
                        (float)(Math.Cos(cameraPitch) * Math.Cos(cameraYaw))
                        )
                        .Normalized();
                }
            }
            else
            {
                firstMove = true;
            }
        }

        private void glControl1_MouseWheel(object sender, MouseEventArgs e)
        {
            float zoomSpeed = 0.1f;
            targetCameraPosition += (e.Delta > 0 ? 1 : -1) * zoomSpeed * targetCameraView;
        }
    }

    public class View
{
    private int basicProgramID;
    private int basicVertexShader;
    private int basicFragmentShader;
    private int vbo_position;
    private int vao;
    private Vector2 uniform_camera_Scale;
    private float time = 0.0f;
    private int loc_camera_Position;
    private int loc_camera_View;
    private int loc_camera_Up;
    private int loc_camera_Side;
    private int loc_camera_Scale;
    private int loc_uTime;
    private int loc_vPosition;

    public Vector3 CameraPosition { get; set; }
    public Vector3 CameraView { get; set; }
    public Vector3 CameraUp { get; set; }
    public Vector3 CameraSide { get; set; }

    public void SetupViewport(int width, int height)
    {
        GL.Viewport(0, 0, width, height);
    }

    public void SetCameraProperties(Vector3 pos, Vector3 viewDir, Vector3 up, Vector3 side, float aspectRatio)
    {
        CameraPosition = pos;
        CameraView = viewDir.Normalized();
        CameraUp = up.Normalized();
        CameraSide = side.Normalized();
        uniform_camera_Scale = new Vector2(aspectRatio, 1.0f);
    }

    public void UpdateCameraAspectRatio(float aspectRatio)
    {
        uniform_camera_Scale.X = aspectRatio;
    }

    private void LoadShaderFromFile(string filename, ShaderType type, int program, out int address)
    {
        address = GL.CreateShader(type);
        GL.ShaderSource(address, File.ReadAllText(filename));
        GL.CompileShader(address);
        GL.AttachShader(program, address);

        string infoLog = GL.GetShaderInfoLog(address);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            Console.WriteLine($"Shader {type} ({Path.GetFileName(filename)}) compile log: {infoLog}");
        }
    }

    public void InitShaders()
    {
        basicProgramID = GL.CreateProgram();
        string fullPathToShaders = @"C:\lessons\комп графика\Shaders";
        string vertShaderPath = Path.Combine(fullPathToShaders, "verting.vert");
        string fragShaderPath = Path.Combine(fullPathToShaders, "fragment.frag");

        LoadShaderFromFile(vertShaderPath, ShaderType.VertexShader, basicProgramID, out basicVertexShader);
        LoadShaderFromFile(fragShaderPath, ShaderType.FragmentShader, basicProgramID, out basicFragmentShader);

        GL.LinkProgram(basicProgramID);
        string programInfoLog = GL.GetProgramInfoLog(basicProgramID);
        if (!string.IsNullOrWhiteSpace(programInfoLog))
        {
            Console.WriteLine($"Program link log: {programInfoLog}");
        }

        loc_vPosition = GL.GetAttribLocation(basicProgramID, "vPosition");
        loc_camera_Position = GL.GetUniformLocation(basicProgramID, "uCamera.Position");
        loc_camera_View = GL.GetUniformLocation(basicProgramID, "uCamera.View");
        loc_camera_Up = GL.GetUniformLocation(basicProgramID, "uCamera.Up");
        loc_camera_Side = GL.GetUniformLocation(basicProgramID, "uCamera.Side");
        loc_camera_Scale = GL.GetUniformLocation(basicProgramID, "uCamera.Scale");
        loc_uTime = GL.GetUniformLocation(basicProgramID, "uTime");
    }

    public void InitBuffers()
    {
        Vector3[] vertdata = new Vector3[] {
            new Vector3(-1f, -1f, 0f),
            new Vector3( 1f, -1f, 0f),
            new Vector3(-1f,  1f, 0f),
            new Vector3( 1f,  1f, 0f)
        };

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        vbo_position = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_position);
        GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertdata.Length * Vector3.SizeInBytes),
                        vertdata, BufferUsageHint.StaticDraw);

        if (loc_vPosition != -1)
        {
            GL.EnableVertexAttribArray(loc_vPosition);
            GL.VertexAttribPointer(loc_vPosition, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);
        }

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    public void Render()
    {
        if (basicProgramID == 0 || vao == 0)
        {
            GL.ClearColor(1.0f, 0.0f, 1.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            return;
        }

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.UseProgram(basicProgramID);

        if (loc_camera_Position != -1) GL.Uniform3(loc_camera_Position, CameraPosition);
        if (loc_camera_View != -1) GL.Uniform3(loc_camera_View, CameraView);
        if (loc_camera_Up != -1) GL.Uniform3(loc_camera_Up, CameraUp);
        if (loc_camera_Side != -1) GL.Uniform3(loc_camera_Side, CameraSide);
        if (loc_camera_Scale != -1) GL.Uniform2(loc_camera_Scale, uniform_camera_Scale);
        if (loc_uTime != -1) GL.Uniform1(loc_uTime, time);

        time += 0.016f;

        GL.BindVertexArray(vao);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        GL.BindVertexArray(0);

        GL.UseProgram(0);
    }

    public void Cleanup()
    {
        if (basicProgramID != 0)
        {
            if (basicVertexShader != 0)
            {
                GL.DetachShader(basicProgramID, basicVertexShader);
                GL.DeleteShader(basicVertexShader);
            }
            if (basicFragmentShader != 0)
            {
                GL.DetachShader(basicProgramID, basicFragmentShader);
                GL.DeleteShader(basicFragmentShader);
            }
            GL.DeleteProgram(basicProgramID);
        }

        if (vbo_position != 0) GL.DeleteBuffer(vbo_position);
        if (vao != 0) GL.DeleteVertexArray(vao);
    }
}
}