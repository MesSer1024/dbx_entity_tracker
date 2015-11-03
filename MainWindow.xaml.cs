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
using Dice.Frostbite.Framework;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;

namespace DbxEntityTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private class EntityLib
        {
            public List<DbxUtils.AssetInstance> Entities { get; set; }
            public List<string> EntityTypes { get; set; }
        }

        private class DbxEntityTracker_data
        {
            public List<DbxUtils.AssetInstance> Entities { get; set; }
        }

        private EntityLib EntityDB;

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
            Utils.AtomicWriteToLog("load");
            var dlg = new OpenFileDialog();
            dlg.Filter = "DbxEntityTracker Save (*.det)|*.det";
            dlg.Multiselect = false;
            dlg.FileOk += (dlgSender, args) => {
                LoadDatabase((dlgSender as OpenFileDialog).FileName);
                showEntities();
            };
            dlg.InitialDirectory = System.IO.Path.GetFullPath(AppSettings.APP_SAVE_FOLDER);
            var dir = new DirectoryInfo(dlg.InitialDirectory);
            if (!dir.Exists)
                dir.Create();
            dlg.FileName = "_lastSave.det";
            dlg.Title = "Open a previous search";
            dlg.ShowDialog();
        }

        private void onPopulateClick(object sender, RoutedEventArgs e)
        {
            Utils.AtomicWriteToLog("Populating internal database based on your settings");
            _loadingText.Content = "Populating internal database based on your settings";
            SetLoadingVisibility(true);
            PopulateFiles();
        }

        private void PopulateFiles() {
            var uithread = Application.Current.Dispatcher;

            var start = DateTime.Now;
            var root = dbxRoot.Text;
            var files = DbxUtils.GetFiles(dbxRoot.Text);
            Console.WriteLine("---Found {0} dbx-Files from \"{2}\" --- time: {1}ms", files.Length, GetMillisecondsSinceStart(start), DbxUtils.GetRootPath());

            var sorted = files.OrderByDescending(a => a.Length).ToList();
            Console.WriteLine("---Sorted {0} Files by size --- time: {1}ms", files.Length, GetMillisecondsSinceStart(start));

            var partitions = DbxUtils.ParsePartitions(sorted, files.Length);
            Console.WriteLine("---Parsed {0} partitions from dbx-files --- time: {1}ms", partitions.Count, GetMillisecondsSinceStart(start));

            var instances = DbxUtils.CreateInstances(partitions);
            Console.WriteLine("---Found {0} instances given all partitions --- time: {1}ms", instances.Count, GetMillisecondsSinceStart(start));

            //Only save "entities" [due to OutOfMemoryException when dumping through JsonConvert...]
            EntityDB.Entities = instances.FindAll(a => a.AssetType.Contains("EntityData"));
            Console.WriteLine("---Found {0} entities --- time: {1}ms", EntityDB.Entities.Count, GetMillisecondsSinceStart(start));

            EntityDB.EntityTypes = DbxUtils.GetUniqueEntityTypes(EntityDB.Entities);
            Console.WriteLine("---Found {0} unique entity types --- time: {1}ms", EntityDB.EntityTypes.Count, GetMillisecondsSinceStart(start));


            //on complete
            uithread.Invoke(() =>
            {
                SetLoadingVisibility(false);
                _content.Visibility = System.Windows.Visibility.Visible;
                _entityTypes.ItemsSource = EntityDB.EntityTypes;
                _references.ItemsSource = EntityDB.Entities;
                _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
                showEntities();
                Utils.AtomicWriteToLog(String.Format("Populate done, time: {0}ms", GetMillisecondsSinceStart(start)));
            });
        }

        private double GetMillisecondsSinceStart(DateTime start)
        {
            return (DateTime.Now - start).TotalMilliseconds;
        }

        void SaveDatabase(string path)
        {
            var save = new DbxEntityTracker_data()
            {
                Entities = EntityDB.Entities,
            };

            var output = JsonConvert.SerializeObject(save);

            var file = new FileInfo(path);
            if (!file.Directory.Exists)
                file.Directory.Create();
            File.WriteAllText(path, output);
        }

        void LoadDatabase(string path)
        {
            EntityDB = new EntityLib();
            var file = new FileInfo(path);
            if (file.Exists)
            {
                using (var sr = new StreamReader(file.FullName))
                {
                    string s = sr.ReadToEnd();
                    var load = JsonConvert.DeserializeObject<DbxEntityTracker_data>(s);
                    EntityDB.Entities = load.Entities;
                }

                EntityDB.EntityTypes = DbxUtils.GetUniqueEntityTypes(EntityDB.Entities);
            }
            else
            {
                throw new Exception(String.Format("File does not exist \"{0}\"", path));
            }
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
            _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void _entities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_entityTypes.SelectedItem == null)
                return;
            var key = _entityTypes.SelectedItem.ToString();
            _references.ItemsSource = DbxUtils.FindEntities(key, EntityDB.Entities);
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
            _infoPanel.Text = DbxUtils.GetInfoTextForAsset(item);
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

        private void onNewSearchClick(object sender, RoutedEventArgs e)
        {
            reset();
        }

        private string doGenerateFrostedLink()
        {
            var asset = _references.SelectedItem as DbxUtils.AssetInstance;

            //frostedLink.AppendFormat("frosted://{0};@{1}/{2}", AppSettings.DATABASE, getOwningGuid(allLines, dbx.FilePath), getInstanceGuid(allLines, dbx.LineNumber - 1));
            var frostedLink = new StringBuilder();
            frostedLink.AppendFormat("frosted://{0};@{1}/{2}", "", asset.PartitionGuid, asset.Guid);
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
            EntityDB = new EntityLib();
            dbxRoot.Text = DbxUtils.GetRootPath();
            _time.Content = "Loading";

            _content.Visibility = System.Windows.Visibility.Collapsed;
            SetLoadingVisibility(false);
            _parseOptions.Visibility = System.Windows.Visibility.Visible;
        }
    }
}
