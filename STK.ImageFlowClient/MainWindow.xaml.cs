using Microsoft.Win32;
using STK.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace STK.ImageFlowClient
{
    public partial class MainWindow : Window
    {
        private readonly ImageFlowClient flow;
        private readonly Timer 
            timer = new Timer()
            ,uiHideTimer = new Timer(5000) { AutoReset = false };
        private List<string> logMsgs = new List<string>();
        private bool isPlaying = false;
        private readonly SettingsManager settingsmanager;
        private const string EXT = "flow";
        
        public MainWindow()
        {
            Dictionary<string, object> defaultvals = new Dictionary<string, object>();
            defaultvals.Add("SaveLocation", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            defaultvals.Add("SourceURL", "http://konachan.net");
            defaultvals.Add("Tags", new List<string>());
            defaultvals.Add("GoToID", "");
            settingsmanager = new SettingsManager(defaultvals, EXT);

            string[] args = Environment.GetCommandLineArgs();
            /* 0 - FileLocation of executable
             * 1 - (optional) FileLocation of file to open with app
             */
            if (args.Length > 1)
            {
                settingsmanager.Load(new FileInfo(args[1]));
            }


            flow = new ImageFlowClient(new DirectoryInfo(settingsmanager.Get<string>("SaveLocation")));

            InitializeComponent();

            optSaveLocation.Text = settingsmanager.Get<string>("SaveLocation");
            optSourceURL.Text = settingsmanager.Get<string>("SourceURL");
            foreach (string s in settingsmanager.Get<List<string>>("Tags"))
                addTag(s);
            optGoToID.Text = settingsmanager.Get<string>("GoToID");

            timer.Elapsed += timer_Elapsed;
            timer.Interval = 3 * 1000;
            timer.AutoReset = false;
            
            this.WindowState = System.Windows.WindowState.Normal;

            if (!settingsmanager.IsFileAssociationRegisterd &&
                  System.Windows.Forms.MessageBox.Show(String.Format(
                      "Möchten Sie die Dateierweiterung '.{0}' mit diesem Programm verknüpfen?"
                      , EXT
                  ), "Filextension", System.Windows.Forms.MessageBoxButtons.YesNo)
                  == System.Windows.Forms.DialogResult.Yes)
            {
                settingsmanager.RegisterFileAssociation();
            }

            init();
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        { next(); }
        private void Next_Click(object sender, RoutedEventArgs e)
        { stop(); next(); }
        private void next()
        {
            if (!flow.IsInit)
                init();

            view(flow.Next()); if (isPlaying) timer.Start();
        }

        private void PlayStop_Click(object sender, RoutedEventArgs e)
        { playStop(); }
        private void playStop()
        {
            if (isPlaying)
            {
                stop();
            }
            else
            {
                play();
            }
        }
        private void play()
        {
            Dispatcher.Invoke(() => btnPlayStop.Content = "Stop");
            isPlaying = true;
            optGoToID.IsEnabled = false;
            timer.Start();

            hideUI();
            this.MouseMove += MainWindow_MouseMove;
            this.MouseLeave += MainWindow_MouseLeave;
            uiHideTimer.Elapsed += uiHideTimer_Elapsed;
            uiHideTimer.Start();
        }

        void MainWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            hideUI();
        }
        
        private void stop()
        {
            Dispatcher.Invoke(() => btnPlayStop.Content = "Play");
            isPlaying = false;
            optGoToID.IsEnabled = true;
            timer.Stop();

            this.MouseMove -= MainWindow_MouseMove;
            this.MouseLeave -= MainWindow_MouseLeave;
            uiHideTimer.Elapsed -= uiHideTimer_Elapsed;
            uiHideTimer.Stop();
            showUI();
        }

        System.Windows.Point? autoHideUiRefPoint = null;
        void uiHideTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var now = Mouse.GetPosition(this);
                if (autoHideUiRefPoint == null)
                    autoHideUiRefPoint = now;

                if (now.X - autoHideUiRefPoint.Value.X == 0 || now.Y - autoHideUiRefPoint.Value.Y == 0)
                    hideUI();
                else
                {
                    autoHideUiRefPoint = now;
                }
            });
            uiHideTimer.Start();
            
        }

        System.Windows.Point? prev = null;
        void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (prev == null)
                prev = e.GetPosition(this);

            if (Math.Abs(e.GetPosition(this).X - prev.Value.X) > 10 || Math.Abs(e.GetPosition(this).Y - prev.Value.Y) > 10)
                showUI();
        }

        void showUI()
        {
            prev = null;
            layoutGrid.ColumnDefinitions[0].Width = new GridLength(170);
            layoutGrid.RowDefinitions[0].Height = new GridLength(45);
            layoutGrid.RowDefinitions[2].Height = new GridLength(30);
            layoutGrid.Background = new SolidColorBrush(Colors.White);
            Cursor = Cursors.Arrow;
        }

        private void hideUI()
        {
            layoutGrid.ColumnDefinitions[0].Width = new GridLength(0);
            layoutGrid.RowDefinitions[0].Height = new GridLength(0);
            layoutGrid.RowDefinitions[2].Height = new GridLength(0);
            layoutGrid.Background = new SolidColorBrush(Colors.Black);
            Cursor = Cursors.None;
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        { previous(); }
        private void previous()
        { stop(); view(flow.Previous()); }

        FlowImage tmp = null;
        private void view(FlowImage fi)
        {

            string
                id = "none"
                , lnk = "http://sklampt.de"
                , rat = "Other";
            List<string> tags = new List<string>();
            System.Drawing.Image img = new Bitmap(1, 1);
            bool downloaded = false;
            if (fi != null)
            {
                id = fi.ID;
                if (fi.Lnk == null)
                    lnk = "";
                else
                    lnk = fi.Lnk.ToString();
                rat = String.Format("{0}",
                       Enum.GetName(typeof(STK.ImageFlowClient.FlowImage.rating), fi.Rating));
                tags = fi.Tags;
                img = fi.Image;
                downloaded = fi.IsDownloaded;
            }
            MemoryStream ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var s = new BitmapImage();
            s.BeginInit();
            s.StreamSource = ms;
            s.CacheOption = BitmapCacheOption.OnLoad;
            s.EndInit();
            s.Freeze();

            Dispatcher.Invoke(new Action(() =>
            {
                tmp = fi;
                imgInfo.Header = String.Format("Info Box - {0}", id);
                imgInfoLnkLbl.Content = lnk;
                imgInfoRating.Content = rat;

                imgInfoTags.Items.Clear();
                foreach (string tag in tags)
                {
                    Label l = new Label();
                    l.Content = tag;
                    l.MouseDoubleClick += (object sender, MouseButtonEventArgs e) =>
                    {
                        addTag((string)(sender as Label).Content);
                    };
                    imgInfoTags.Items.Add(l);
                }


                imgView.Source = s;

                optGoToID.Text = String.Format("{0}", id);

                isDownloaded.IsChecked = downloaded;

                statisticsDB.Content = String.Format(
                    "{0,7} / {1,7} / {2,7}"
                    , flow.Index + 1
                    , flow.DbCount
                    , flow.DbMaxCount
                    );
            }));


        }

        private void saveLocation_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        { editSaveLocation(); }

        private void editSaveLocation()
        {
            var diag = new System.Windows.Forms.FolderBrowserDialog();
            diag.SelectedPath = optSaveLocation.Text;
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                optSaveLocation.Text = diag.SelectedPath;
            }
        }

        private void LBTB_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox send = (TextBox)sender;

            if (e.Key.CompareTo(Key.Enter) == 0)
            {
                addTag(send.Text);
                send.Text = string.Empty;
            }
        }

        private void addTag(string tag)
        {
            if (string.Empty.CompareTo(tag) != 0)
            {
                DockPanel dp = new DockPanel();

                var lbl = new Label();
                lbl.Content = tag;
                lbl.Width = 55;
                DockPanel.SetDock(lbl, Dock.Left);

                Button del = new Button();
                del.Content = "X";
                del.ToolTip = "Delete  Item";
                del.Width = 25;
                del.Height = 25;
                del.Click += LBTBDel_Click;
                DockPanel.SetDock(del, Dock.Right);

                dp.Children.Add(lbl);
                dp.Children.Add(del);
                optTagsList.Items.Add(dp);
            }
        }

        void LBTBDel_Click(object sender, RoutedEventArgs e)
        {
            string s = (sender as Button).Content as string;
            (((sender as Button).Parent as DockPanel).Parent as ListBox).Items
                .Remove((sender as Button).Parent);
        }

        private void LBTB_Focus(object sender, RoutedEventArgs e)
        {
            TextBox send = (TextBox)sender;

            if (send.Tag is String && send.Text.CompareTo(send.Tag) == 0)
            {
                send.Text = string.Empty;
                send.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void LBTB_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox send = (TextBox)sender;

            if (send.Tag is String && send.Text.CompareTo(send.Tag) != 0)
            {
                send.Text = send.Tag as String;
                send.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        { saveCurrent(); }
        private void saveCurrent()
        {
            try
            {
                tmp.Save();
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("Speicherort nicht existent.", "Speichern nicht möglich");
            }
            isDownloaded.IsChecked = tmp.IsDownloaded;
        }

        private void optGoToID_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key.CompareTo(Key.Enter) == 0)
            {
                Cursor = Cursors.Wait;
                flow.GoToID(optGoToID.Text);
                Cursor = Cursors.Arrow;
            }
        }

        private void safeRatingCheck(object sender, RoutedEventArgs e)
        {
            flow.EnableSafe = ((CheckBox)sender).IsChecked == null ? false : (bool)((CheckBox)sender).IsChecked;
        }
        private void questionableRatingCheck(object sender, RoutedEventArgs e)
        {
            flow.EnableQuestionable = ((CheckBox)sender).IsChecked == null ? false : (bool)((CheckBox)sender).IsChecked;
        }
        private void explicitRatingCheck(object sender, RoutedEventArgs e)
        {
            flow.EnableExplicit = ((CheckBox)sender).IsChecked == null ? false : (bool)((CheckBox)sender).IsChecked;
        }
        private void otherRatingCheck(object sender, RoutedEventArgs e)
        {
            flow.EnableOther = ((CheckBox)sender).IsChecked == null ? false : (bool)((CheckBox)sender).IsChecked;
        }

        private void chkViewAdvanced_Checked(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.MessageBox.Show(
                "Achtung! Wenn Sie hier Änderungen durchführen kann es sein, dass Sie pornographisches oder anderweitiges, nicht für Minderjährige gedachtes, Material downloaden und sichten. Sofern Sie dieses nicht dürfen oder wollen ändern Sie die Daten nicht.",
                "Pornographisches Material");
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (flow.IsInit)
            {
                if (e.Key.CompareTo(Key.S) == 0)
                    saveCurrent();
                if (e.Key.CompareTo(Key.OemMinus) == 0 || e.Key.CompareTo(Key.Subtract) == 0 || e.Key.CompareTo(Key.MediaPreviousTrack) == 0)
                    previous();
                if (e.Key.CompareTo(Key.P) == 0 || e.Key.CompareTo(Key.Multiply) == 0 || e.Key.CompareTo(Key.MediaPlayPause) == 0)
                    playStop();
                if (e.Key.CompareTo(Key.OemPlus) == 0 || e.Key.CompareTo(Key.Add) == 0 || e.Key.CompareTo(Key.MediaNextTrack) == 0)
                {
                    stop();
                    next();
                }
            }
        }

        private void saveCurrentFlow()
        {
            if (
                (settingsmanager.CurrentLoaded != null && (optAutoSave.IsChecked == null ? true : (bool)optAutoSave.IsChecked)) ||
                System.Windows.Forms.MessageBox.Show("Suche speichern?", "Speichern", System.Windows.Forms.MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                settingsmanager.Set("SaveLocation", optSaveLocation.Text);
                settingsmanager.Set("SourceURL", optSourceURL.Text);
                settingsmanager.Set("Tags", flow.Tags);
                settingsmanager.Set("GoToID", optGoToID.Text);

                if (settingsmanager.CurrentLoaded != null)
                    settingsmanager.Save(settingsmanager.CurrentLoaded);
                else
                {
                    string name = "FlowImage_";
                    if (flow.Tags.Count() > 1)
                        name += flow.Tags.Aggregate((a, b) => a + "_" + b);

                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.AddExtension = true;
                    sfd.CheckFileExists = false;
                    sfd.CheckPathExists = true;
                    sfd.ValidateNames = true;
                    sfd.DefaultExt = EXT;
                    sfd.FileName = name;
                    sfd.Filter = String.Format("Image Flow|*.{0}", EXT);
                    sfd.FilterIndex = 0;
                    sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    sfd.Title = "Speichern des ImageFlow";

                    if (sfd.ShowDialog() == true)
                    {
                        settingsmanager.Save(new FileInfo(sfd.FileName));
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            stop();
            saveCurrentFlow();
            Environment.Exit(0); // Exit all threads etc.
        }

        private void btnStartSearch_Click(object sender, RoutedEventArgs e)
        {
            saveCurrentFlow();
            settingsmanager.Reset();
            optGoToID.Text = "";
            init();
        }

        private void init()
        {
            stop();
            Dispatcher.Invoke(() =>
            {
                flow.CacheLocation = new DirectoryInfo(optSaveLocation.Text);
                flow.Source = new Uri(optSourceURL.Text);
                flow.ClearTags();

                foreach (var x in optTagsList.Items)
                {
                    if (x is Panel)
                    {
                        flow.AddTag((((Panel)x).Children[0] as Label).Content as string);
                    }
                }

                flow.Init(optGoToID.Text);
                play();
                next();
            });
        }
    }
}
