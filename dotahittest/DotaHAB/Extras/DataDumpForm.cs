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
    public partial class DataDumpForm : Form
    {
        internal int mbX = -1;
        internal int mbY = -1;
        
        MainForm mainOwner = null;

        public DataDumpForm()
        {
            InitializeComponent();
            compactRB.Checked = true;
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

        internal void compactDump()
        {
            HabPropertiesCollection hpcHeroes = new HabPropertiesCollection();

            List<HabPropertiesCollection> unitHpcList = new List<HabPropertiesCollection>();
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["Profile"]);
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitData"]);
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitWeapons"]);
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitBalance"]);
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitAbilities"]);
            unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitUI"]);

            HabPropertiesCollection hpcAbilities = new HabPropertiesCollection();

            List<HabPropertiesCollection> abilHpcList = new List<HabPropertiesCollection>();
            abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["Profile"]);
            abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["AbilityData"]);

            foreach (unit tavern in DHLOOKUP.taverns)
                foreach (string unitID in tavern.sellunits)
                {
                    HabProperties hps = new HabProperties();
                    foreach (HabPropertiesCollection hpc in unitHpcList)
                        hps.Merge(hpc[unitID], false);

                    hps.name = unitID;

                    List<string> abils = hps.GetStringListValue("heroAbilList");
                    foreach (string abilID in abils)
                        if (!hpcAbilities.ContainsKey(abilID))
                        {
                            HabProperties hpsAbilData = new HabProperties();
                            foreach (HabPropertiesCollection hpcAbilData in abilHpcList)
                                hpsAbilData.Merge(hpcAbilData[abilID]);

                            hpsAbilData.name = abilID;

                            hpcAbilities.Add(hpsAbilData);
                        }

                    hpcHeroes.Add(hps);
                }

            List<string> filelist = new List<string>(2);

            if (heroesCB.Checked)
            {
                hpcHeroes.SaveToFile(Application.StartupPath + "\\" + "heroes.txt");
                filelist.Add("heroes.txt");
            }

            if (heroesAbilsCB.Checked)
            {
                hpcAbilities.SaveToFile(Application.StartupPath + "\\" + "heroes_abilities.txt");
                filelist.Add("heroes_abilities.txt");
            }

            string filenames = "";
            foreach (string filename in filelist)
                filenames += " " + filename + ",";

            MessageBox.Show("Data dumped to " + Application.StartupPath + "\nFiles:" + filenames.TrimEnd(','));
        }
        internal void fullDump()
        {
            List<string> filelist = new List<string>(3);

            if (unitsCB.Checked)
            {
                HabPropertiesCollection hpcUnits = new HabPropertiesCollection();

                List<HabPropertiesCollection> unitHpcList = new List<HabPropertiesCollection>();                
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["Profile"]);
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitData"]);
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitWeapons"]);
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitBalance"]);
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitAbilities"]);
                unitHpcList.Add(DHMpqDatabase.UnitSlkDatabase["UnitUI"]);

                foreach (HabPropertiesCollection hpc in unitHpcList)
                    hpcUnits.Merge(hpc, false, false);

                hpcUnits.SaveToFile(Application.StartupPath + "\\" + "units.txt");
                filelist.Add("units.txt");
            }

            if (abilitiesCB.Checked)
            {
                HabPropertiesCollection hpcAbilities = new HabPropertiesCollection();

                List<HabPropertiesCollection> abilHpcList = new List<HabPropertiesCollection>();
                abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["Profile"]);
                abilHpcList.Add(DHMpqDatabase.AbilitySlkDatabase["AbilityData"]);

                foreach (HabPropertiesCollection hpc in abilHpcList)
                    hpcAbilities.Merge(hpc);                

                hpcAbilities.SaveToFile(Application.StartupPath + "\\" + "abilities.txt");
                filelist.Add("abilities.txt");
            }

            if (itemsCB.Checked)
            {
                HabPropertiesCollection hpcItems = new HabPropertiesCollection();

                List<HabPropertiesCollection> itemHpcList = new List<HabPropertiesCollection>();
                itemHpcList.Add(DHMpqDatabase.ItemSlkDatabase["Profile"]);
                itemHpcList.Add(DHMpqDatabase.ItemSlkDatabase["ItemData"]);

                foreach (HabPropertiesCollection hpc in itemHpcList)
                    hpcItems.Merge(hpc);

                hpcItems.SaveToFile(Application.StartupPath + "\\" + "items.txt");
                filelist.Add("items.txt");
            }

            if (upgradesCB.Checked)
            {
                HabPropertiesCollection hpcUpgrades = new HabPropertiesCollection();

                List<HabPropertiesCollection> upgradeHpcList = new List<HabPropertiesCollection>();
                upgradeHpcList.Add(DHMpqDatabase.UpgradeSlkDatabase["Profile"]);
                upgradeHpcList.Add(DHMpqDatabase.UpgradeSlkDatabase["UpgradeData"]);

                foreach (HabPropertiesCollection hpc in upgradeHpcList)
                    hpcUpgrades.Merge(hpc);

                hpcUpgrades.SaveToFile(Application.StartupPath + "\\" + "upgrades.txt");
                filelist.Add("upgrades.txt");
            }

            string filenames = "";
            foreach (string filename in filelist)
                filenames += " " + filename + ",";

            MessageBox.Show("Data dumped to " + Application.StartupPath + "\nFiles:" + filenames.TrimEnd(','));
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