﻿using System;
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
using TooHuman1SE.SEStructure;
using TooHuman1SE.SEFunctions;
using TooHuman1SE.Windows;

namespace TooHuman1SE.Editor_Tabs
{
    /// <summary>
    /// Interaction logic for TabRunesUC.xaml
    /// </summary>
    public partial class TabRunesUC : UserControl
    {
        public TabRunesUC()
        {
            InitializeComponent();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditorWindow ewin = (EditorWindow)Window.GetWindow(this);
            RuneEditorWindow dlg = new RuneEditorWindow();
            dlg.runeIndex = gridRunes.SelectedIndex;
            dlg.thisRune = (TH1Rune)gridRunes.SelectedItem;
            dlg.Owner = Window.GetWindow(this);
            
            if (dlg.ShowDialog().Equals(true))
            {
                Functions.log("Saving Rune..");
                gridRunes.ItemsSource = null;
                ewin._save.runes[dlg.runeIndex] = dlg.thisRune;
                gridRunes.ItemsSource = ewin._save.runes;
                recountRunes();
            } else  Functions.log("User Cancelled");
        }

        public void recountRunes()
        {
            TH1Rune tmpRune = new TH1Rune();
            lblRuneCount.Content = String.Format("{0}/{1} Runes", gridRunes.Items.Count, tmpRune.LIMIT_MAX_RUNES);
        }

        private void flipFlop()
        {
            EditorWindow ewin = (EditorWindow)Window.GetWindow(this);
            ewin._save.runes = (List<TH1Rune>)gridRunes.ItemsSource;
            gridRunes.ItemsSource = null;
            gridRunes.ItemsSource = ewin._save.runes;
        }

        #region Actions Menu

        public void buildContextMenu() {
            buildColourContext();
        }

        private void buildColourContext()
        {
            string[] colours = new TH1Rune().getColourNames;
            MenuItem rootItem = findActionsMenu();
            MenuItem _group = new MenuItem();

            _group.Header = "Set Colour";
            for (int i = 0; i < colours.Length; i++)
            {
                MenuItem tmpi = new MenuItem();
                tmpi.Header = colours[i];
                tmpi.Tag = i;
                tmpi.Click += mnu_SetColourClick;
                _group.Items.Add(tmpi);
            }
            rootItem.Items.Add(_group);
        }

        private MenuItem findActionsMenu()
        {
            MenuItem rootItem = new MenuItem();
            foreach (object _obj in gridRunes.ContextMenu.Items)
            {
                if (_obj is MenuItem)
                {
                    MenuItem mnu = _obj as MenuItem;
                    if (mnu.Header.ToString() == "Actions")
                    {
                        rootItem = mnu;
                    }
                }
            }
            return rootItem;
        }

        #endregion Actions Menu

        #region Event Handlers

        private void btnAddRune_Click(object sender, RoutedEventArgs e)
        {
            RuneEditorWindow dlg = new RuneEditorWindow();
            dlg.Owner = Window.GetWindow(this);
            EditorWindow ewin = (EditorWindow)Window.GetWindow(this);

            if (gridRunes.Items.Count < dlg.thisRune.LIMIT_MAX_RUNES)
            {
                if (dlg.ShowDialog().Equals(true))
                {
                    Functions.log("Creating New Rune..");
                    gridRunes.ItemsSource = null;
                    ewin._save.runes.Add(dlg.thisRune);
                    gridRunes.ItemsSource = ewin._save.runes;
                    recountRunes();
                } else Functions.log("User Cancelled");
            }
            else
            {
                Functions.log("Max Rune Limit Reached, Cannot Create New Rune", Functions.LC_WARNING);
                MessageBox.Show(String.Format("You Have Reached The Maximum Number Of Runes ({0})", dlg.thisRune.LIMIT_MAX_RUNES), "Rune Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void mnu_DeleteRunes(object sender, RoutedEventArgs e)
        {
            EditorWindow ewin = (EditorWindow)Window.GetWindow(this);
            if (gridRunes.SelectedItems.Count < 1) return;

            if( MessageBox.Show(String.Format("Are You Sure You Want To Delete {0} Rune(s)?",gridRunes.SelectedItems.Count),"Confirm Delete",MessageBoxButton.YesNo,MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach( TH1Rune rune in gridRunes.SelectedItems)
                {
                    ewin._save.runes.Remove(rune);
                }
                gridRunes.ItemsSource = null;
                gridRunes.ItemsSource = ewin._save.runes;
                recountRunes();
            }
        }

        private void mnu_SetColourClick(object sender, RoutedEventArgs e)
        {
            if ( sender is MenuItem)
            {
                MenuItem mnu = sender as MenuItem;
                foreach (TH1Rune _rune in gridRunes.SelectedItems)
                {
                    _rune.setColourByName((string)mnu.Header);
                }
                flipFlop();
            }
        }

        private void mnu_PurchasedClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                bool _toggle = (string)((MenuItem)sender).Header == "Yes";
                foreach (TH1Rune _rune in gridRunes.SelectedItems)
                {
                    _rune.purchased = (uint)(_toggle ? 1 : 0);
                }
                flipFlop();
            }
        }

        #endregion Event Handlers
    }
}