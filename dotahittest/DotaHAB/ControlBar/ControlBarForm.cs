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
using DotaHIT.Core.Resources.Media;

namespace DotaHIT
{
    public partial class ControlBarForm : Form
    {
        internal MainForm mainOwner = null;
        List<FloatingListForm> floatingForms = new List<FloatingListForm>();

        LogForm logForm = new LogForm();
        ActionsForm actionsForm = null;
        UnitsForm unitsForm = null;

        internal ToolTipForm toolTip = null;        

        int layout = 0;
        int used_layout = 0;

        ToolStripButton switchButton = null;        

        internal int shiftX = 0;
        internal int shiftY = 0;

        bool showGold = true;
        bool isPrepared = false;

        public ControlBarForm()
        {
            InitializeComponent();

            PrepareConfigUI();
        }        

        public void SetParent(MainForm parentForm, List<FloatingListForm> floatingForms)
        {
            mainOwner = parentForm;
            this.Owner = parentForm;

            logForm.Owner = this;            

            this.floatingForms = floatingForms;

            Point bp = parentForm.GetBindPoint(true);            
            parentForm.PreMove += new MainForm.PreMoveEvent(parentForm_PreMove);

            bp.Y = bp.Y + parentForm.Height;

            this.SetDesktopLocation(bp.X, bp.Y);

            shiftX = parentForm.Location.X - bp.X;
            shiftY = parentForm.Location.Y - bp.Y;
        }

        void parentForm_PreMove(object sender, Point point)
        {            
            SetPos(point);
        }

        private void SetPos(Point point)
        {
            this.SetDesktopLocation(point.X - shiftX, point.Y - shiftY);
        }        

        public void ApplyConfig()
        {
            if (DHCFG.Items["ControlBar"].ContainsKey("SentinelPlayers"))
            {
                sentinelPlayersLL.Tag = DHCFG.Items["ControlBar"].GetIntValue("SentinelPlayers");
                sentinelPlayersLL.Text = sentinelPlayersLL.Tag + "";
            }

            if (DHCFG.Items["ControlBar"].ContainsKey("ScourgePlayers"))
            {
                scourgePlayersLL.Tag = DHCFG.Items["ControlBar"].GetIntValue("ScourgePlayers");
                scourgePlayersLL.Text = scourgePlayersLL.Tag + "";
            }

            if (DHCFG.Items["ControlBar"].ContainsKey("GameSpeed"))
                SetGameSpeed(DHCFG.Items["ControlBar"].GetDoubleValue("GameSpeed"));
            else
                SetGameSpeed(1.0);

            if (DHCFG.Items["ControlBar"].ContainsKey("ShowGold"))
            {
                showGold = DHCFG.Items["ControlBar"].GetIntValue("ShowGold") == 1;
                ShowGold();
            }

            if (DHCFG.Items["ControlBar"].ContainsKey("ShowBuildCost"))            
                if (DHCFG.Items["ControlBar"].GetIntValue("ShowBuildCost") == 0)
                    buildCostSwitchTSMI_Click(null, EventArgs.Empty);

            this.used_layout = DHCFG.Items["ControlBar"].GetIntValue("UsedLayout", 1);

            if (!DHCFG.Items["ControlBar"].ContainsKey("Enabled") ||
                DHCFG.Items["ControlBar"].GetIntValue("Enabled") == 1)
                Display();
        }

        internal void PrepareConfigUI()
        {
            foreach (ToolStripButton tsb in speedTS.Items)
            {
                tsb.Image = GetImage(10, 10, tsb.BackColor);
                tsb.BackColor = Color.Black;
                tsb.TextImageRelation = TextImageRelation.Overlay;
                tsb.Padding = new Padding(2);
                tsb.Tag = (tsb.MergeIndex < 0) ? 0.5 : (1.0 + tsb.MergeIndex);
            }
        }

        internal Image GetImage(int w, int h, Color color)
        {            
            Image img = new Bitmap(w, h);
            Graphics g = Graphics.FromImage(img);
            g.Clear(color);
            return img;
        }

        public void Clear()
        {
            logForm.Clear();
        }

