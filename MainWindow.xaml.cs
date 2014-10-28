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
using System.Threading;
using System.ComponentModel;
using DbxEntityTracker.commands;

namespace DbxEntityTracker
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EntityDbxLib _lib;
        private System.Timers.Timer _timer;
        public MainWindow()
        {
            InitializeComponent();
            AppSettings.load();
            Application.Current.DispatcherUnhandledException += onUnhandledException;
            reset();
        }

        void onUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            applicationCrash(e.Exception);
            Application.Current.Shutdown();
        }

        private void readFromAppSettings()
        {
            this.dbxRoot.Text = AppSettings.DBX_ROOT;
            this.ddfRoot.Text = AppSettings.DDF_WSROOT;
            this.suffix.Text = AppSettings.ENTITY_SUFFIX;
        }

        private void updateAppSettings()
        {
            AppSettings.DBX_ROOT = this.dbxRoot.Text;
            AppSettings.DDF_WSROOT = this.ddfRoot.Text;
            AppSettings.ENTITY_SUFFIX = this.suffix.Text;
        }

        private void onLoad(object sender, RoutedEventArgs e) {
            var dlg = new OpenFileDialog();
            dlg.Filter = "DbxEntityTracker Save (*.det)|*.det";
            dlg.Multiselect = false;
            dlg.FileOk += (dlgSender, args) => {
                _lib.load((dlgSender as OpenFileDialog).FileName);
                showEntities();
            };
            dlg.InitialDirectory = Environment.CurrentDirectory + "\\output\\";
            dlg.FileName = "_lastSave.det";
            dlg.Title = "Selected a previous search";
            dlg.ShowDialog();
        }

        private void onPopulateClick(object sender, RoutedEventArgs e)
        {
            updateAppSettings();
            _loadingText.Content = "Populating internal database based on your settings";
            loadingVisible(true);
            doPopulate();
        }

        private void doPopulate() {
            var uithread = Application.Current.Dispatcher;
            var cmd = new InitializeDatabaseCommand(_lib);
            cmd.AllFilesFound = () => {
                uithread.Invoke(() => {
                    _loadingText.Content = String.Format("{0}\n\nTotal DDF-files: {1}\nTotal DBX-files: {2}", _loadingText.Content, _lib.ddfFiles.Length, _lib.dbxFiles.Length);
                });
            };
            cmd.AllFilesPopulated = () => {
                uithread.Invoke(() => {
                    loadingVisible(false);
                    AppSettings.save();
                    _content.Visibility = System.Windows.Visibility.Visible;
                    _entities.ItemsSource = _lib.AllEntities.Keys;
                    _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
                    _lib.save();
                    showEntities();
                });
            };
            cmd.Error = (string error) => {
                uithread.Invoke(() => {
                    MessageBox.Show(error);
                    loadingVisible(false);
                });
            };

            cmd.execute();
        }

        private void loadingVisible(bool flag) {
            if (flag) {
                var startTime = DateTime.Now;
                var s = _loadingText.Content.ToString();
                _loading.Visibility = System.Windows.Visibility.Visible;
                var uithread = Application.Current.Dispatcher;
                if (_timer == null) {
                    _timer = new System.Timers.Timer(1000);
                    _timer.Elapsed += (Object source, System.Timers.ElapsedEventArgs e) => {
                        uithread.Invoke(() => {
                            _time.Content = string.Format("Loading: {0}s", (DateTime.Now - startTime).TotalSeconds.ToString("0"));
                        });
                    };
                }
                _timer.Start();
            } else {
                if (_timer != null) {
                    _timer.Stop();
                    _timer = null; //need to create a new timer since anonymous function has another variable of "start time" compared to what we want
                }
                _loading.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void applicationCrash(Exception ex)
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
            _entities.ItemsSource = _lib.AllEntities.Keys;
            _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void _entities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_entities.SelectedItem == null)
                return;
            var key = _entities.SelectedItem.ToString();
            _infoPanel.Text = _lib.FindDdfSource(key) ?? "";
            _references.ItemsSource = _lib.FindDbxReferences(key);
        }

        private void _references_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var key = _entities.SelectedItem.ToString();
            var idx = _references.SelectedIndex;
            if (idx < 0 || key == "")
            {
                _infoPanel.Text = "";
                return;
            }
            var sb = new StringBuilder(_lib.FindDdfSource(key));
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("DbxInfo:");
            var dbx = _lib.GetDbxInfo(key, idx);
            if (dbx != null)
            {
                sb.AppendLine("FilePath: " + dbx.FilePath);
                sb.AppendLine("LineNumber: " + dbx.LineNumber);
                sb.AppendLine("EntityType: " + dbx.EntityType);
            }
            else
            {
                sb.AppendLine(String.Format("key: {0}, index: {1} is null", key, idx));
            }
            _infoPanel.Text = sb.ToString();
        }

        private void _textFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var all = _lib.AllEntities.Keys.ToList();
            if (all == null || all.Count == 0) {
                return;
            }
            var filter = _textFilter.Text;
            if (filter != "") {   
                _entities.ItemsSource = all.FindAll(a => a.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            } else {
                _entities.ItemsSource = all;
            }
            _entities.Items.Refresh();
        }

        private void onNewSearchClick(object sender, RoutedEventArgs e) {
            _lib.CancleTasks();
            reset();
        }

        private void reset() {
            _lib = new EntityDbxLib();
            _time.Content = "Loading";

            readFromAppSettings();
            _content.Visibility = System.Windows.Visibility.Collapsed;
            loadingVisible(false);
            _parseOptions.Visibility = System.Windows.Visibility.Visible;
        }
    }
}
