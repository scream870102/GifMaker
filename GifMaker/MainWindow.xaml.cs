using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Shell;

namespace GifMaker
{
    public partial class MainWindow : Window
    {
        MediaInfo mediaInfo = null;
        const double sliderTimerTick = 0.3;
        StringBuilder log = new StringBuilder();
        DispatcherTimer timer = null;
        Point rectStart = default(Point);
        bool isMouseDown = false;
        double fpsSec = 0.0;
        bool isPlay = false;
        bool isEditingName = false;

        public MainWindow()
        {
            InitializeComponent();

            //UI Button Event Bind
            OpenButton.Click += OpenButton_Click;
            ClearButton.Click += ClearButton_Click;
            ExportButton.Click += ExportButton_Click;
            ResetButton.Click += ResetButton_Click;
            ExportTextBox.GotKeyboardFocus += ExportTextBox_GotKeyboardFocus;
            ExportTextBox.LostKeyboardFocus += ExportTextBox_LostKeyboardFocus;

            //Slider Event Bind
            Slider.MouseLeftButtonUp += Slider_MouseLeftButtonUp;

            //Media Control Button Bind
            PlayButton.Click += PlayButton_Click;
            KeyDown += MainWindow_KeyDown;
            FromButton.Click += FromButton_Click;
            ToButton.Click += ToButton_Click;

            //Rect Event Bind
            Media.MediaOpened += Media_MediaOpened;
            Media.MouseMove += Media_MouseMove;
            Media.MouseLeftButtonDown += Media_MouseLeftButtonDown;
            RangeRect.MouseRightButtonDown += RangeRect_MouseRightButtonDown;


            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(sliderTimerTick);
            timer.Tick += Timer_Tick;
            timer.Start();

            UpdateLog("", true);

        }



        #region UI_BUTTON_CALLBACK
        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                UpdateLog($"Opening {openFileDialog.FileName}\n\n");
                Media.Source = new Uri(openFileDialog.FileName);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            WidthTextBox.Text = "0";
            HeightTextBox.Text = "0";
            FromTextBox.Text = "0.0";
            ToTextBox.Text = mediaInfo?.Duration.ToString() ?? "0.0";
            XTextBox.Text = "";
            YTextBox.Text = "";
            FpsTextBox.Text = mediaInfo?.FrameRate.ToString() ?? "0";
            RangeRect.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
            RangeRect.Width = 0;
            RangeRect.Height = 0;
            isMouseDown = false;
            UpdateLog("Clear Setting\n");
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var path = new StringBuilder(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            path.Append($"\\{ExportTextBox.Text}.gif");
            var command = new StringBuilder();
            var dur = Convert.ToDouble(ToTextBox.Text) - Convert.ToDouble(FromTextBox.Text);
            if (WidthTextBox.Text == "0" || HeightTextBox.Text == "0")
                command.Append($"-ss {FromTextBox.Text} -t {dur} -i \"{Media.Source.LocalPath}\" -vf \"fps={FpsTextBox.Text},scale = {ScaleTextBox.Text}:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{path.ToString()}\"");
            else
                command.Append($"-ss {FromTextBox.Text} -t {dur} -i \"{Media.Source.LocalPath}\" -vf \"fps={FpsTextBox.Text},crop = {WidthTextBox.Text}:{HeightTextBox.Text}:{XTextBox.Text}:{YTextBox.Text},scale = {ScaleTextBox.Text}:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{path.ToString()}\"");
            UpdateLog($"Export Command : {command}\n\n");
            FFMpegHandler.ExecuteFFMpeg(command.ToString());
            Process.Start("explorer.exe", "/select, \"" + Assembly.GetEntryAssembly().Location);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            WidthTextBox.Text = "0";
            HeightTextBox.Text = "0";
            XTextBox.Text = "";
            YTextBox.Text = "";
            RangeRect.Margin = new Thickness(0.0, 0.0, 0.0, 0.0);
            RangeRect.Width = 0;
            RangeRect.Height = 0;
            isMouseDown = false;
        }

        private void ExportTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => isEditingName = false;

        private void ExportTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => isEditingName = true;
        #endregion

        private void Slider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Media.Position = TimeSpan.FromSeconds(Slider.Value);

        #region MEDIA_CONTROL_BUTTON_CALLBACK
        private void PlayButton_Click(object sender, RoutedEventArgs e) => ToggleMediaPlayPause();

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Keyboard.Focus(PlayButton);
                isEditingName = false;
            }

