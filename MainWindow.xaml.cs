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
using System.Diagnostics;

namespace DbxEntityTracker
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EntityDbxLib _lib;
        private string _frostedLink;
        public MainWindow()
        {
            InitializeComponent();

            _lib = new EntityDbxLib();
            AppSettings.load();
            readFromAppSettings();
            _content.Visibility = System.Windows.Visibility.Collapsed;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            applicationCrash(e.Exception);
            Application.Current.Shutdown();
        }

        private void readFromAppSettings()
        {
            this.dbxRoot.Text = AppSettings.DBX_ROOT;
            this.ddfRoot.Text = AppSettings.DDF_WSROOT;
            this.suffix.Text = AppSettings.ENTITY_SUFFIX;
            this._database.Text = AppSettings.DATABASE;
        }

        private void updateAppSettings()
        {
            AppSettings.DBX_ROOT = this.dbxRoot.Text;
            AppSettings.DDF_WSROOT = this.ddfRoot.Text;
            AppSettings.ENTITY_SUFFIX = this.suffix.Text;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            updateAppSettings();
            bool success = false;
            try
            {
                _lib.init();
                success = true;
            }
            catch (Exception ex)
            {
                applicationCrash(ex);
            }

            if (success)
            {
                AppSettings.save();
                _content.Visibility = System.Windows.Visibility.Visible;
                _entities.ItemsSource = _lib.AllEntities.Keys;
                _parseOptions.Visibility = System.Windows.Visibility.Collapsed;
                _lib.save();
                showEntities();
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
            if (all == null || all.Count == 0)
            {
                return;
            }
            var filter = _textFilter.Text;
            if (filter != "")
            {   
                _entities.ItemsSource = all.FindAll(a => a.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            else
            {
                _entities.ItemsSource = all;
            }
            _entities.Items.Refresh();
        }

        private void _database_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            AppSettings.DATABASE = _database.Text;
        }

        private void onLoad(object sender, RoutedEventArgs e)
        {
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

        private void onGenerateFrostEdLink(object sender, RoutedEventArgs e)
        {
            var key = _entities.SelectedItem.ToString();
            var idx = _references.SelectedIndex;
            var dbx = _lib.GetDbxInfo(key, idx);

            var frostedLink = new StringBuilder();
            var allLines = File.ReadAllLines(dbx.FilePath);
            frostedLink.AppendFormat("frosted://{0};@{1}/{2}", AppSettings.DATABASE, getOwningGuid(allLines, dbx.FilePath), getInstanceGuid(allLines, dbx.LineNumber - 1));
            _frostedLink = frostedLink.ToString();
            var sb = new StringBuilder(_infoPanel.Text);
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
            ThreadPool.QueueUserWorkItem(delegate
            {
                Process process = Process.Start(@_frostedLink);
            });
        }
    }
}
