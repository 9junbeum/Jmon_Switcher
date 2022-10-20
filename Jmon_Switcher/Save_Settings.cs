using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace Jmon_Switcher
{
    internal class Save_Settings
    {
        JObject jobj = new JObject();
        string save_path;

        string IP_Address;
        int Screen_Index;


        public bool is_SaveFile_Exist()
        {
            if(File.Exists(save_path))
            {
                //파일이 있으면
                return true;
            }
            else
            {
                return false;
            }
        }

        public void set_Path(string path)
        {
            save_path = path;
        }

        public void SAVE(string ipAddress, int screenIndex)
        {
            try
            {
                jobj = new JObject(new JProperty("IP_Address", ipAddress), new JProperty("Screen_Index", screenIndex));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public dynamic LOAD(int load_index)
        {
            try
            {
                string json = File.ReadAllText(save_path);
                JObject jobj = JObject.Parse(json);
                this.IP_Address = jobj["IP_Address"].ToString();
                this.Screen_Index = int.Parse(jobj["Screen_Index"].ToString());

                switch (load_index)
                {
                    case 0: return this.IP_Address;
                    case 1: return this.Screen_Index;

                    default: return null;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
