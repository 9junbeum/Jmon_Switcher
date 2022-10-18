using System;
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

namespace Jmon_Switcher
{
    /// <summary>
    /// _3state_toggle_button.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class _3state_toggle_button : UserControl
    {
        public int Tag;
        public _3state_toggle_button()
        {
            InitializeComponent();
        }
        public void SetTag(int t)
        {
            this.Tag = t;
        }

        private void Audio_On_Btn_Click(object sender, RoutedEventArgs e)
        {
            //on
            on_btn.Background = Brushes.LightGreen;
            off_btn.Background = Brushes.Gray;
            afv_btn.Background = Brushes.Gray;

            Dispatcher.Invoke(() =>
            {

            });
        }

        private void Audio_Off_Btn_Click(object sender, RoutedEventArgs e)
        {
            //off
            on_btn.Background = Brushes.Gray;
            off_btn.Background = Brushes.DarkGray;
            afv_btn.Background = Brushes.Gray;
        }

        private void Audio_AFV_Btn_Click(object sender, RoutedEventArgs e)
        {
            //afv
            on_btn.Background = Brushes.Gray;
            off_btn.Background = Brushes.Gray;
            afv_btn.Background = Brushes.LightCoral;
        }

        public void Set_Btn_enable()
        {
            on_btn.IsEnabled = true;
            off_btn.IsEnabled = true;
            afv_btn.IsEnabled = true;
        }
        public void Set_Btn_disable()
        {
            on_btn.IsEnabled = false;
            off_btn.IsEnabled = false;
            afv_btn.IsEnabled = false;
        }

    }
}
