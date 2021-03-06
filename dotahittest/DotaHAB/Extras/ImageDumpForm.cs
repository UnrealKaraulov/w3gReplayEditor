using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DotaHIT.DatabaseModel.Data;
using DotaHIT.DatabaseModel.DataTypes;
using DotaHIT.Core;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Specialized;
using DotaHIT.Jass.Native.Types;
using DotaHIT.Jass;
using DotaHIT.Core.Resources;
using DotaHIT.Jass.Types;
using System.IO;

namespace DotaHIT
{
    public partial class ImageDumpForm : Form
    {
        internal int mbX = -1;
        internal int mbY = -1;
        
        MainForm mainOwner = null;

        bool silentDump = false;
        string imageFolder = Application.StartupPath + "\\" + "DotA Images";

        public ImageDumpForm()
        {
            InitializeComponent();
            compactRB.Checked = true;
        }

        public ImageDumpForm(bool full, bool asBitmap, bool maintainStructure, string path)
        {
            InitializeComponent();

            if (full)
            {
                fullRB.Checked = true;
                unitsCB.Checked = true;
                itemsCB.Checked = true;
                abilitiesCB.Checked = true;

                if (asBitmap)                    
                    bmpRB.Checked = true;
                else
                    blpRB.Checked = true;

                createDirsCB.Checked = maintainStructure;

                imageFolder = path;
            }

            silentDump = true;
            dumpB_Click(null, EventArgs.Empty);
        }

        public void SetParent(MainForm parentForm)
        {         
            this.Owner = parentForm;
            mainOwner = parentForm;
        }