        public void PrepareGameUI()
        {
            if (buildCostTSMI.Checked)
                DHLOOKUP.CollectItemCombiningData();                
        }
        public void PrepareControls()
        {
            int sentinelPlayers = (sentinelPlayersLL.Tag is int) ? (int)sentinelPlayersLL.Tag : 1;
            int scourgePlayers = (scourgePlayersLL.Tag is int) ? (int)scourgePlayersLL.Tag : 1;

            /*
             * set Ac[0]=Player(0)
             * set Ac[1]=Player(1)
             * set Ac[2]=Player(2)
             * set Ac[3]=Player(3)
             * set Ac[4]=Player(4)
             * set Ac[5]=Player(5)
             * set AC[0]=Player(6)
             * set AC[1]=Player(7)
             * set AC[2]=Player(8)
             * set AC[3]=Player(9)
             * set AC[4]=Player(10)
             * set AC[5]=Player(11)
             * set Ad=Player(12)
             * set AD=Player(15)
             */

            foreach (player p in player.players)
            {
                int number = p.get_id();

                if (number >= 1 && number <= 5)
                    p.playing = (number <= sentinelPlayers);
                else
                if (number >= 7 && number <= 11)
                    p.playing = (number <= scourgePlayers + 6);
                else
                    p.playing = false;
            }

            Current.player = player.players[1];

            Current.player.gold_changed += new DotaHIT.Jass.Native.Events.DHJassEventHandler(ControlPanelForm_gold_changed);
            Current.player.message += new DotaHIT.Jass.Native.Events.DHJassEventHandler(ControlPanelForm_message);

            Current.player.unit_summon += new DotaHIT.Jass.Native.Events.DHJassEventHandler(player_unit_summon);

            if (actionsForm != null)
            {
                actionsForm.Close();
                actionsForm = null;
            }

            isPrepared = true;
        }

        void player_unit_summon(object sender, DotaHIT.Jass.Native.Events.DHJassEventArgs e)
        {
            unit u = e.args["unit"] as unit;

            // if it's a hero
            if (u.primary != PrimAttrType.None)
            {
                Current.unit = u;
                Current.unit.playSound(UnitAckSounds.Ready);                
            }
        }

        public void manual_summon(unit hero)
        {
            if (hero.IsDisposed) return;

            Current.unit = hero;
            Current.unit.Updated = true;
            Current.unit.playSound(UnitAckSounds.Ready);
        }

        void ControlPanelForm_message(object sender, DotaHIT.Jass.Native.Events.DHJassEventArgs e)
        {
            string message = e.args["messageString"] as string;

            if (message == " ") return;

            if (!logForm.Visible) chatLogB.Text = "log*";
            logForm.AddToLog(message);
        }

        void ControlPanelForm_gold_changed(object sender, DotaHIT.Jass.Native.Events.DHJassEventArgs e)
        {
            heroGoldTSMI.Tag = (e.args["player"] as player).Gold;
            ShowGold();
        }

        internal void ShowGold(bool show)
        {
            showGold = show;
            ShowGold();
        }
        internal void ShowGold()
        {
            if (showGold)
            {
                if (heroGoldTSMI.Font != UIFonts.boldArial8)
                    heroGoldTSMI.Font = UIFonts.boldArial8;
                heroGoldTSMI.Text = ((heroGoldTSMI.Tag is int) ? (int)heroGoldTSMI.Tag : 0) + "";
            }
            else
            {
                if (heroGoldTSMI.Font != UIFonts.boldVerdana)
                    heroGoldTSMI.Font = UIFonts.boldVerdana;
                heroGoldTSMI.Text = "∞";
            }
        }

        internal void ShowBuildCost(bool show)
        {
            if (buildCostTSMI.Checked != show)
                buildCostSwitchTSMI_Click(null, EventArgs.Empty);
        }

        internal void RefreshBuildCost()
        {
            if (Current.unit == null || !buildCostTSMI.Checked) return;

            int buildCost = 0;

            foreach (DBITEMSLOT itemSlot in Current.unit.Inventory)
                if (!itemSlot.IsNull)
                    buildCost += mainOwner.ilForm.GetGoldCost(itemSlot.Item);
            
            buildCostTSMI.Text = buildCost+ "";
        }

        public void Display()
        {
            if (this.Visible == false)
                this.SetLayout(used_layout);
            else
                SetLayout(layout);

            this.Show();
        }

        public void Remove()
        {            
            used_layout = layout;
            this.Visible = false;
            SetLayout(0);            
        }

        internal void SetLayout(int layout)
        {
            if (this.layout == layout) return;

            this.layout = layout;            

            switch (layout)
            {
                case 0:
                    foreach (FloatingListForm ff in floatingForms)
                        ff.SetOffset(0, -this.Height);
                    this.shiftY -= Owner.Height + this.Height;
                    this.Top += Owner.Height + this.Height;
                    break;

                case 1: 
                    foreach (FloatingListForm ff in floatingForms)
                        ff.SetOffset(0, this.Height);
                    this.shiftY += Owner.Height + this.Height;
                    this.Top -= Owner.Height + this.Height;
                    break;
            }
        }

        private void captionB_Click(object sender, EventArgs e)
        {
            int new_layout = layout ^ 1;
            SetLayout(new_layout);
        }

