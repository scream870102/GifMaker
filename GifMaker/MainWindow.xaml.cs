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
        Rect rect = null;
        const double sliderTimerTick = 0.001;
        StringBuilder log = new StringBuilder();
        DispatcherTimer timer = null;
        Point rectStart = default(Point);
        bool isMouseDown = false;
        double fpsSec = 0.0;
        bool isPlay = false;
        bool isEditingName = false;
        bool isMosueOnSlider = false;

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
            Slider.MouseEnter += Slider_MouseEnter;
            Slider.MouseLeave += Slider_MouseLeave;

            //Media Control Button Bind
            PlayButton.Click += PlayButton_Click;
            KeyDown += MainWindow_KeyDown;
            FromButton.Click += FromButton_Click;
            ToButton.Click += ToButton_Click;
            SeekButton.Click += SeekButton_Click;

            //Rect Event Bind
            Media.MediaOpened += Media_MediaOpened;
            Media.MouseMove += Media_MouseMove;
            Media.MouseLeftButtonDown += Media_MouseLeftButtonDown;
            Media.MouseRightButtonDown += Media_MouseRightButtonDown;
            RangeRect.MouseLeftButtonDown += RangeRect_MouseLeftButtonDown;
            RangeRect.MouseRightButtonDown += RangeRect_MouseRightButtonDown;
            MediaRect.MouseLeave += MediaRect_MouseLeave;
            XTextBox.TextChanged += XTextBox_TextChanged;
            YTextBox.TextChanged += YTextBox_TextChanged;
            WidthTextBox.TextChanged += WidthTextBox_TextChanged;
            HeightTextBox.TextChanged += HeightTextBox_TextChanged;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(sliderTimerTick);
            timer.Tick += Timer_Tick;
            timer.Start();

            UpdateLog($"", true);

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
            FromTextBox.Text = "0.0";
            ToTextBox.Text = mediaInfo?.Duration.ToString() ?? "0.0";
            rect.Reset();
            UpdateRect();
            FpsTextBox.Text = mediaInfo?.FrameRate.ToString() ?? "0";
            ScaleTextBox.Text = "-1:-1";
            isMouseDown = false;
            UpdateLog("Clear Setting\n");
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var path = new StringBuilder(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
            path.Append($"\\{ExportTextBox.Text}.gif");
            var command = new StringBuilder();
            var dur = Convert.ToDouble(ToTextBox.Text) - Convert.ToDouble(FromTextBox.Text);
            if (rect.Width == 0 || rect.Height == 0)
            {
                var scaleProps = ScaleTextBox.Text.Split(':');
                var sW = Convert.ToInt32(scaleProps[0]);
                var sH = Convert.ToInt32(scaleProps[1]);
                sW = sW == -1 ? (int)mediaInfo.Width : sW;
                var sFactor = (double)sW / mediaInfo.Width;
                sH = sH == -1 ? (int)(mediaInfo.Height * sFactor) : sH;
                var scale = sW.ToString() + ":" + sH.ToString();

                command.Append($"-ss {FromTextBox.Text} -t {dur} -i \"{Media.Source.LocalPath}\" -vf \"fps={FpsTextBox.Text},scale = {scale}:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{path.ToString()}\"");
            }
            else
            {
                var x = rect.X / mediaInfo.WidthFactor;
                var y = rect.Y / mediaInfo.HeightFactor;
                var w = rect.Width/mediaInfo.WidthFactor;
                var h = rect.Height/mediaInfo.HeightFactor;

                var scaleProps = ScaleTextBox.Text.Split(':');
                var sW = Convert.ToInt32(scaleProps[0]);
                var sH = Convert.ToInt32(scaleProps[1]);
                sW = sW == -1 ? (int)rect.Width : sW;
                var sFactor = (double)sW / rect.Width;
                sH = sH == -1 ? (int)(rect.Height*sFactor) : sH;
                var scale = sW.ToString() + ":" + sH.ToString();
                command.Append($"-ss {FromTextBox.Text} -t {dur} -i \"{Media.Source.LocalPath}\" -vf \"fps={FpsTextBox.Text},crop = {w}:{h}:{x}:{y},scale = {scale}:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{path.ToString()}\"");
            }
            UpdateLog($"Export Command : {command}\n\n");
            FFMpegHandler.ExecuteFFMpeg(command.ToString());
            Process.Start("explorer.exe", "/select, \"" + Assembly.GetEntryAssembly().Location);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e) => ResetRect();

        private void ExportTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => isEditingName = false;

        private void ExportTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => isEditingName = true;
        #endregion

        private void Slider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Media.Position = TimeSpan.FromSeconds(Slider.Value);

        private void Slider_MouseLeave(object sender, MouseEventArgs e) => isMosueOnSlider = false;

        private void Slider_MouseEnter(object sender, MouseEventArgs e) => isMosueOnSlider = true;


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
            if (e.Key == Key.C)
                FromTextBox.Text = GetCurrentMediaPosition();
            if (e.Key == Key.V)
                ToTextBox.Text = GetCurrentMediaPosition();
        }

        private void FromButton_Click(object sender, RoutedEventArgs e) => FromTextBox.Text = GetCurrentMediaPosition();

        private void ToButton_Click(object sender, RoutedEventArgs e) => ToTextBox.Text = GetCurrentMediaPosition();

        private void SeekButton_Click(object sender, RoutedEventArgs e) => Media.Position = TimeSpan.FromSeconds(Convert.ToDouble(SeekTimeTextBox.Text));
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
                var width = shellFile.Properties.System.Video.FrameWidth.Value.Value;
                var height = shellFile.Properties.System.Video.FrameHeight.Value.Value;
                mediaInfo = new MediaInfo((uint)Media.NaturalVideoWidth, (uint)Media.NaturalVideoHeight, Media.NaturalDuration.TimeSpan.TotalSeconds, (uint)(shellFile.Properties.System.Video.FrameRate.Value / 1000),width,height);
                rect = new Rect(mediaInfo, Media);
                UpdateRect();
                FromTextBox.Text = "0.0";
                ToTextBox.Text = mediaInfo.Duration.ToString();
                ScaleTextBox.Text = "-1:-1";
                FpsTextBox.Text = mediaInfo.FrameRate.ToString();
                Slider.Maximum = mediaInfo.Duration;
                fpsSec = 1.0 / mediaInfo.FrameRate;

                //Update Media Info
                var info = new StringBuilder($"File Name : {System.IO.Path.GetFileName(Media.Source.LocalPath)}\n");
                info.Append($"Width : {mediaInfo.Width}\n");
                info.Append($"Height : {mediaInfo.Height}\n");
                info.Append($"Width Factor : {mediaInfo.WidthFactor.ToString("0.000")}\n");
                info.Append($"Height Factor : {mediaInfo.HeightFactor.ToString("0.000")}\n");
                info.Append($"Duration : {TimeSpan.FromSeconds(mediaInfo.Duration).ToString(@"mm\:ss")}\n");
                info.Append($"Fps : {mediaInfo.FrameRate}\n");
                info.Append($"Frame/sec : {fpsSec.ToString("0.000")} sec\n");
                FileInfoTextBlock.Text = info.ToString();
                
            }
        }

        private void Media_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown)
            {
                var rectEnd = Mouse.GetPosition(MediaRect);
                var hw = rectEnd - rectStart;
                hw.X = Math.Abs(hw.X);
                hw.Y = Math.Abs(hw.Y);
                rect.Offset = new Point(hw.X, hw.Y);

                var tmp = default(Point);
                tmp.X = Math.Min(rectStart.X, rectEnd.X);
                tmp.Y = Math.Min(rectStart.Y, rectEnd.Y);
                rect.LeftTopPoint = new Point(tmp.X, tmp.Y);
                UpdateRect();

            }
        }

        private void Media_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isMouseDown)
            {
                rect.Reset();
                isMouseDown = true;
                rectStart = Mouse.GetPosition(MediaRect);
                rect.LeftTopPoint = rectStart;
                UpdateRect();
            }
        }

        private void Media_MouseRightButtonDown(object sender, MouseButtonEventArgs e) => ResetRect();

        private void RangeRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => isMouseDown = false;

        private void RangeRect_MouseRightButtonDown(object sender, MouseButtonEventArgs e) => ResetRect();

        private void MediaRect_MouseLeave(object sender, MouseEventArgs e) => isMouseDown = false;

        private void XTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Keyboard.FocusedElement != XTextBox)
                return;
            uint res;
            if (uint.TryParse(XTextBox.Text, out res))
            {
                res = Math.Clamp(res, 0, (uint)mediaInfo.Width-rect.Width);
                rect.X = res;
            }
            UpdateRect();
        }

        private void YTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Keyboard.FocusedElement != YTextBox)
                return;
            uint res;
            if (uint.TryParse(YTextBox.Text, out res))
            {
                res =Math.Clamp(res, 0, (uint)mediaInfo.Height-rect.Height);
                rect.Y = res;
            }
            UpdateRect();
        }

        private void WidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Keyboard.FocusedElement != WidthTextBox)
                return;
            uint res;
            if (uint.TryParse(WidthTextBox.Text, out res))
            {
                res = Math.Clamp(res, 0, (uint)mediaInfo.Width-rect.X);
                rect.Width = res;
            }
            UpdateRect();
        }

        private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Keyboard.FocusedElement != HeightTextBox)
                return;
            uint res;
            if (uint.TryParse(HeightTextBox.Text, out res))
            {
                res = Math.Clamp(res, 0, (uint)mediaInfo.Height-rect.Y);
                rect.Height = res;
            }
            UpdateRect();
        }

        #endregion

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (Media.Source != null)
            {
                if (!isMosueOnSlider)
                    Slider.Value = Media.Position.TotalSeconds;
                if (Media.NaturalDuration.HasTimeSpan)
                    TimeTextBox.Text = $"{GetCurrentMediaPosition()} \\ {Media.NaturalDuration.TimeSpan.TotalSeconds.ToString("0.000")} sec";
            }
        }

        private string GetCurrentMediaPosition() => (Media.Position.TotalSeconds).ToString("0.000");

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
            LogScrollViewer.ScrollToBottom();
        }

        private void ResetRect()
        {
            rect.Reset();
            UpdateRect();
            isMouseDown = false;
        }

        void UpdateRect()
        {
            RangeRect.Margin = new Thickness(rect.LeftTopPoint.X, rect.LeftTopPoint.Y, 0.0, 0.0);
            RangeRect.Width = rect.Offset.X;
            RangeRect.Height = rect.Offset.Y;

            XTextBox.Text = rect.X.ToString();
            YTextBox.Text = rect.Y.ToString();
            WidthTextBox.Text = rect.Width.ToString();
            HeightTextBox.Text = rect.Height.ToString();
        }


    }

    class MediaInfo
    {
        public double Duration { get; private set; } = 0.0;
        public uint FrameRate { get; private set; } = 0;
        public uint Width { get; private set; } = 0;
        public uint Height { get; private set; } = 0;
        public double WidthFactor { get; private set; } = 0.0;
        public double HeightFactor { get; private set; } = 0.0;

        public MediaInfo(uint width, uint height, double duration, uint frameRate,uint actualWidth,uint actulaHeight)
        {
            Width = width;
            Height = height;
            Duration = duration;
            FrameRate = frameRate;
            WidthFactor = (double)width / actualWidth;
            HeightFactor = (double)height / actulaHeight;
        }
    }

    class Rect
    {
        uint x = 0;
        uint y = 0;
        uint width = 0;
        uint height = 0;
        Point leftTopPoint = default(Point);
        Point offset = default(Point);
        public uint X
        {
            get => x;
            set
            {
                x = value;
                UpdateLeftTopPoint();
            }
        }
        public uint Y
        {
            get => y; 
            set
            {
                y = value;
                UpdateLeftTopPoint();
            }
        }
        public uint Width
        {
            get => width; 
            set
            {
                width = value;
                UpdateOffset();
            }
        }
        public uint Height
        {
            get => height; 
            set
            {
                height = value;
                UpdateOffset();
            }
        }
        public Point LeftTopPoint
        {
            get => leftTopPoint; 
            set
            {
                leftTopPoint = value;
                UpdateXY();
            }
        }
        public Point Offset
        {
            get => offset; 
            set
            {
                offset = value;
                UpdateWH();
            }
        }

        MediaInfo mediaInfo = null;
        MediaElement mediaElement = null;

        public Rect(MediaInfo mediaInfo, MediaElement mediaElement)
        {
            this.mediaInfo = mediaInfo;
            this.mediaElement = mediaElement;
        }

        public void Reset()
        {
            x = 0;
            y = 0;
            width = 0;
            height = 0;
            leftTopPoint = default(Point);
            offset = default(Point);
        }

        void UpdateXY()
        {
            x = Convert.ToUInt32(LeftTopPoint.X * mediaInfo.Width / mediaElement.ActualWidth);
            y = Convert.ToUInt32(LeftTopPoint.Y * mediaInfo.Height / mediaElement.ActualHeight);
        }

        void UpdateWH()
        {
            width = Convert.ToUInt32(Offset.X * mediaInfo.Width / mediaElement.ActualWidth);
            height = Convert.ToUInt32(Offset.Y * mediaInfo.Height / mediaElement.ActualHeight);
        }

        void UpdateLeftTopPoint()
        {
            var lx = X * mediaElement.ActualWidth / mediaInfo.Width;
            var ly = Y * mediaElement.ActualHeight / mediaInfo.Height;
            leftTopPoint = new Point(lx, ly);
        }

        void UpdateOffset()
        {
            var ox = Width * mediaElement.ActualWidth / mediaInfo.Width;
            var oy = Height * mediaElement.ActualHeight / mediaInfo.Height;
            offset = new Point(ox, oy);
        }
    }
}