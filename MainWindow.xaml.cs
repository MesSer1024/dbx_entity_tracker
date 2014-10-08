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
using dbx_lib;
using System.Collections.Concurrent;

namespace DbxEntityTracker
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EntityDbxLib _lib;
        public MainWindow()
        {
            InitializeComponent();

            _lib = new EntityDbxLib();
            readFromAppSettings();
            _content.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void readFromAppSettings()
        {
            this.dbxRoot.Text = AppSettings.DBX_ROOT;
            this.ddfRoot.Text = AppSettings.DDF_WSROOT;
            this.prefix.Text = AppSettings.ENTITY_PREFIX;
            this.suffix.Text = AppSettings.ENTITY_SUFFIX;
        }

        private void updateAppSettings()
        {
            AppSettings.DBX_ROOT = this.dbxRoot.Text;
            AppSettings.DDF_WSROOT = this.ddfRoot.Text;
            AppSettings.ENTITY_PREFIX = this.prefix.Text;
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
                MessageBox.Show(ex.Message);
            }

            if (success)
            {
                AppSettings.save();
                _content.Visibility = System.Windows.Visibility.Visible;
                _entities.ItemsSource = _lib.AllEntities.Keys;
            }
        }

        private void _entities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var key = _entities.SelectedItem.ToString();
            if (_lib.EntityUsage.ContainsKey(key))
                _references.ItemsSource = _lib.EntityUsage[key];
            else
                _references.ItemsSource = null;
        }

    }
}
