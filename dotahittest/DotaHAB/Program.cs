using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.Collections;
using System.IO;

namespace DotaHIT
{
    static class Program
    {        
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);           
            //Application.Run(new DotaHIT.Extras.ReplayParserForm());
            //return;
            //args = new string[1];
            //args[0] = @"D:\Bob\Programming\WorkSpace\DotaHIT\DotaHAB\bin\Release\Anti-Mage test.dhb";            

            if (args.Length > 0)
            {
                string filename = args[0];

                if (!File.Exists(filename))
                    MessageBox.Show("File '" + filename + "' does not exists", "DotA H.I.T.");
                else
                {
                    switch (Path.GetExtension(filename).ToLower())
                    {
                        case ".w3x":
                            if (args.Length > 3 && args[1] == "RunPlugin1")
                            {
                                MainForm mf = new MainForm(filename);

                                object plugin = null;
                                foreach (DotaHIT.Core.HabProperties hpsPluginType in Current.plugins)
                                    if (hpsPluginType.TryGetValue(args[2], out plugin))
                                    {                                        
                                        (plugin as DotaHIT.Plugins.IDotaHITPlugin1).Click(args[3], EventArgs.Empty);
                                        break;
                                    }

                                if (plugin == null)
                                    MessageBox.Show("Failed to find plugin '" + args[2] + "'");
                            }
                            else
                                Application.Run(new MainForm(filename));
                            break;
                        case ".dhb":
                        case ".w3g":
                            Application.Run(new MainForm(filename));
                            break;                        
                        default:
                            MessageBox.Show("Unknown file extenstion", "DotA H.I.T.");
                            Application.Run(new MainForm(true));
                            break;
                    }
                    return;
                }
            }

            try
            {
                Application.Run(new MainForm(true));                
            }
            catch (Exception e)
            {                
                Application.Run(GetErrorReportForm("Error report", e));
            }
        }

        public static Form GetErrorReportForm(string caption, Exception e)
        {
            Form f = new Form();

            f.Text = caption;
            f.Width = 400;
            f.StartPosition = FormStartPosition.CenterScreen;
            TextBox tb = new TextBox();
            tb.Multiline = true;
            tb.ScrollBars = ScrollBars.Vertical;
            tb.Text = "Error message: " + e.Message + "\r\n\r\nStackTrace:\r\n" + e.StackTrace;
            f.Controls.Add(tb);
            tb.Dock = DockStyle.Fill;

            return f;
        }
    }    
}