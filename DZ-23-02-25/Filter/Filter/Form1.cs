using System.Runtime.InteropServices.JavaScript;

namespace Filter
{
    public partial class Form1 : Form
    {
        Bitmap image;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Image files|*.png;*.jpg;*.bmp|All files(*.*)|*.*)";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                image = new Bitmap(dialog.FileName);
                pictureBox1.Image = image;
                pictureBox1.Refresh();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (image != null)
            {
                for (int i = 0; i < image.Height; i++)
                {
                    for (int j = 0; j < image.Width; j++)
                    {
                        Color color = image.GetPixel(j, i);
                        int shade_gray = (color.R + color.G + color.B) / 3;
                        Color gray = Color.FromArgb(shade_gray, shade_gray, shade_gray);
                        image.SetPixel(j, i, gray);
                    }
                }
                pictureBox1.Image = image;
                pictureBox1.Refresh();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            pictureBox1.Image = null;
            pictureBox1.Refresh();
            this.Close();
        }
    }
}
