using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Windows.Forms;
using DotaHIT.Core.Resources;
using System.Threading;

namespace DotaHIT
{
    public class DHRELEASE
    {
        public static string CurrentVersion { get { return "0.999t1"; } }

        static readonly string UrlToChangeLog = @"http://dotahit.svn.sourceforge.net/viewvc/dotahit/trunk/DotaHAB/bin/Release/changelog.txt";
        static readonly string DefaultUrlToSfxPackage = @"http://downloads.sourceforge.net/project/dotahit/packageName?use_mirror=dfn";        
        
        public static bool TryGetUpdate(bool showSplash, bool notifyOnLatest, out string sfxPackageName)
        {
            Thread thread = new Thread(UpdaterProcedure);

            SplashScreen splash = null;
            if (showSplash)
            {
                splash = new SplashScreen(Current.mainForm);
                splash.StopButtonVisible = true;
                splash.StopButtonClick += (o, e) => { if (thread != null) thread.Abort(); };//throw new Exception("Update cancelled by user."); };
                splash.Show();
                splash.ShowText("Online Updater: Connecting to server...");
            }            

            object[] args = new object[3] { splash, notifyOnLatest, null };
            thread.Start(args);
            
            while (thread.ThreadState == ThreadState.Running || thread.ThreadState == ThreadState.WaitSleepJoin)
            {
                Application.DoEvents();
                Thread.Sleep(100);
            }
            
            sfxPackageName = args[2] as string;

            if (showSplash) splash.Close();

            return sfxPackageName != null;
        }

        static void UpdaterProcedure(object arg)
        {
            object[] args = arg as object[];
            SplashScreen splash = args[0] as SplashScreen;

            bool showSplash = splash != null;
            bool notifyOnLatest = (bool)args[1];
            string sfxPackageName = null;

            HttpWebRequest WebReq = null;
            HttpWebResponse WebResp;
            try
            {
                WebReq = (HttpWebRequest)WebRequest.Create(string.Format(UrlToChangeLog));
                WebReq.Method = "GET";

                // Get Default Proxy
                IWebProxy proxy = WebRequest.GetSystemWebProxy();
                if (proxy != null && proxy.Credentials != null)
                {
                    WebReq.Proxy = proxy;
                }
                else
                    WebReq.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;

                WebReq.AllowAutoRedirect = true;
                WebResp = (HttpWebResponse)WebReq.GetResponse();
            }
            catch (ThreadAbortException)
            {                
                return;
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not connect to the server.\n The error message is: '" + e.Message + "'" + "\n Try visiting the sourceforge page linked in About form");                
                return;
            }

            if (showSplash)
            {
                splash.ShowText("Online Updater: Checking versions..."); splash.ShowProgress(20.0, 100.0);
            }

            Stream Answer = WebResp.GetResponseStream();
            if (WebResp.ContentEncoding == "gzip")
            {
                byte[] decompressed = DotaHIT.Core.Compression.DHCOMPRESSOR.ReadGzipDecompressed(Answer);
                Answer = new MemoryStream(decompressed);
            }
            using (StreamReader _Answer = new StreamReader(Answer))
            {
                // version string example
                // #0.999s

                string onlineVersion = null;
                do
                {
                    onlineVersion = _Answer.ReadLine();
                    if (onlineVersion == null)
                    {
                        MessageBox.Show("Invalid update file on the server");                        
                        sfxPackageName = null;
                        return;
                    }
                }
                while (onlineVersion.StartsWith("#") == false);
                onlineVersion = onlineVersion.TrimStart('#');

                DHCFG.Items["Update"]["LastUpdate"] = DateTime.Now.Ticks;

                // if current version is lower than online version
                if (CurrentVersion.CompareTo(onlineVersion) < 0)
                {
                    if (MessageBox.Show("A newer version is available: " + onlineVersion + " (your is " + CurrentVersion + ")" +
                        "\nDo you want to proceed with update?", "DotA H.I.T. Updater", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        string fileName = "DotaHIT_v" + onlineVersion + ".rar";
                        if (Path.GetExtension(fileName).ToLower() != ".rar")
                        {
                            if (MessageBox.Show("The update configuration file, located on the sourceforge, does not contain a valid patch package file link" +
                                "\n Would you like to visit DotA H.I.T. online project page to download the update manually?", "DotA H.I.T. Updater", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start("http://sourceforge.net/projects/dotahit/");                                
                                sfxPackageName = null;
                                return;
                            }
                        }

                        if (showSplash)
                        {
                            splash.ShowText("Online Updater: Downloading update..."); splash.ShowProgress(40.0, 100.0);
                        }

                        string urlToSfxPackage = DefaultUrlToSfxPackage.Replace("packageName", fileName);
                        sfxPackageName = Path.ChangeExtension(fileName, ".exe");

                        WebClient wc = new WebClient();
                        try
                        {
                            wc.DownloadFile(urlToSfxPackage, sfxPackageName);
                            wc.DownloadFile(UrlToChangeLog, "changelog.txt");
                        }
                        catch (ThreadAbortException)
                        {
                            return;
                        }
                        catch
                        {
                            if (MessageBox.Show("Could not find a proper patch file on the server." +
                                "\n Would you like to visit DotA H.I.T. online project page to download the update manually?", "DotA H.I.T. Updater", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start("http://sourceforge.net/projects/dotahit/");                                
                                sfxPackageName = null;
                                return;
                            }
                        }

                        if (showSplash)
                        {
                            splash.ShowText("Online Updater: Done"); splash.ShowProgress(99.0, 100.0);                            
                        }

                        // save this version number before update
                        DHCFG.Items["Update"]["PrevVersion"] = CurrentVersion;

                        args[2] = sfxPackageName;
                        return;
                    }
                }
                else
                {
                    if (showSplash)
                    {
                        splash.ShowText("Online Updater: Done"); splash.ShowProgress(99.0, 100.0);
                    }

                    if (notifyOnLatest)
                        MessageBox.Show("You have the latest version available");
                }
            }
        }

        public static bool CreateBatchScript(string batchName, string sfxPackageName)
        {
            FileStream stream = File.Create(batchName);
            using (StreamWriter sw = new StreamWriter(stream))
            {
                sw.WriteLine("sleep 0.5");

                /*foreach (KeyValuePair<string, string> kvp in args)
                {
                    sw.WriteLine("del " + kvp.Key);
                    sw.WriteLine("ren " + kvp.Value + " " + kvp.Key);
                }*/
                
                sw.WriteLine(sfxPackageName + " -s");
                sw.WriteLine("del " + sfxPackageName);

                sw.WriteLine("start \"\" " + Path.GetFileName(System.Windows.Forms.Application.ExecutablePath));                
                sw.WriteLine("del %0");
            }            

            return true;
        }
    }
}
