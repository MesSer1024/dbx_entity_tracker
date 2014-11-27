﻿using System;
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
using System.Threading;using System.Diagnostics;
using System.ComponentModel;
using DbxEntityTracker.commands;
using System.Windows.Threading;

namespace DbxEntityTracker
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EntityDbxLib _lib;
        private string _frostedLink;
        private System.Timers.Timer _timer;
        private bool _searchDdfFiles;
        private string _lastItem;
        private DateTime _timestamp;

        public MainWindow()
        {
            InitializeComponent();
            AppSettings.loadSettings();
            Application.Current.DispatcherUnhandledException += onUnhandledException;
            readFromAppSettings();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { 
                reset(); 
            }));

        }


        void onUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            applicationCrash(e.Exception);
            Utils.AtomicWriteToLog("crashed");
            Application.Current.Shutdown();
        }

        private void readFromAppSettings()
        {
            this.dbxRoot.Text = AppSettings.DBX_ROOT;
            this.ddfRoot.Text = AppSettings.DDF_WSROOT;
            this.suffix.Text = AppSettings.ENTITY_SUFFIX;
            this._ddfCheckbox.IsChecked = AppSettings.DDF_SEARCH_ENABLED;
            //this._database.Text = AppSettings.DATABASE;
        }

        private void updateAppSettings()
        {
            AppSettings.DBX_ROOT = this.dbxRoot.Text;
            AppSettings.DDF_WSROOT = this.ddfRoot.Text;
            AppSettings.ENTITY_SUFFIX = this.suffix.Text;
            AppSettings.DDF_SEARCH_ENABLED = (bool)this._ddfCheckbox.IsChecked;
            //AppSettings.DATABASE = this._database.Text;
        }

        private void onLoad(object sender, RoutedEventArgs e) {
            Utils.AtomicWriteToLog("load");
            var dlg = new OpenFileDialog();
            dlg.Filter = "DbxEntityTracker Save (*.det)|*.det";
            dlg.Multiselect = false;
            dlg.FileOk += (dlgSender, args) => {
                _lib.load((dlgSender as OpenFileDialog).FileName);
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
            _timestamp = DateTime.Now;
            updateAppSettings();
            Utils.AtomicWriteToLog("populate useddf?" + AppSettings.DDF_SEARCH_ENABLED.ToString());

            _loadingText.Content = "Populating internal database based on your settings";
            loadingVisible(true);
            doPopulate();
        }

        private void doPopulate() {
            var uithread = Application.Current.Dispatcher;
            var cmd = new InitializeDatabaseCommand(_lib, _searchDdfFiles);
            AppSettings.saveSettings();

            cmd.AllFilesFound = () => {
                uithread.Invoke(() => {
                    _loadingText.Content = String.Format("{0}\n\nTotal DDF-files: {1}\nTotal DBX-files: {2}", _loadingText.Content, _lib.ddfFiles.Length, _lib.dbxFiles.Length);
                });
            };
            cmd.AllFilesPopulated = () => {
                uithread.Invoke(() => {
                    loadingVisible(false);
                    _content.Visibility = System.Windows.Visibility.Visible;
                    _entities.ItemsSource = _lib.AllEntities.Keys;
                    _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
                    _lib.save();
                    showEntities();
                    Utils.AtomicWriteToLog(String.Format("Populate done, time:{0}ms", (DateTime.Now - _timestamp).TotalMilliseconds));
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
            var uiElementsEnabled = !flag;
            ddfRoot.IsEnabled = uiElementsEnabled;
            dbxRoot.IsEnabled = uiElementsEnabled;
            suffix.IsEnabled = uiElementsEnabled;
            parseButton.IsEnabled = uiElementsEnabled;
            loadButton.IsEnabled = uiElementsEnabled;

            if (flag)
            {

                var startTime = DateTime.Now;
                var s = _loadingText.Content.ToString();
                _loading.Visibility = System.Windows.Visibility.Visible;
                var uithread = Application.Current.Dispatcher;
                if (_timer == null) {
                    _timer = new System.Timers.Timer(495);
                    _timer.Elapsed += (Object source, System.Timers.ElapsedEventArgs e) => {
                        uithread.Invoke(() => {
                            var stringPrefix = _lib.IsCancelled ? "Canceling:" : "Loading:";
                            _time.Content = string.Format("{0} {1}s", stringPrefix, (DateTime.Now - startTime).TotalSeconds.ToString("0"));
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
            _lastItem = _entities.SelectedItem.ToString();
        }

        private void _references_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var key = _entities.SelectedItem == null ? _lastItem : _entities.SelectedItem.ToString();
            var idx = _references.SelectedIndex;
            if (idx < 0 || String.IsNullOrEmpty(key))
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

            if (String.IsNullOrEmpty(_textFilter.Text))
            {
                _entities.ItemsSource = all;
            }
            else
            {
                //generate filters
                var filters = new List<string>();
                filters.AddRange(_textFilter.Text.Split(' '));

                //if we have a currently selected item in reference view, add it to list
                var items = new List<string>();
                if (_references.SelectedItem != null)
                    items.Add(_lastItem);

                //check if all words exists in file name
                items.AddRange(from item in all where filters.All(a => item.IndexOf(a,StringComparison.OrdinalIgnoreCase) >= 0) select item);
                _entities.ItemsSource = items.ToList();
            }

            _entities.Items.Refresh();
        }

        //private void _database_TextChanged_1(object sender, TextChangedEventArgs e)
        //{
        //    AppSettings.DATABASE = _database.Text;
        //}

        private void onNewSearchClick(object sender, RoutedEventArgs e)
        {
            if (_lib.IsRunning)
            {
                _lib.CancleTasks();
            }
            else
            {
                reset();
            }
        }

        private void doGenerateFrostedLink()
        {
            var key = _entities.SelectedItem == null ? _lastItem : _entities.SelectedItem.ToString();
            var idx = _references.SelectedIndex;
            var sb = new StringBuilder();
            if (String.IsNullOrEmpty(key) || idx < 0)
            {
                sb = new StringBuilder(_infoPanel.Text);
                sb.AppendLine("----------------------");
                sb.AppendLine("FrostEd-Link");
                sb.AppendLine("-----------------Unable to generate frosted link, invalid selection in UI?");
                _infoPanel.Text = sb.ToString();
                return;
            }
            var dbx = _lib.GetDbxInfo(key, idx);

            var frostedLink = new StringBuilder();
            var allLines = File.ReadAllLines(dbx.FilePath);
            //frostedLink.AppendFormat("frosted://{0};@{1}/{2}", AppSettings.DATABASE, getOwningGuid(allLines, dbx.FilePath), getInstanceGuid(allLines, dbx.LineNumber - 1));
            frostedLink.AppendFormat("frosted://{0};@{1}/{2}", "", getOwningGuid(allLines, dbx.FilePath), getInstanceGuid(allLines, dbx.LineNumber - 1));
            _frostedLink = frostedLink.ToString();
            sb = new StringBuilder(_infoPanel.Text);
            sb.AppendLine();
            sb.AppendLine("----------------------");
            sb.AppendLine("FrostEd-Link");
            sb.AppendLine(frostedLink.ToString());
            _infoPanel.Text = sb.ToString();
        }

        private string getOwningGuid(string[] lines, string file)
        {
            int counter = 0;
            foreach (var line in lines)
            {
                counter++;
                if (line.Contains("primaryInstance"))
                {
                    var guid = _lib.findSubstring(line, "guid=\"", 36);
                    return guid;
                }
                if (counter > 20)
                    throw new Exception("Unable to find primaryInstance within 20 lines in file: " + file);
            }
            return "";
        }

        private string getInstanceGuid(string[] lines, int lineNumber)
        {
            var line = lines[lineNumber];

            var guid = _lib.findSubstring(line, "guid=\"", 36);
            return guid;
        }

        private void onOpenInFrosted(object sender, RoutedEventArgs e)
        {
            doGenerateFrostedLink();
            Utils.AtomicWriteToLog("open frosted " + _frostedLink);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Process process = Process.Start(@_frostedLink);
            });
        }

        private void reset() {
            _lib = new EntityDbxLib();
            _time.Content = "Loading";

            _lastItem = null;
            _content.Visibility = System.Windows.Visibility.Collapsed;
            loadingVisible(false);
            _parseOptions.Visibility = System.Windows.Visibility.Visible;
            updateDdfStatus();
        }

        private void CheckBox_Click_1(object sender, RoutedEventArgs e)
        {
            updateDdfStatus();
        }

        private void updateDdfStatus()
        {
            bool status = (bool)_ddfCheckbox.IsChecked;
            ddfRoot.IsEnabled = status;
            ddfRoot.IsReadOnly = !status;
            _searchDdfFiles = status;
            if (suffix != null)
            {
                suffix.IsEnabled = status;
                suffix.IsReadOnly = !status;
            }
        }
    }
}
