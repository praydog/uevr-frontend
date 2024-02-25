using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace UEVR {
    public partial class YesNoDialog : Window {
        public bool DialogResultYes { get; private set; } = false;

        public YesNoDialog(string windowTitle, string txt) {
            InitializeComponent();
            m_dialogText.Text = txt;
            this.Title = windowTitle;
        }

        private void btnYes_Click(object sender, RoutedEventArgs e) {
            DialogResultYes = true;
            this.Close();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e) {
            DialogResultYes = false;
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                this.DragMove();
            }
        }
    }
}