        private void captionB_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mbX = MousePosition.X - this.Location.X;
                mbY = MousePosition.Y - this.Location.Y;
            }
            else
                mbX = mbY = -1;
        }

        private void captionB_MouseMove(object sender, MouseEventArgs e)
        {
            if (mbX != -1 && mbY != -1)
                if ((MousePosition.X - mbX) != 0 && (MousePosition.Y - mbY) != 0)
                    this.SetDesktopLocation(MousePosition.X - mbX, MousePosition.Y - mbY);
        }

        private void captionB_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                mbX = mbY = -1;
        }     

        private void closeB_Click(object sender, EventArgs e)
        {            
            this.Hide();
        }

        internal string ImageFolder
        {
            get
            {
                return imageFolder;
            }
            set
            {
                imageFolder = value;
            }
        }

        internal string GetProperPath(string imagePath)
        {
            string fullPath;

            if (createDirsCB.Checked)
            {                
                string path = Path.GetDirectoryName(imagePath);
                fullPath = ImageFolder + "\\" + path;                
            }
            else
                fullPath = ImageFolder;

            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            return fullPath + "\\" + Path.GetFileName(imagePath);
        }
        internal void SaveImages(List<string> imagePathList)
        {
            foreach (string imagePath in imagePathList)
            {
                if (blpRB.Checked)
                {
                    DHMpqFile image = DHRC.Default.GetImageFile(imagePath);
                    if (!image.IsNull)
                        image.ToFile(GetProperPath(imagePath));
                }
                else
                {
                    Bitmap image = DHRC.Default.GetImage(imagePath);

                    if (image != null)
                        image.Save(GetProperPath(Path.ChangeExtension(imagePath, ".png")));
                }
            }
        }

        internal void compactDump()
        {
            HabPropertiesCollection hpcHeroes = new HabPropertiesCollection();

            List<HabPropertiesCollection> unitHpcList = new List<HabPropertiesCollection>();
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["Profile"]);            
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitAbilities"]);

            List<string> unitImages = new List<string>();

            HabPropertiesCollection hpcAbilities = new HabPropertiesCollection();

            List<HabPropertiesCollection> abilHpcList = new List<HabPropertiesCollection>();
            abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["Profile"]);
            abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["AbilityData"]);

            List<string> abilityImages = new List<string>();

            string imageName;

            foreach (unit tavern in DHLOOKUP.taverns)
                foreach (string unitID in tavern.sellunits)
                {
                    HabProperties hps = new HabProperties();
                    foreach (HabPropertiesCollection hpc in unitHpcList)
                        hps.Merge(hpc[unitID]);

                    hps.name = unitID;

                    imageName = hps.GetStringValue("Art");
                    if (!string.IsNullOrEmpty(imageName) && !unitImages.Contains(imageName))
                        unitImages.Add(imageName);

                    List<string> abils = hps.GetStringListValue("heroAbilList");
                    foreach (string abilID in abils)
                        if (!hpcAbilities.ContainsKey(abilID))
                        {
                            HabProperties hpsAbilData = new HabProperties();
                            foreach (HabPropertiesCollection hpcAbilData in abilHpcList)
                                hpsAbilData.Merge(hpcAbilData[abilID]);

                            imageName = hpsAbilData.GetStringValue("Art");
                            if (!string.IsNullOrEmpty(imageName) && !abilityImages.Contains(imageName))
                                abilityImages.Add(imageName);

                            hpsAbilData.name = abilID;

                            hpcAbilities.Add(hpsAbilData);
                        }

                    hpcHeroes.Add(hps);
                }

            List<string> filelist = new List<string>(2);

            if (heroesCB.Checked)
            {
                SaveImages(unitImages);
            }

            if (heroesAbilsCB.Checked)
            {
                SaveImages(abilityImages);               
            }            

            if (!silentDump)
                MessageBox.Show("Images extracted to " + ImageFolder);
        }
        internal void fullDump()
        {            
            string imageName;

            if (unitsCB.Checked)
            {
                HabPropertiesCollection hpcUnits = new HabPropertiesCollection();

                List<HabPropertiesCollection> unitHpcList = new List<HabPropertiesCollection>();
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["Profile"]);                
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitAbilities"]);                

                foreach (HabPropertiesCollection hpc in unitHpcList)
                    hpcUnits.Merge(hpc);

                List<string> unitImages = new List<string>();

                foreach (HabProperties hpsUnit in hpcUnits)
                {
                    imageName = hpsUnit.GetStringValue("Art");

                    if (!string.IsNullOrEmpty(imageName) && !unitImages.Contains(imageName))
                        unitImages.Add(imageName);
                }

                SaveImages(unitImages);
            }

            if (abilitiesCB.Checked)
            {
                HabPropertiesCollection hpcAbilities = new HabPropertiesCollection();

                List<HabPropertiesCollection> abilHpcList = new List<HabPropertiesCollection>();
                abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["Profile"]);
                abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["AbilityData"]);

                foreach (HabPropertiesCollection hpc in abilHpcList)
                    hpcAbilities.Merge(hpc);

                List<string> abilityImages = new List<string>();

                foreach (HabProperties hpsAbility in hpcAbilities)
                {
                    imageName = hpsAbility.GetStringValue("Art");

                    if (!string.IsNullOrEmpty(imageName) && !abilityImages.Contains(imageName))
                        abilityImages.Add(imageName);
                }

                SaveImages(abilityImages);                
            }

            if (itemsCB.Checked)
            {
                HabPropertiesCollection hpcItems = new HabPropertiesCollection();

                List<HabPropertiesCollection> itemHpcList = new List<HabPropertiesCollection>();
                itemHpcList.Add(DHMpqDatabase.ItemSlkDatabase["Profile"]);
                itemHpcList.Add(DHMpqDatabase.ItemSlkDatabase["ItemData"]);

                foreach (HabPropertiesCollection hpc in itemHpcList)
                    hpcItems.Merge(hpc);

                List<string> itemImages = new List<string>();

                foreach (HabProperties hpsItem in hpcItems)
                {
                    imageName = hpsItem.GetStringValue("Art");

                    if (!string.IsNullOrEmpty(imageName) && !itemImages.Contains(imageName))
                        itemImages.Add(imageName);
                }

                SaveImages(itemImages);  
            }

            if (!silentDump)
                MessageBox.Show("Images extracted to " + ImageFolder);
        }

        private void anyRB_CheckedChanged(object sender, EventArgs e)
        {
            compactGroupBox.Enabled = compactRB.Checked;
            compactRB.ForeColor = compactRB.Checked ? Color.White : Color.FromArgb(82, 85, 82);

            fullGroupBox.Enabled = fullRB.Checked;
            fullRB.ForeColor = fullRB.Checked ? Color.White : Color.FromArgb(82, 85, 82);
        }

        private void dumpB_Click(object sender, EventArgs e)
        {
            if (compactRB.Checked)
                compactDump();
            else
                if (fullRB.Checked)
                    fullDump();
        }
    }
}