            if (isEditingName)
                return;
            if (e.Key == Key.Space)
                ToggleMediaPlayPause();
            if (e.Key == Key.X)
                Media.Position = TimeSpan.FromSeconds(Slider.Value + fpsSec);
            else if (e.Key == Key.Z)
                Media.Position = TimeSpan.FromSeconds(Slider.Value - fpsSec);
            if(e.Key==Key.C)
                FromTextBox.Text = Media.Position.TotalSeconds.ToString();
            if(e.Key==Key.V)
                ToTextBox.Text = Media.Position.TotalSeconds.ToString();
        }

        private void FromButton_Click(object sender, RoutedEventArgs e) => FromTextBox.Text = Media.Position.TotalSeconds.ToString();

        private void ToButton_Click(object sender, RoutedEventArgs e) => ToTextBox.Text = Media.Position.TotalSeconds.ToString();
        #endregion

        #region RECT_CALLBACK
        private void Media_MediaOpened(object sender, RoutedEventArgs e)
        {
            Media.LoadedBehavior = MediaState.Manual;
            Media.Pause();
            PlayButton.Content = "Play";
            if (Media.NaturalDuration.HasTimeSpan)
            {
                UpdateLog($"Open File Succeess\n");
                ShellFile shellFile = ShellFile.FromFilePath(Media.Source.LocalPath);
                mediaInfo = new MediaInfo(Media.NaturalVideoWidth, Media.NaturalVideoHeight, Media.NaturalDuration.TimeSpan.TotalSeconds, (uint)(shellFile.Properties.System.Video.FrameRate.Value / 1000));
                WidthTextBox.Text = "0";
                HeightTextBox.Text = "0";
                FromTextBox.Text = "0.0";
                ToTextBox.Text = mediaInfo.Duration.ToString();
                FpsTextBox.Text = mediaInfo.FrameRate.ToString();
                Slider.Maximum = mediaInfo.Duration;

                fpsSec = 1.0 / mediaInfo.FrameRate;

                //Update Media Info
                var info = new StringBuilder($"File Name : {System.IO.Path.GetFileName(Media.Source.LocalPath)}\n");
                info.Append($"Width : {mediaInfo.Width}\n");
                info.Append($"Height : {mediaInfo.Height}\n");
                info.Append($"Duration : {TimeSpan.FromSeconds(mediaInfo.Duration).ToString(@"mm\:ss")}\n");
                info.Append($"Fps : {mediaInfo.FrameRate}\n");
                FileInfoTextBlock.Text = info.ToString();
            }
        }

        private void Media_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown)
            {
                var currentPos = Mouse.GetPosition(MediaRect);
                var hw = currentPos - rectStart;
                RangeRect.Width = Math.Abs(hw.X);
                RangeRect.Height = Math.Abs(hw.Y);
            }
        }

        private void Media_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isMouseDown)
            {
                isMouseDown = true;
                rectStart = Mouse.GetPosition(MediaRect);
                RangeRect.Margin = new Thickness(rectStart.X, rectStart.Y, 0.0, 0.0);
                RangeRect.Width = 0;
                RangeRect.Height = 0;
                var mousePoint = Mouse.GetPosition(Media);
                XTextBox.Text = Convert.ToInt32(mousePoint.X * mediaInfo.Width / Media.ActualWidth).ToString();
                YTextBox.Text = Convert.ToInt32(mousePoint.Y * mediaInfo.Height / Media.ActualHeight).ToString();
            }
        }

        private void RangeRect_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isMouseDown)
            {
                WidthTextBox.Text = Convert.ToInt32(RangeRect.ActualWidth * mediaInfo.Width / Media.ActualWidth).ToString();
                HeightTextBox.Text = Convert.ToInt32(RangeRect.ActualHeight * mediaInfo.Height / Media.ActualHeight).ToString();
                isMouseDown = false;
            }
        }
        #endregion

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (Media.Source != null)
            {
                Slider.Value = Media.Position.TotalSeconds;
                if (Media.NaturalDuration.HasTimeSpan)
                    TimeTextBox.Text = $"{Media.Position.TotalSeconds.ToString("0.000")} sec \\ {Media.NaturalDuration.TimeSpan.TotalSeconds.ToString("0.000")} sec";
            }
        }

        private void ToggleMediaPlayPause()
        {
            if (Media.LoadedBehavior != MediaState.Manual)
                return;
            isPlay = !isPlay;
            if (isPlay)
            {
                Media.Play();
                PlayButton.Content = "Pause";
            }
            else
            {
                Media.Pause();
                PlayButton.Content = "Play";
            }
        }

        private void UpdateLog(string context, bool isClear = false)
        {
            if (isClear)
                log.Clear();
            OutputLog.Text = log.Append(context).ToString();

        }


    }

    class MediaInfo
    {
        public double Duration { get; private set; } = 0.0;
        public uint FrameRate { get; private set; } = 0;
        public int Width { get; private set; } = 0;
        public int Height { get; private set; } = 0;

        public MediaInfo(int width, int height, double duration, uint frameRate)
        {
            Width = width;
            Height = height;
            Duration = duration;
            FrameRate = frameRate;
        }
    }
}