        internal void WriteConfig()
        {
            DHCFG.Items["ControlBar"]["Enabled"] = this.Visible ? 1 : 0;
            DHCFG.Items["ControlBar"]["UsedLayout"] = layout;
            DHCFG.Items["ControlBar"]["ShowGold"] = showGold ? 1 : 0;
            DHCFG.Items["ControlBar"]["ShowBuildCost"] = (buildCostTSMI.Checked) ? 1 : 0;
            DHCFG.Items["ControlBar"]["SentinelPlayers"] = (sentinelPlayersLL.Tag is int) ? (int)sentinelPlayersLL.Tag : 1;
            DHCFG.Items["ControlBar"]["ScourgePlayers"] = (scourgePlayersLL.Tag is int) ? (int)scourgePlayersLL.Tag : 1;
            DHCFG.Items["ControlBar"]["GameSpeed"] = (switchButton.Tag is double) ? (double)switchButton.Tag : 1;
        }     

        private void teamPlayersLL_MouseDown(object sender, MouseEventArgs e)
        {
            LinkLabel ll = sender as LinkLabel;

            int players;
            if (ll.Tag is int)
                players = (int)ll.Tag;
            else
                players = 1;

            switch (e.Button)
            {
                case MouseButtons.Left:
                    players++;
                    if (players > 5) players = 5;
                    break;
                case MouseButtons.Right:
                    players--;
                    if (players < 1 && ll == sentinelPlayersLL) players = 1;
                    else if (players < 0) players = 0;
                    break;
            }
            ll.Tag = players;
            ll.Text = players + "";
        }

        private void heroGoldTSMI_Click(object sender, EventArgs e)
        {
            showGold = !showGold;
            ShowGold();
        }

        private void chatLogB_Click(object sender, EventArgs e)
        {
            logForm.Visible = !logForm.Visible;
            if (logForm.Visible)
                chatLogB.Text = "log";
        }

        private void messageRTB_KeyDown(object sender, KeyEventArgs e)
        {            
            switch (e.KeyCode)
            {
                case Keys.Enter:                                     
                    if (isPrepared) Current.player.OnChat(messageRTB.Text);
                    messageRTB.Clear();
                    e.Handled = true;
                    break;
            }
        }

        private void buildCostSwitchTSMI_Click(object sender, EventArgs e)
        {
            if (buildCostTSMI.Checked)
            {                
                buildCostTSMI.Text = "0";
                buildCostTSMI.Visible = false;
                buildCostTSMI.Checked = false;
                buildCostSwitchTSMI.Visible = true;                
            }
            else
            {                              
                if (Current.player != null) DHLOOKUP.CollectItemCombiningData();
                buildCostTSMI.Visible = true;
                buildCostTSMI.Checked = true;
                buildCostSwitchTSMI.Visible = false;
                RefreshBuildCost();
            }
        }

        private void actionsB_Click(object sender, EventArgs e)
        {
            if (Current.map == null)
            {
                MessageBox.Show("Load a map first, then select a hero");
                return;
            }
            else
                if (Current.unit == null)
                {
                    MessageBox.Show("Select a hero first");
                    return;
                }

            if (actionsForm == null)
            {
                actionsForm = new ActionsForm();
                actionsForm.Owner = this;
            }

            actionsForm.Visible = !actionsForm.Visible;
        }               

        void tsb_MouseLeave(object sender, EventArgs e)
        {
            if (switchButton != sender)
                (sender as ToolStripItem).BackColor = Color.Black;
        }

        void tsb_MouseEnter(object sender, EventArgs e)
        {
            if (!speedTS.Focused)
                speedTS.Focus();
            if (switchButton != sender)
                (sender as ToolStripItem).BackColor = Color.Gray;
        }

        void tsb_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                SelectGameSpeed(sender as ToolStripButton);            
        }

        internal void UpdateGameSpeed(double speed)
        {
            DotaHIT.Jass.Native.Types.timer.timeFactor = 1 / speed;
        }

        void SelectGameSpeed(ToolStripButton switchButton)
        {
            if (this.switchButton == switchButton)
                return;

            this.switchButton = switchButton;

            foreach (ToolStripButton b in speedTS.Items)
            {
                if (switchButton == b)
                    b.BackColor = Color.White;
                else
                    b.BackColor = Color.Black;                                 
            }

            UpdateGameSpeed((double)switchButton.Tag);
        }

        internal void SetGameSpeed(double speed)
        {
            int index = (int)((speed < 1) ? -1 : speed - 1);
            foreach (ToolStripButton tsb in speedTS.Items)
                if (tsb.MergeIndex == index)
                {
                    SelectGameSpeed(tsb);
                    break;
                }

            if (switchButton == null) SetGameSpeed(1.0);
        }

        private void unitsB_Click(object sender, EventArgs e)
        {
            if (Current.map == null)
            {
                MessageBox.Show("Load a map first");
                return;
            }           

            if (unitsForm == null)
            {
                unitsForm = new UnitsForm();
                unitsForm.Owner = this;
            }

            unitsForm.Visible = !unitsForm.Visible;
        }
    }
}