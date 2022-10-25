using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace Jmon_Switcher
{
    internal class PPT
    {
        PowerPoint.Application PPT_App;
        static PPT _instance = null;

        PowerPoint.Presentations ps = null;
        PowerPoint.Presentation p = null;

        MsoTriState ofalse = MsoTriState.msoFalse;
        MsoTriState otrue = MsoTriState.msoTrue;
        MsoTriState oCtrue = MsoTriState.msoCTrue;

        private int screen_index = 0;

        public static PPT Instance //singleton
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PPT();
                }
                return _instance;
            }
        }

        private PPT()
        {
            PPT_App = new PowerPoint.Application();
            PPT_App.Visible = Microsoft.Office.Core.MsoTriState.msoTrue;
        }

        public void Set_PPT_Screen_Index(int screen_index)
        {
            this.screen_index = screen_index;
        }
        public void Show_ppt_at_Screen_Index()
        {
            if(p!=null)
            {
                p.SlideShowWindow.Top = Pixel_To_Points(Screen.AllScreens[screen_index].WorkingArea.Top);
                p.SlideShowWindow.Left = Pixel_To_Points(Screen.AllScreens[screen_index].WorkingArea.Left);
            }
        }

        public bool Load_File()
        {
            string file_path = "";
            OpenFileDialog openFileDialog = new OpenFileDialog();
            DialogResult dialogResult = openFileDialog.ShowDialog();
            if(dialogResult == DialogResult.OK)
            {
                string file_name = openFileDialog.FileName;//폴더 선택기에서 선택한 파일의 이름을 받아옴.
                try
                {
                    file_path = System.IO.Path.GetFullPath(file_name);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                if (p == null && file_path != null)
                {
                    ps = PPT_App.Presentations;
                    p = ps.Open(file_path, oCtrue, otrue, ofalse);
                    return true;
                }
            }
            return false;
        }

        
        public void Show()
        {
            p.SlideShowSettings.ShowType = PowerPoint.PpSlideShowType.ppShowTypeKiosk;
            p.SlideShowSettings.ShowPresenterView = ofalse;
            p.SlideShowSettings.Run();

            Show_ppt_at_Screen_Index();

        }
        public void Next()
        {
            if(p.SlideShowWindow.View.CurrentShowPosition == p.Slides.Count)
            {
                MessageBox.Show("마지막 페이지입니다.");
            }
            else
            {
                p.SlideShowWindow.Activate();
                p.SlideShowWindow.View.Next();
            }
        }
        public void Prev()
        {
            p.SlideShowWindow.View.Previous();
        }
        
        public void Close()
        {
            if(p != null)
            {
                p.Close();
                p = null;
            }
        }
        public void Test_addNewPage()
        {

        }

        private float Pixel_To_Points(int point)
        {
            return point * 72 / 96;
        }

    }
}
