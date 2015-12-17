using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.Concurrent;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Threading;
using System.Diagnostics;
using Extension.InstanceTracker.InstanceTrackerEditor;
using System.Net.Mail;

namespace DbxEntityTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EntityDatabase EntityDB;
        private ViewMode _viewMode;

        public MainWindow()
        {
            InitializeComponent();
            Application.Current.DispatcherUnhandledException += onUnhandledException;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { 
                reset(); 
            }));
        }

        void onUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ReportApplicationCrash(e.Exception);
            Utils.AtomicWriteToLog("crashed");
            Application.Current.Shutdown();
        }

        private void onLoad(object sender, RoutedEventArgs e) {
            var start = DateTime.Now;

            if (EntityDB.CanLoadDatabase())
            {
                Utils.AtomicWriteToLog("load");
                SetLoadingVisibility(true);
                var uithread = Application.Current.Dispatcher;

                Task.Run(() =>
                {
                    EntityDB.LoadDatabase();
                    uithread.Invoke(() =>
                    {
                        SetLoadingVisibility(false);
                        _content.Visibility = System.Windows.Visibility.Visible;
                        _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
                        showEntities();
                        Utils.AtomicWriteToLog(String.Format("Populate done, time: {0}ms", GetMillisecondsSinceStart(start)));
                    });
                }
                );
            }
        }

        private void onPopulateClick(object sender, RoutedEventArgs e)
        {
            Utils.AtomicWriteToLog("Populating internal database based on your settings");
            _loadingText.Content = "Populating internal database based on your settings";
            SetLoadingVisibility(true);
        
            var uithread = Application.Current.Dispatcher;
            var start = DateTime.Now;
            Task.Run(() =>
            {
                EntityDB.RefreshDatabase();

                //on complete
                uithread.Invoke(() =>
                {
                    SetLoadingVisibility(false);
                    _content.Visibility = System.Windows.Visibility.Visible;
                    _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
                    showEntities();
                    Utils.AtomicWriteToLog(String.Format("Populate done, time: {0}ms", GetMillisecondsSinceStart(start)));
                });
            });
        }

        private double GetMillisecondsSinceStart(DateTime start)
        {
            return (DateTime.Now - start).TotalMilliseconds;
        }

        private void SetLoadingVisibility(bool flag) {
            var uiElementsEnabled = !flag;
            dbxRoot.IsEnabled = uiElementsEnabled;
            parseButton.IsEnabled = uiElementsEnabled;
            loadButton.IsEnabled = uiElementsEnabled;

            if (flag)
            {
                var startTime = DateTime.Now;
                var s = _loadingText.Content.ToString();
                _loading.Visibility = System.Windows.Visibility.Visible;
                _time.Content = String.Format("Loading: {0}s", (DateTime.Now - startTime).TotalSeconds.ToString("0"));
            } else {
                _loading.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void ReportApplicationCrash(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine(ex.Message);
            if(ex.InnerException != null)
                sb.AppendLine(ex.InnerException.ToString());
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(ex.StackTrace);
            var filename = Utils.NextAvailableFilename("./crash/crashdump.foo");
            var fi = new FileInfo(filename);
            if (!fi.Directory.Exists)
                fi.Directory.Create();
            File.WriteAllText(filename, sb.ToString());
            MessageBox.Show(sb.ToString());
        }

        private void showEntities()
        {
            _content.Visibility = System.Windows.Visibility.Visible;
            _entityTypes.ItemsSource = EntityDB.EntityTypes;
            _references.Items.Clear();
            _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void _entities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_entityTypes.SelectedItem == null)
                return;
            var key = _entityTypes.SelectedItem.ToString();
            _references.ItemsSource = DbxUtils.GetEntities(EntityDB.Entities, key);
            _infoPanel.Text = "";
        }

        private void _references_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var key = _entityTypes.SelectedItem == null ? string.Empty : _entityTypes.SelectedItem.ToString();
            var idx = _references.SelectedIndex;
            if (idx < 0 || String.IsNullOrEmpty(key))
            {
                return;
            }

            var item = _references.SelectedItem as DbxUtils.AssetInstance;
            _infoPanel.Text = DbxUtils.GetAssetDescription(item);
        }

        private void _textFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EntityDB == null || EntityDB.EntityTypes.Count == 0)
                return;
            if (String.IsNullOrWhiteSpace(_textFilter.Text))
            {
                _entityTypes.ItemsSource = EntityDB.EntityTypes;
                _entityTypes.Items.Refresh();
                return;
            }

            var wordFilters = _textFilter.Text.Split(' ');
            //only fill items where all words exists
            var items = from item in EntityDB.EntityTypes where wordFilters.All(a => item.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0) select item;
            _entityTypes.ItemsSource = items;

            _entityTypes.Items.Refresh();
        }

        private string doGenerateFrostedLink()
        {
            var asset = _references.SelectedItem as DbxUtils.AssetInstance;

            //frostedLink.AppendFormat("frosted://{0};@{1}/{2}", AppSettings.DATABASE, getOwningGuid(allLines, dbx.FilePath), getInstanceGuid(allLines, dbx.LineNumber - 1));
            var frostedLink = new StringBuilder();
            frostedLink.AppendFormat("frosted://{0};@{1}/{2}", "", asset.PartitionGuid, asset.AssetGuid);
            var sb = new StringBuilder(_infoPanel.Text);
            sb.AppendLine();
            sb.AppendLine("----------------------");
            sb.AppendLine("FrostEd-Link");
            sb.AppendLine(frostedLink.ToString());
            _infoPanel.Text = sb.ToString();
            return frostedLink.ToString();
        }

        private void onOpenInFrosted(object sender, RoutedEventArgs e)
        {
            var s = doGenerateFrostedLink();
            Utils.AtomicWriteToLog("open frosted " + s);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Process process = Process.Start(@s);
            });
        }

        private void reset() {
            EntityDB = new EntityDatabase();
            dbxRoot.Text = EntityDatabase.DbxRootFolder;
            _time.Content = "Loading";

            _content.Visibility = System.Windows.Visibility.Collapsed;
            SetLoadingVisibility(false);
            _parseOptions.Visibility = System.Windows.Visibility.Visible;
            SetViewMode(ViewMode.Application);
        }

        private enum ViewMode
        {
            Application,
            Debug,
            About,
        }

        private void onViewApplication(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Application);
        }

        private void onViewDebug(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.Debug);
        }

        private void onViewAbout(object sender, RoutedEventArgs e)
        {
            SetViewMode(ViewMode.About);
        }

        private void SetViewMode(ViewMode viewMode)
        {
            if (_viewMode == viewMode)
                return;

            _viewMode = viewMode;
            _menuDebug.IsEnabled = viewMode != ViewMode.Debug;
            _menuAbout.IsEnabled = viewMode != ViewMode.About;
            _menuApplication.IsEnabled = viewMode != ViewMode.Application;

            _debugView.Visibility = viewMode == ViewMode.Debug ? System.Windows.Visibility.Visible : Visibility.Collapsed;
            _aboutView.Visibility = viewMode == ViewMode.About ? System.Windows.Visibility.Visible : Visibility.Collapsed;
            _applicationView.Visibility = viewMode == ViewMode.Application ? System.Windows.Visibility.Visible : Visibility.Collapsed;

            if (viewMode == ViewMode.Debug)
            {
                _debugText.Text = InstanceTrackerAPI.GetLog();
            }
            else if (viewMode == ViewMode.About)
            {
                _aboutText.Text = InstanceTrackerAPI.GetAboutText();
            }
        }
    }
}
