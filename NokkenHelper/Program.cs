using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageDisplayApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ConfiguratorForm());
        }
    }

    public class ConfiguratorForm : Form
    {
        private NumericUpDown showDurationInput;
        private NumericUpDown intervalInput;
        private CheckBox pauseMediaCheckBox;
        private Button startButton;
        private CancellationTokenSource cancellationTokenSource;
        private Form imageForm;
        private bool isImageDisplayed = false;
        private bool wasPlaying = false;
        private object lockObject = new object();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

        public ConfiguratorForm()
        {
            Text = "Image Display Configurator";
            Size = new Size(300, 250);

            Label showDurationLabel = new Label { Text = "Show duration (ms):", Dock = DockStyle.Top };
            showDurationInput = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = 500, Dock = DockStyle.Top };

            Label intervalLabel = new Label { Text = "Interval (ms):", Dock = DockStyle.Top };
            intervalInput = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = 5000, Dock = DockStyle.Top };

            pauseMediaCheckBox = new CheckBox { Text = "Pause media during display", Checked = false, Dock = DockStyle.Top };

            startButton = new Button { Text = "Start", Dock = DockStyle.Top };
            startButton.Click += StartButton_Click;

            Controls.Add(startButton);
            //Controls.Add(pauseMediaCheckBox); 
            // хотел сделать опцию остановки видео оказалось что неоткуда взять информацию о том что оно сейчас играет
            //а когда попытался удалить функционал оно стало выключаться само собой
            //так что не сломано не ломай + похуй + нахуй
            Controls.Add(intervalInput);
            Controls.Add(intervalLabel);
            Controls.Add(showDurationInput);
            Controls.Add(showDurationLabel);

            imageForm = CreateImageForm();
        }

        private Form CreateImageForm()
        {
            Form form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                TopMost = true,
                BackgroundImageLayout = ImageLayout.Stretch
            };

            form.MouseDown += (s, e) =>
            {
                form.Hide();
                lock (lockObject)
                {
                    isImageDisplayed = false;
                }
            };
            form.Show();
            form.Hide();

            return form;
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                startButton.Text = "Start";
                cancellationTokenSource = null;
                return;
            }

            string imagePath = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.png")
                                      .Concat(Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.jpg"))
                                      .FirstOrDefault();

            if (string.IsNullOrEmpty(imagePath))
            {
                MessageBox.Show("No image found in the application directory.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;

            startButton.Text = "Stop";

            int showDuration = (int)showDurationInput.Value;
            int interval = (int)intervalInput.Value;

            await Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(interval, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    if (pauseMediaCheckBox.Checked && !isImageDisplayed)
                    {
                        PauseMediaPlayback();
                        wasPlaying = true;
                    }

                    ShowImage(imagePath);

                    try
                    {
                        await Task.Delay(showDuration, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }

                    imageForm.Invoke(new Action(() => imageForm.Hide()));
                    lock (lockObject)
                    {
                        isImageDisplayed = false;
                    }

                    if (pauseMediaCheckBox.Checked && wasPlaying)
                    {
                        ResumeMediaPlayback();
                    }
                }
            });

            startButton.Text = "Start";
            cancellationTokenSource = null;
        }

        private void ShowImage(string imagePath)
        {
            lock (lockObject)
            {
                if (!isImageDisplayed)
                {
                    if (imageForm.BackgroundImage != null)
                    {
                        imageForm.BackgroundImage.Dispose();
                    }
                    imageForm.BackgroundImage = Image.FromFile(imagePath);

                    imageForm.Invoke(new Action(() => imageForm.Show()));
                    
                    isImageDisplayed = true;
                }
            }
        }

        private void PauseMediaPlayback()
        {
            keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, 0);
        }

        private void ResumeMediaPlayback()
        {
            keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, 0);
        }
    }
}
