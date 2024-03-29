﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace UEVR {
    public partial class VDWarnDialog : Window {
        public bool HideFutureWarnings { get; private set; }
        public bool DialogResultOK { get; private set; }

        public VDWarnDialog() {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e) {
            HideFutureWarnings = chkHideWarning.IsChecked ?? false;
            DialogResultOK = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) {
            DialogResultOK = false;
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                this.DragMove();
            }
        }
    }
}
