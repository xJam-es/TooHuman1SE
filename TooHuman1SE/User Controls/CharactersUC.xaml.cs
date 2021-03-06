﻿using System;
using System.IO;
using System.Collections.Generic;
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
using TooHuman1SE.SEFunctions;
using TooHuman1SE.SEStructure;
using System.Collections.ObjectModel;

namespace TooHuman1SE.User_Controls
{
    /// <summary>
    /// Interaction logic for CharactersUC.xaml
    /// </summary>
    /// 

    public partial class CharactersUC : UserControl
    {

        public List<CharList> _charList { get; set; }

        public CharactersUC()
        {
            InitializeComponent();
        }

        #region Threading

        void SetStatus(string text)
        {
            Dispatcher.Invoke(() =>
            {
                MainWindow mwin = (MainWindow)Window.GetWindow(this);
                mwin.lblLoading.Text = text;
                mwin.pbarLoading.Value += 1;
                mwin.pbarLoading.Visibility = Visibility.Visible;
            });
        }

        void ResetStatus()
        {
            Dispatcher.Invoke(() =>
            {
                MainWindow mwin = (MainWindow)Window.GetWindow(this);
                mwin.lblLoading.Text = "Ready";
                mwin.pbarLoading.Value = 0;
                mwin.pbarLoading.Maximum = 0;
                mwin.pbarLoading.Visibility = Visibility.Hidden;
            });
        }

        void setPbarMax( int _max)
        {
            Dispatcher.Invoke(() =>
            {
                MainWindow mwin = (MainWindow)Window.GetWindow(this);
                mwin.pbarLoading.Maximum = _max;
            });
        }

        internal void ExecutePreloading( MainWindow mwin )
        {
            ResetStatus();
            setPbarMax(3);

            // Pre-Load Databases
            SetStatus("Loading Databases");
            mwin.db = new TH1Collections();

            // Load Character List
            SetStatus("Loading Character List");
            Dispatcher.Invoke(() =>
            {
                Functions.initLoadCharList();
            });
        }

        #endregion Threading

        #region Event Handlers

        private async void lstCharacters_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow mwin = (MainWindow)Window.GetWindow(this);
            await Task.Run(() => ExecutePreloading(mwin));
            mwin.enableInteractions();
            ResetStatus();
        }

        private void lstCharacters_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            DependencyObject obj = (DependencyObject)e.OriginalSource;
            ListView lv = (ListView)sender;

            while (obj != null && obj != lv)
            {
                if (obj.GetType() == typeof(ListViewItem))
                {
                    mnu_OpenEvent(sender, e);
                }
                obj = VisualTreeHelper.GetParent(obj);
            }

        }

        private void mnu_OpenContext(object sender, RoutedEventArgs e)
        {
            mnu_OpenEvent(sender, e);
        }

        private void mnu_DeleteContext(object sender, RoutedEventArgs e)
        {
            int itemcount = lstCharacters.SelectedItems.Count;
            ObservableCollection<CharListImages> tmplist = (ObservableCollection<CharListImages>)lstCharacters.ItemsSource;
            List<object> forRemoval = new List<object>();

            if (itemcount > 0)
            {
                if( MessageBox.Show(String.Format("Are You Sure You Want To Delete {0} Files?", itemcount), "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    foreach (CharListImages item in lstCharacters.SelectedItems)
                    {
                        string savepath = item.path;
                        File.Delete(savepath);
                        forRemoval.Add(item);
                    }

                    foreach( CharListImages rem in forRemoval)
                    {
                        tmplist.Remove(rem);
                    }
                }
            }

        }

        // Generic Open Event
        private void mnu_OpenEvent(object sender, RoutedEventArgs e)
        {
            foreach(CharListImages item in lstCharacters.SelectedItems)
            {
                string savepath = item.path;
                Functions.loadIntoEditorWindow(savepath, (MainWindow)Window.GetWindow(this) );
            }
            // Functions.saveCharList((List<CharList>)lstCharacters.ItemsSource);

        }

        #endregion Event Handlers
    }
}
