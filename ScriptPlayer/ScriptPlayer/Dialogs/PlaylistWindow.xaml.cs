﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer.Dialogs
{
    /// <summary>
    /// Interaction logic for PlaylistWindow.xaml
    /// </summary>
    public partial class PlaylistWindow : Window
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(PlaylistViewModel), typeof(PlaylistWindow), new PropertyMetadata(default(PlaylistViewModel)));

        public PlaylistViewModel ViewModel
        {
            get { return (PlaylistViewModel) GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        public PlaylistWindow(PlaylistViewModel viewmodel)
        {
            ViewModel = viewmodel;
            InitializeComponent();
        }

        private void PlaylistEntry_DoubleClicked(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = sender as ListBoxItem;
            PlaylistEntry entry = item?.DataContext as PlaylistEntry;
            if (entry == null)
                return;

            ViewModel.RequestPlayEntry(entry);
        }
    }
}
