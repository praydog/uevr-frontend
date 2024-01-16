using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace UEVR {
    public partial class GameAutoStartExplanationDialog : Window {
        public bool HideAutostartWarning { get; private set; }
        public bool DialogResultStartGame { get; private set; }

        public GameAutoStartExplanationDialog() {
            InitializeComponent();
        }

        private void btnStartGame_Click(object sender, RoutedEventArgs e) {
            HideAutostartWarning = chkRememberChoice.IsChecked == true;
            DialogResultStartGame = true;
            this.Close();
        }

        private void btnWaitForGameStart_Click(object sender, RoutedEventArgs e) {
            HideAutostartWarning = chkRememberChoice.IsChecked == true;
            DialogResultStartGame = false;
            this.Close();
        }
    }
}
