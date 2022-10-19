using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Jmon_Switcher
{
    /// <summary>
    /// Chroma_Window.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Chroma_Window : Window
    {
        private int screen_index = 0;
        public Chroma_Window()
        {
            InitializeComponent();
        }

        public void Set_Screen_Index(int screen_index)
        {
            this.screen_index = screen_index;
        }
        public void Show_window_at_Screen_Index()
        {
            this.WindowState = WindowState.Minimized;
            this.Top = Screen.AllScreens[screen_index].WorkingArea.Top;
            this.Left = Screen.AllScreens[screen_index].WorkingArea.Left;
            this.WindowState = WindowState.Maximized;
        }

        public void Set_Chroma_Canvas(Canvas canvas)
        {
            chroma_fullscreen = canvas;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //var scaleRatio = Math.Max(Screen.PrimaryScreen.WorkingArea.Width / SystemParameters.PrimaryScreenWidth, Screen.PrimaryScreen.WorkingArea.Height / SystemParameters.PrimaryScreenHeight);

            Show_window_at_Screen_Index();// 번호에 해당하는 스크린에 표시

        }

        public void Set_Text(string text)
        {
            chroma_Caption.Text = text;
        }
        public void Set_VerticalAlignment(VerticalAlignment va)
        {
            chroma_Caption.VerticalContentAlignment = va;

        }
        public void Set_Font_Size(double f_size)
        {
            chroma_Caption.FontSize = f_size * 3; //비율 감안하여

        }
        public void Set_Font_Color(Brush f_color)
        {
            chroma_Caption.Foreground = f_color;

        }
        public void Set_Font_Family(FontFamily ff)
        {
            chroma_Caption.FontFamily = ff;

        }

        public void Set_Background(Brush background)
        {
            chroma_fullscreen.Background = background;

        }

    }
}
