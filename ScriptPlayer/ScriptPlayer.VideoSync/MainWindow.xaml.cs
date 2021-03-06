﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Scripts;
using ScriptPlayer.VideoSync.Dialogs;
using Brush = System.Windows.Media.Brush;

namespace ScriptPlayer.VideoSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty SpeedRatioModifierProperty = DependencyProperty.Register(
            "SpeedRatioModifier", typeof(int), typeof(MainWindow), new PropertyMetadata(0, OnSpeedRatioModifierPropertyChanged));

        private static void OnSpeedRatioModifierPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainWindow)d).SpeedRatioModifierChanged();
        }

        private void SpeedRatioModifierChanged()
        {
            double value = 1;
            int modifier = SpeedRatioModifier;
            while (modifier < 0)
            {
                value /= 2;
                modifier++;
            }

            while (modifier > 0)
            {
                value *= 2;
                modifier--;
            }

            videoPlayer.SpeedRatio = value;
        }

        public int SpeedRatioModifier
        {
            get { return (int)GetValue(SpeedRatioModifierProperty); }
            set { SetValue(SpeedRatioModifierProperty, value); }
        }

        public static readonly DependencyProperty PositionsProperty = DependencyProperty.Register(
            "Positions", typeof(PositionCollection), typeof(MainWindow), new PropertyMetadata(default(PositionCollection)));

        public PositionCollection Positions
        {
            get { return (PositionCollection)GetValue(PositionsProperty); }
            set { SetValue(PositionsProperty, value); }
        }

        public static readonly DependencyProperty BeatBarDurationProperty = DependencyProperty.Register(
            "BeatBarDuration", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata(TimeSpan.FromSeconds(5.0)));

        public TimeSpan BeatBarDuration
        {
            get { return (TimeSpan)GetValue(BeatBarDurationProperty); }
            set { SetValue(BeatBarDurationProperty, value); }
        }

        public static readonly DependencyProperty BeatBarCenterProperty = DependencyProperty.Register(
            "BeatBarCenter", typeof(double), typeof(MainWindow), new PropertyMetadata(0.5));

        public double BeatBarCenter
        {
            get { return (double)GetValue(BeatBarCenterProperty); }
            set { SetValue(BeatBarCenterProperty, value); }
        }

        public static readonly DependencyProperty SampleXProperty = DependencyProperty.Register(
            "SampleX", typeof(int), typeof(MainWindow), new PropertyMetadata(680, OnSampleSizeChanged));

        private static void OnSampleSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainWindow)d).UpdateSampleSize();
        }

        private void UpdateSampleSize()
        {
            var rect = new Int32Rect(SampleX, SampleY, SampleW, SampleH);
            if (rect == _captureRect) return;

            SetCaptureRect(rect);
        }

        public int SampleX
        {
            get { return (int)GetValue(SampleXProperty); }
            set { SetValue(SampleXProperty, value); }
        }

        public static readonly DependencyProperty SampleYProperty = DependencyProperty.Register(
            "SampleY", typeof(int), typeof(MainWindow), new PropertyMetadata(700, OnSampleSizeChanged));

        public int SampleY
        {
            get { return (int)GetValue(SampleYProperty); }
            set { SetValue(SampleYProperty, value); }
        }

        public static readonly DependencyProperty SampleWProperty = DependencyProperty.Register(
            "SampleW", typeof(int), typeof(MainWindow), new PropertyMetadata(4, OnSampleSizeChanged));

        public int SampleW
        {
            get { return (int)GetValue(SampleWProperty); }
            set { SetValue(SampleWProperty, value); }
        }

        public static readonly DependencyProperty SampleHProperty = DependencyProperty.Register(
            "SampleH", typeof(int), typeof(MainWindow), new PropertyMetadata(4, OnSampleSizeChanged));

        public int SampleH
        {
            get { return (int)GetValue(SampleHProperty); }
            set { SetValue(SampleHProperty, value); }
        }

        public static readonly DependencyProperty BookmarksProperty = DependencyProperty.Register(
            "Bookmarks", typeof(List<TimeSpan>), typeof(MainWindow), new PropertyMetadata(default(List<TimeSpan>)));

        public List<TimeSpan> Bookmarks
        {
            get { return (List<TimeSpan>)GetValue(BookmarksProperty); }
            set { SetValue(BookmarksProperty, value); }
        }

        public static readonly DependencyProperty SamplerProperty = DependencyProperty.Register(
            "Sampler", typeof(ColorSampler), typeof(MainWindow), new PropertyMetadata(default(ColorSampler)));

        public ColorSampler Sampler
        {
            get { return (ColorSampler)GetValue(SamplerProperty); }
            set { SetValue(SamplerProperty, value); }
        }

        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
            "Duration", typeof(double), typeof(MainWindow), new PropertyMetadata(default(double)));

        public double Duration
        {
            get { return (double)GetValue(DurationProperty); }
            set { SetValue(DurationProperty, value); }
        }

        private RenderTargetBitmap _bitmap;
        private DrawingVisual _drawing;

        public static readonly DependencyProperty BeatsProperty = DependencyProperty.Register(
            "Beats", typeof(BeatCollection), typeof(MainWindow), new PropertyMetadata(default(BeatCollection)));

        public BeatCollection Beats
        {
            get { return (BeatCollection)GetValue(BeatsProperty); }
            set { SetValue(BeatsProperty, value); }
        }


        public static readonly DependencyProperty BeatCountProperty = DependencyProperty.Register(
            "BeatCount", typeof(int), typeof(MainWindow), new PropertyMetadata(default(int)));

        public int BeatCount
        {
            get { return (int)GetValue(BeatCountProperty); }
            set { SetValue(BeatCountProperty, value); }
        }


        public static readonly DependencyProperty PixelPreviewProperty = DependencyProperty.Register(
            "PixelPreview", typeof(Brush), typeof(MainWindow), new PropertyMetadata(default(Brush)));

        private bool _active;
        private bool _up;

        public Brush PixelPreview
        {
            get { return (Brush)GetValue(PixelPreviewProperty); }
            set { SetValue(PixelPreviewProperty, value); }
        }

        public MainWindow()
        {
            Bookmarks = new List<TimeSpan>();
            SetAllBeats(new BeatCollection());
            InitializeComponent();
        }

        private void InitializeSampler()
        {
            Sampler = new ColorSampler();
            Sampler.BeatDetected += SamplerOnBeatDetected;
            Sampler.Sample = _captureRect;

            RefreshSampler();
        }

        private void RefreshSampler()
        {
            Sampler.Resolution = videoPlayer.Resolution;
            Sampler.Source = videoPlayer.VideoBrush;
            Sampler.TimeSource = videoPlayer.TimeSource;
            Sampler.Refresh();
        }

        private void SamplerOnBeatDetected(object sender, TimeSpan d)
        {
            if (cckRecord.IsChecked != true) return;
            AddBeat(d);
            BeatCount = Beats.Count;
        }

        private Int32Rect _captureRect = new Int32Rect(680, 700, 4, 4);
        private bool _wasplaying;

        private string _videoFile;
        private string _projectFile;

        private FrameCaptureCollection _frameSamples;
        private BeatCollection _originalBeats;
        private PixelColorSampleCondition _condition = new PixelColorSampleCondition();

        private TimeSpan _stretchFromBegin;
        private TimeSpan _stretchFromEnd;
        private TimeSpan _stretchToEnd;
        private TimeSpan _stretchToBegin;
        private TimeSpan _marker1;
        private TimeSpan _marker2;

        private void AddBeat(TimeSpan positionTotalSeconds)
        {
            Debug.WriteLine(positionTotalSeconds);
            Beats.Add(positionTotalSeconds);
            Beats.Sort();
        }


        private readonly string[] _supportedVideoExtensions = { "mp4", "mpg", "mpeg", "m4v", "avi", "mkv", "mp4v", "mov", "wmv", "asf" };
        private void mnuOpenVideo_Click(object sender, RoutedEventArgs e)
        {
            string videoFilters = String.Join(";", _supportedVideoExtensions.Select(v => $"*.{v}"));

            OpenFileDialog dialog = new OpenFileDialog { Filter = $"Videos|{videoFilters}|All Files|*.*" };
            if (dialog.ShowDialog(this) != true) return;

            OpenVideo(dialog.FileName, true, true);
        }

        private void SeekBar_OnSeek(object sender, double relative, TimeSpan absolute, int downMoveUp)
        {
            switch (downMoveUp)
            {
                case 0:
                    _wasplaying = videoPlayer.IsPlaying;
                    videoPlayer.Pause();
                    videoPlayer.SetPosition(absolute);
                    break;
                case 1:
                    videoPlayer.SetPosition(absolute);
                    break;
                case 2:
                    videoPlayer.SetPosition(absolute);
                    if (_wasplaying)
                        videoPlayer.Play();
                    break;
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            videoPlayer.Pause();
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            videoPlayer.Play();
        }

        private void mnuClear_Click(object sender, RoutedEventArgs e)
        {
            Beats.Clear();
        }

        private void mnuSaveBeats_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_videoFile)
            };
            dialog.Filter = "Text-File|*.txt";
            if (dialog.ShowDialog(this) != true) return;

            SaveAsBeatsFile(dialog.FileName);
        }

        private void SaveAsBeatsFile(string filename)
        {
            Beats.Save(filename);
        }

        private void mnuLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Text-File|*.txt";
            if (dialog.ShowDialog(this) != true) return;
            LoadBeatsFile(dialog.FileName);
        }

        private void LoadBeatsFile(string filename)
        {
            SetAllBeats(BeatCollection.Load(filename));
        }

        private void videoPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeSampler();
        }

        private void VideoPlayer_OnMediaOpened(object sender, EventArgs e)
        {
            RefreshSampler();
        }

        private void videoPlayer_VideoMouseDown(object sender, int x, int y)
        {
            SetCaptureRect(new Int32Rect(x, y, SampleW, SampleH));
        }

        private void SetCaptureRect(Int32Rect rect)
        {
            _captureRect = rect;

            SampleX = rect.X;
            SampleY = rect.Y;
            SampleW = rect.Width;
            SampleH = rect.Height;

            Debug.WriteLine($"Set Sample Area to: {_captureRect.X} / {_captureRect.Y} ({_captureRect.Width} x {_captureRect.Height})");
            Sampler.Sample = _captureRect;
            videoPlayer.SampleRect = new Rect(_captureRect.X, _captureRect.Y, _captureRect.Width, _captureRect.Height);
        }

        private void VideoPlayer_OnVideoMouseUp(object sender, int x, int y)
        {
            //throw new NotImplementedException();
        }

        private void mnuFrameSampler_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FrameSamplerDialog(_videoFile, _captureRect);
            if (dialog.ShowDialog() != true) return;

            SetSamples(dialog.Result);
        }

        private void mnuSaveSamples_Click(object sender, RoutedEventArgs e)
        {
            if (_frameSamples == null)
            {
                MessageBox.Show("No Samples loaded!");
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Frame Sample Files|*.framesamples|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile)
            };

            if (dialog.ShowDialog(this) != true) return;

            _frameSamples.SaveToFile(dialog.FileName);
        }

        private void mnuLoadSamples_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Frame Sample Files|*.framesamples|All Files|*.*" };

            if (dialog.ShowDialog(this) != true) return;

            SetSamples(FrameCaptureCollection.FromFile(dialog.FileName));

            if (_videoFile != _frameSamples.VideoFile)
            {
                if (MessageBox.Show(this, "Load '" + _frameSamples.VideoFile + "'?", "Open associated file?",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    OpenVideo(_frameSamples.VideoFile, true, false);
                }
            }
        }

        private void SetSamples(FrameCaptureCollection frames)
        {
            _frameSamples = frames;
            SetCaptureRect(_frameSamples.CaptureRect);
            colorSampleBar.Frames = _frameSamples;
        }

        private void OpenVideo(string videoFile, bool play, bool resetFileNames)
        {
            _videoFile = videoFile;
            videoPlayer.Open(videoFile);

            SetTitle(videoFile);

            if (play)
                videoPlayer.Play();

            if (resetFileNames)
            {
                _projectFile = null;
            }
        }

        private void mnuAnalyseSamples_Click(object sender, RoutedEventArgs e)
        {
            AnalyseSamples();
        }

        private void AnalyseSamples()
        {
            if (_frameSamples == null)
            {
                MessageBox.Show(this, "No samples to analyse!");
                return;
            }

            AnalysisParameters parameters = new AnalysisParameters() { MaxPositiveSamples = 10 };
            FrameAnalyserDialog dialog = new FrameAnalyserDialog(_frameSamples, _condition, parameters);

            if (dialog.ShowDialog() != true) return;

            SetAllBeats(dialog.Result);
        }

        private void SetAllBeats(IEnumerable<TimeSpan> beats)
        {
            _originalBeats = new BeatCollection(beats);
            Beats = _originalBeats.Duplicate();
        }

        private double GetDouble(double defaultValue)
        {
            DoubleInputDialog dialog = new DoubleInputDialog(defaultValue) { Owner = this };
            dialog.Owner = this;
            if (dialog.ShowDialog() != true)
                return double.NaN;

            return dialog.Result;
        }

        private void mnuScale_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBeats == null) return;
            double scale = GetDouble(1);
            if (double.IsNaN(scale)) return;

            Beats = Beats.Scale(scale);
        }

        private void mnuShift_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBeats == null) return;
            double shift = GetDouble(0);
            if (double.IsNaN(shift)) return;

            Beats = Beats.Shift(TimeSpan.FromSeconds(shift));
        }

        private void mnuReset_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBeats == null) return;
            Beats = _originalBeats.Duplicate();
        }

        private void btnAddBookmark_Click(object sender, RoutedEventArgs e)
        {
            List<TimeSpan> newBookMarks = new List<TimeSpan>(Bookmarks);
            newBookMarks.Add(videoPlayer.GetPosition());
            newBookMarks.Sort();

            Bookmarks = newBookMarks;
        }

        private void btnResetBookmarks_Click(object sender, RoutedEventArgs e)
        {
            Bookmarks = new List<TimeSpan>();
        }

        private void Bookmark_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = sender as ListBoxItem;
            if (item == null) return;

            if (!(item.DataContext is TimeSpan)) return;

            TimeSpan position = (TimeSpan)item.DataContext;

            videoPlayer.SetPosition(position);
        }

        private void mnuSetCondition_Click(object sender, RoutedEventArgs e)
        {
            var currentCondition = _condition;
            ConditionEditorDialog dialog = new ConditionEditorDialog(currentCondition);
            dialog.LiveConditionUpdate += DialogOnLiveConditionUpdate;

            if (dialog.ShowDialog() != true)
            {
                SetCondition(currentCondition);
            }
            else
            {
                SetCondition(dialog.Result);
                if (dialog.Reanalyse)
                    AnalyseSamples();
            }

        }

        private void DialogOnLiveConditionUpdate(object sender, PixelColorSampleCondition condition)
        {
            SetCondition(condition);
        }

        private void SetCondition(PixelColorSampleCondition condition)
        {
            _condition = condition;
            colorSampleBar.SampleCondition = condition;
        }

        private void ShiftTime(TimeSpan timespan)
        {
            videoPlayer.SetPosition(videoPlayer.GetPosition() + timespan);
        }


        private void btnBeatBarDurationBack_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(-BeatBarDuration);
        }

        private void btnBeatbarDurationForward_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(BeatBarDuration);
        }

        private void btnPreviousBookmark_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMinutes(-1));
        }

        private void btnSecondBack_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromSeconds(-1));
        }

        private void btnFrameBack_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMilliseconds(-10));
        }

        private void btnFrameForward_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMilliseconds(10));
        }

        private void btnSecondForward_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromSeconds(1));
        }

        private void btnNextBookmark_Click(object sender, RoutedEventArgs e)
        {
            ShiftTime(TimeSpan.FromMinutes(1));
        }
        private void btnMarker1_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(1, videoPlayer.GetPosition());
        }

        private void SetMarker(int index, TimeSpan position)
        {
            switch (index)
            {
                case 1:
                    _marker1 = position;
                    BeatBar.Marker1 = _marker1;
                    break;
                case 2:
                    _marker2 = position;
                    BeatBar.Marker2 = _marker2;
                    break;
            }
        }

        private void btnMarker2_Click(object sender, RoutedEventArgs e)
        {
            SetMarker(2, videoPlayer.GetPosition());
        }

        private void btnStretchFromBegin_Click(object sender, RoutedEventArgs e)
        {
            _stretchFromBegin = videoPlayer.GetPosition();
        }

        private void btnStretchFromEnd_Click(object sender, RoutedEventArgs e)
        {
            _stretchFromEnd = videoPlayer.GetPosition();
        }

        private void btnStretchToEnd_Click(object sender, RoutedEventArgs e)
        {
            _stretchToEnd = videoPlayer.GetPosition();
        }

        private void btnStretchToBegin_Click(object sender, RoutedEventArgs e)
        {
            _stretchToBegin = videoPlayer.GetPosition();
        }

        private void btnStretchExecute_Click(object sender, RoutedEventArgs e)
        {
            TimeSpan durationFrom = _stretchFromEnd - _stretchFromBegin;
            TimeSpan durationTo = _stretchToEnd - _stretchToBegin;

            if (durationTo <= TimeSpan.Zero || durationFrom <= TimeSpan.Zero)
                return;

            double factor = durationTo.Divide(durationFrom);
            TimeSpan shift = _stretchToBegin - _stretchFromBegin.Multiply(factor);

            //TimeSpan newBegin = _stretchFromBegin.Multiply(factor) + shift;
            //TimeSpan newEnd = _stretchFromEnd.Multiply(factor) + shift;

            var newbeats = _originalBeats.Scale(factor).Shift(shift);
            Beats = new BeatCollection(newbeats);
        }

        private void mnuJumpToFirstBeat_Click(object sender, RoutedEventArgs e)
        {
            if (Beats == null) return;
            if (Beats.Count == 0) return;
            TimeSpan beat = Beats.First();
            videoPlayer.SetPosition(beat);
        }

        private void mnuFindShortestBeat_Click(object sender, RoutedEventArgs e)
        {
            if (Beats == null) return;
            if (Beats.Count < 2) return;

            TimeSpan shortest = TimeSpan.MaxValue;
            TimeSpan position = TimeSpan.Zero;

            for (int i = 0; i < Beats.Count - 1; i++)
            {
                TimeSpan duration = Beats[i + 1] - Beats[i];
                if (duration < shortest)
                {
                    shortest = duration;
                    position = Beats[i];
                }
            }

            videoPlayer.SetPosition(position);
            MessageBox.Show("Shortest beat: " + shortest.TotalMilliseconds + " ms");
        }

        private void mnuJumpToLastBeat_Click(object sender, RoutedEventArgs e)
        {
            if (Beats == null) return;
            if (Beats.Count == 0) return;
            TimeSpan beat = Beats.Last();
            videoPlayer.SetPosition(beat);
        }

        private void mnuSaveKiiroo_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Kiiroo JS|*.js|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile)
            };

            if (dialog.ShowDialog(this) != true) return;

            using (StreamWriter writer = new StreamWriter(dialog.FileName, false))
            {
                writer.Write("var kiiroo_subtitles = {");

                CultureInfo culture = new CultureInfo("en-us");

                List<string> commands = new List<string>();

                bool up = false;
                foreach (TimeSpan timestamp in Beats)
                {
                    up ^= true;

                    commands.Add(String.Format(culture, "{0:f2}:{1}", timestamp.TotalSeconds, up ? 4 : 1));
                }

                writer.Write(String.Join(",", commands));

                writer.Write("};");
            }
        }

        private void mnuLoadFun_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog {Filter = "Funscript|*.funscript"};

            if (dialog.ShowDialog(this) != true)
                return;

            FunScriptLoader loader = new FunScriptLoader();
            var actions = loader.Load(dialog.FileName).Cast<FunScriptAction>();

            var pos = actions.Select(s => new TimedPosition()
            {
                TimeStamp = s.TimeStamp,
                Position = s.Position
            });

            Positions = new PositionCollection(pos);
        }

        private void mnuSaveFunscript_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Funscript|*.funscript|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile)
            };

            if (dialog.ShowDialog(this) != true) return;

            SaveAsFunscript(dialog.FileName);
        }

        private void SaveAsFunscript(string filename, int mode = 0)
        {
            FunScriptFile script = new FunScriptFile
            {
                Inverted = false,
                Range = 90,
                Version = new Version(1, 0),
                Actions = BeatsToFunScriptConverter.Convert(Beats, ConversionMode.UpOrDown)
            };

            string content = JsonConvert.SerializeObject(script);

            File.WriteAllText(filename, content, new UTF8Encoding(false));
        }

        private void btnResetSamples_Click(object sender, RoutedEventArgs e)
        {
            if (_frameSamples == null) return;

            SetCaptureRect(_frameSamples.CaptureRect);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteBeatsWithinMarkers();
        }

        private void DeleteBeatsWithinMarkers()
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            SetAllBeats(Beats.Where(t => t < tBegin || t > tEnd));
        }

        private void mnuTrackBlob_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new VisualTrackerDialog(_videoFile, _marker1, _marker2, new Rectangle(SampleX, SampleY, SampleW, SampleH));
            dialog.Show();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            Beats.Add(videoPlayer.GetPosition());
            SetAllBeats(Beats);
        }

        private void mnuShowMock_Click(object sender, RoutedEventArgs e)
        {
            new MocktestDialog().Show();
        }

        private void SaveProjectAs()
        {
            if (String.IsNullOrWhiteSpace(_videoFile))
                return;

            SaveFileDialog dialog = new SaveFileDialog { Filter = "Beat Projects|*.bproj" };
            dialog.FileName = Path.ChangeExtension(_videoFile, ".bproj");

            if (dialog.ShowDialog(this) != true)
                return;

            SaveProjectAs(dialog.FileName);
        }

        private void SaveProjectAs(string filename)
        {
            BeatProject project = new BeatProject
            {
                VideoFile = _videoFile,
                SampleCondition = _condition,
                BeatBarDuration = BeatBar.TotalDisplayedDuration.TotalSeconds,
                BeatBarMidpoint = BeatBar.Midpoint,
                Beats = Beats.Select(b => b.Ticks).ToList(),
                Bookmarks = Bookmarks.Select(b => b.Ticks).ToList()
            };
            project.Save(filename);
            _projectFile = filename;

            SaveAsFunscript(Path.ChangeExtension(_videoFile, "funscript"));
            SaveAsBeatsFile(Path.ChangeExtension(_videoFile, "txt"));
        }

        private void mnuLoadProject_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Beat Projects|*.bproj" };
            if (dialog.ShowDialog(this) != true)
                return;

            BeatProject project = BeatProject.Load(dialog.FileName);

            if (File.Exists(project.VideoFile))
                OpenVideo(project.VideoFile, true, false);
            else
            {
                string otherPath = ChangePath(dialog.FileName, project.VideoFile);
                OpenVideo(otherPath, true, false);
            }

            SetCondition(project.SampleCondition);
            BeatBarDuration = TimeSpan.FromSeconds(project.BeatBarDuration);
            BeatBarCenter = project.BeatBarMidpoint;
            Beats = new BeatCollection(project.Beats.Select(TimeSpan.FromTicks));
            Bookmarks = project.Bookmarks.Select(TimeSpan.FromTicks).ToList();

            _projectFile = dialog.FileName;
        }

        private string ChangePath(string path, string filename)
        {
            string directory = System.IO.Path.GetDirectoryName(path);
            string file = Path.GetFileName(filename);

            return Path.Combine(directory, file);
        }

        private void mnuSaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(_projectFile))
                SaveProjectAs();
            else
                SaveProjectAs(_projectFile);
        }

        private void btnNormalize_Click(object sender, RoutedEventArgs e)
        {
            Normalize();
        }

        private void Normalize()
        {
            bool wasPlaying = videoPlayer.IsPlaying;
            if (wasPlaying)
                videoPlayer.Pause();

            double additionalBeats = GetDouble(0);
            if (double.IsNaN(additionalBeats)) return;

            bool caps = Keyboard.IsKeyToggled(Key.CapsLock);
            Normalize((int)additionalBeats, !caps);

            if (wasPlaying)
                videoPlayer.Play();
        }

        private void Normalize(int additionalBeats, bool trimToBeats = true)
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            List<TimeSpan> beatsToEvenOut = Beats.GetBeats(tBegin, tEnd).ToList();

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            if (beatsToEvenOut.Count < 2 && trimToBeats)
                return;

            TimeSpan first = trimToBeats ? beatsToEvenOut.Min() : tBegin;
            TimeSpan last = trimToBeats ? beatsToEvenOut.Max() : tEnd;

            int numberOfBeats = (int)(beatsToEvenOut.Count + additionalBeats);
            if (numberOfBeats < 2)
                numberOfBeats = 2;

            TimeSpan tStart = first;
            TimeSpan intervall = (last - first).Divide(numberOfBeats - 1);

            for (int i = 0; i < numberOfBeats; i++)
                otherBeats.Add(tStart + intervall.Multiply(i));

            Fadeout.SetText(intervall.TotalMilliseconds.ToString("f0") + "ms", TimeSpan.FromSeconds(4));

            SetAllBeats(otherBeats);
        }

        private void mnuShowBeatDuration_Click(object sender, RoutedEventArgs e)
        {
            TimeSpan previous = TimeSpan.MinValue;
            TimeSpan next = TimeSpan.MaxValue;

            TimeSpan position = videoPlayer.GetPosition();

            foreach (TimeSpan t in Beats)
            {
                if (t < position)
                    previous = t;
                else
                {
                    next = t;
                    break;
                }
            }

            if (previous == TimeSpan.MinValue || next == TimeSpan.MaxValue)
            {
                MessageBox.Show("Can't find adjecent beat!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show((next - previous).TotalMilliseconds.ToString("f0"), "Duration", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool caps = Keyboard.IsKeyToggled(Key.CapsLock);

            bool handled = true;
            switch (e.Key)
            {
                case Key.PageDown:
                    {
                        GotoNextBookMark();
                        break;
                    }
                case Key.PageUp:
                    {
                        GotoPreviousBookMark();
                        break;
                    }
                case Key.Space:
                    {
                        if (videoPlayer.IsPlaying)
                            videoPlayer.Pause();
                        else
                            videoPlayer.Play();
                        break;
                    }
                case Key.Left:
                case Key.Right:
                    {
                        double multiplier = e.Key == Key.Right ? 1 : -1;

                        if (control && shift)
                            ShiftTime(TimeSpan.FromMilliseconds(multiplier * 100));
                        else if (control)
                            ShiftTime(BeatBarDuration.Multiply(multiplier / 2.0));
                        else if (shift)
                            ShiftTime(TimeSpan.FromMilliseconds(multiplier * 20));
                        else
                            ShiftTime(TimeSpan.FromMilliseconds(multiplier * 1000));
                        break;
                    }
                case Key.N:
                    {
                        Normalize();
                        break;
                    }
                case Key.Delete:
                    {
                        DeleteBeatsWithinMarkers();
                        break;
                    }
                case Key.Add:
                    {
                        Normalize(1, !caps);
                        break;
                    }
                case Key.Subtract:
                    {
                        Normalize(-1, !caps);
                        break;
                    }
                case Key.F6:
                    {
                        SnapToClosestBeat();
                        break;
                    }
                case Key.Enter:
                    {
                        if (IsNumpadEnterKey(e))
                        {
                            Normalize(0, !caps);
                        }
                        else
                        {
                            handled = false;
                        }
                        break;
                    }
                default:
                    handled = false;
                    break;
            }

            if (handled)
                e.Handled = true;
        }

        private void SnapToClosestBeat()
        {
            videoPlayer.SetPosition(GetClosestBeat(videoPlayer.GetPosition()));
        }

        private void GotoNextBookMark()
        {
            var pos = videoPlayer.GetPosition();
            if (!Bookmarks.Any(t => t > pos))
                return;

            TimeSpan bookmark = Bookmarks.First(t => t > pos);
            lstBookmarks.SelectedItem = bookmark;
            videoPlayer.SetPosition(bookmark);
        }

        private void GotoPreviousBookMark()
        {
            var pos = videoPlayer.GetPosition();
            if (!Bookmarks.Any(t => t < pos))
                return;

            TimeSpan bookmark = Bookmarks.FindLast(t => t < pos);
            lstBookmarks.SelectedItem = bookmark;
            videoPlayer.SetPosition(bookmark);
        }

        public void SetTitle(string filePath)
        {
            Title = "ScriptPlayer Video Sync - " + Path.GetFileNameWithoutExtension(filePath);
        }

        private void BeatBar_OnTimeMouseDown(object sender, TimeSpan e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                RemoveClosestBeat(e);
            else
            {
                Beats.Add(e);
                Beats.Sort();
            }

            BeatBar.InvalidateVisual();
        }

        private void RemoveClosestBeat(TimeSpan timeSpan)
        {
            TimeSpan closest = GetClosestBeat(timeSpan);
            Beats.Remove(closest);
        }

        private TimeSpan GetClosestBeat(TimeSpan timeSpan)
        {
            return Beats.OrderBy(b => Math.Abs(b.Ticks - timeSpan.Ticks)).First();
        }

        private static bool IsNumpadEnterKey(KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return false;

            // To understand the following UGLY implementation please check this MSDN link. Suggested workaround to differentiate between the Return key and Enter key.
            // https://social.msdn.microsoft.com/Forums/vstudio/en-US/b59e38f1-38a1-4da9-97ab-c9a648e60af5/whats-the-difference-between-keyenter-and-keyreturn?forum=wpf
            try
            {
                return (bool)typeof(KeyEventArgs).InvokeMember("IsExtendedKey", BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance, null, e, null);
            }
            catch (Exception ex)
            {

            }

            return false;
        }

        private void mnuSetMarkerMode_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null)
                return;

            TimeLineHeader header = FindPlacementTarget(menuItem) as TimeLineHeader;
            if (header == null) return;

            header.MarkerMode = (MarkerMode)menuItem.Tag;
        }

        private UIElement FindPlacementTarget(MenuItem sender)
        {
            DependencyObject currentElement = sender;
            while (currentElement != null)
            {
                ContextMenu element = currentElement as ContextMenu;
                if (element != null)
                    return element.PlacementTarget;
                currentElement = VisualTreeHelper.GetParent(currentElement);
            }
            return null;
        }

        private void BeatBar_OnTimeMouseRightDown(object sender, TimeSpan e)
        {
            if (KeepMarkerDuration())
            {
                TimeSpan currentDuration = TimeSpan.FromTicks(Math.Abs(_marker1.Ticks - _marker2.Ticks));

                SetMarker(1, e);
                SetMarker(2, e + currentDuration);
            }
            else
            {
                SetMarker(1, e);
                SetMarker(2, e);
            }
        }

        private void BeatBar_OnTimeMouseRightMove(object sender, TimeSpan e)
        {
            if (KeepMarkerDuration())
            {
                TimeSpan currentDuration = TimeSpan.FromTicks(Math.Abs(_marker1.Ticks - _marker2.Ticks));

                SetMarker(1, e);
                SetMarker(2, e + currentDuration);
            }
            else
            {
                SetMarker(2, e);
            }
        }

        private void BeatBar_OnTimeMouseRightUp(object sender, TimeSpan e)
        {
            if (KeepMarkerDuration())
            {
                TimeSpan currentDuration = TimeSpan.FromTicks(Math.Abs(_marker1.Ticks - _marker2.Ticks));

                SetMarker(1, e);
                SetMarker(2, e + currentDuration);
            }
            else
            {
                SetMarker(2, e);
            }
        }

        private bool KeepMarkerDuration()
        {
            if (_marker1 == TimeSpan.MinValue) return false;
            if (_marker2 == TimeSpan.MinValue) return false;
            if (_marker1 == _marker2) return false;

            return Keyboard.IsKeyToggled(Key.Scroll);
        }

        private void btnLoadBookmarks_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Filter = "Log Files|*.log;*.txt|All Files|*.*"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            string[] lines = File.ReadAllLines(dialog.FileName);

            List<TimeSpan> newBookmarks = new List<TimeSpan>();

            foreach (string line in lines)
            {
                TimeSpan t;
                if (TimeSpan.TryParse(line, out t))
                    newBookmarks.Add(t);
            }

            newBookmarks.Sort();

            Bookmarks = newBookmarks;
        }

        private void CckRTL_OnChecked(object sender, RoutedEventArgs e)
        {
            BeatBar.FlowDirection = FlowDirection.RightToLeft;
        }

        private void CckRTL_OnUnchecked(object sender, RoutedEventArgs e)
        {
            BeatBar.FlowDirection = FlowDirection.LeftToRight;
        }

        private void mnuShiftSelected_Click(object sender, RoutedEventArgs e)
        {
            TimeSpan tBegin = _marker1 < _marker2 ? _marker1 : _marker2;
            TimeSpan tEnd = _marker1 < _marker2 ? _marker2 : _marker1;

            double shiftBy = GetDouble(0.0);
            if (Double.IsNaN(shiftBy))
                return;

            TimeSpan shift = TimeSpan.FromMilliseconds((int)shiftBy);

            List<TimeSpan> beatsToEvenOut = Beats.GetBeats(tBegin, tEnd).Select(b => b + shift).ToList();

            List<TimeSpan> otherBeats = Beats.Where(t => t < tBegin || t > tEnd).ToList();

            SetAllBeats(otherBeats.Concat(beatsToEvenOut));

            _marker1 += shift;
            _marker2 += shift;


            BeatBar.Marker1 = _marker1;
            BeatBar.Marker2 = _marker2;
        }

        private void mnuLoadVorze_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog {Filter = "Vorze Script|*.csv"};

            if (dialog.ShowDialog(this) != true)
                return;

            VorzeScriptLoader loader = new VorzeScriptLoader();
            var actions = loader.Load(dialog.FileName).Cast<VorzeScriptAction>().ToList();

            var filteredActions = new List<VorzeScriptAction>();

            foreach (var action in actions)
            {
                if(filteredActions.Count == 0)
                    filteredActions.Add(action);
                else if (filteredActions.Last().TimeStamp >= action.TimeStamp)
                    continue;
                else
                    filteredActions.Add(action);
            }

            actions = filteredActions;

            List<FunScriptAction> funActions = new List<FunScriptAction>();

            for(int i = 0; i< actions.Count; i++)
            {
                int samePosition = 0;

                for (int j = i + 1; j < actions.Count; j++)
                {
                    if (actions[j].Action == actions[i].Action)
                        samePosition++;
                    else
                        break;
                }

                funActions.Add(new FunScriptAction
                {
                    Position = (byte) (actions[i].Action == 0 ? 95:5),
                    TimeStamp = actions[i + samePosition].TimeStamp
                });
            }

            var pos = funActions.Select(s => new TimedPosition()
            {
                TimeStamp = s.TimeStamp,
                Position = s.Position
            });

            Positions = new PositionCollection(pos);

            SaveFileDialog dialog2 = new SaveFileDialog
            {
                Filter = "Funscript|*.funscript|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_videoFile)
            };

            if (dialog2.ShowDialog(this) != true) return;

            FunScriptFile script = new FunScriptFile
            {
                Inverted = false,
                Range = 90,
                Version = new Version(1, 0),
                Actions = funActions
            };

            string content = JsonConvert.SerializeObject(script);

            File.WriteAllText(dialog2.FileName, content, new UTF8Encoding(false));
        }
    }
}
