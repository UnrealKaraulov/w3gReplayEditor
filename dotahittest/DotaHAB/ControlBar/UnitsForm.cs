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

namespace DotaHIT
{
    public partial class UnitsForm : Form
    {
        internal int mbX = -1;
        internal int mbY = -1;

        ImageList fakeImgList = null;

        int borderWidth = 2;
        Pen framePen = null;        

        public UnitsForm()
        {
            InitializeComponent();

            framePen = new Pen(Color.Lime, (float)borderWidth);

            fakeImgList = new ImageList();
            fakeImgList.ImageSize = new Size(32, 32);

            unitsLV.LargeImageList = fakeImgList;
            unitsLV.SmallImageList = fakeImgList;
            
            Current.player.selection.ListChanged += new MethodInvoker(selection_ListChanged);
        }                

        public void SetParent(MainForm parentForm)
        {         
            this.Owner = parentForm;         
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

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = cp.Style | Win32Msg.WS_THICKFRAME;
                return cp;
            }
        }

        private void closeB_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void UnitsForm_VisibleChanged(object sender, EventArgs e)
        {
            if (this.Visible) 
                ReloadUnits();
        }       

        void selection_ListChanged()
        {
            if (this.Visible)
                ReloadUnits();
        }

        int compareUnits(unit x, unit y)
        {
            return Comparer<int>.Default.Compare(x.handle, y.handle);
        }

        void ReloadUnits()
        {
            unitsLV.BeginUpdate();
            unitsLV.Items.Clear();

            List<unit> units = new List<unit>(Current.player.units.Values.Count);
            foreach (unit u in Current.player.units.Values)
                units.Add(u);

            units.Sort(compareUnits);

            foreach (unit u in units)
            {
                if (u.pathing == false) continue;

                ListViewItem lvi = new ListViewItem(u.ID);

                lvi.ImageKey = u.iconName;
                lvi.ToolTipText = u.ID;
                lvi.Tag = u;
                lvi.Selected = Current.player.selection.Contains(u);
                lvi.Focused = lvi.Selected;

                unitsLV.Items.Add(lvi);
            }

            unitsLV.EndUpdate();
        }

        void makeSelectionValid()
        {
            Current.player.selection.BeginUpdate();

            for (int i = 0; i < Current.player.selection.Count && Current.player.selection.Count > 1 ; i++)
                if (Current.player.selection[i].IsBuilding)
                {
                    unit u = Current.player.selection[i];

                    for (int j = 0; j < unitsLV.SelectedItems.Count; j++ )                    
                        if (unitsLV.SelectedItems[j].Tag == u)
                            unitsLV.SelectedItems[j].Selected = false;
                    
                    i--;                    
                }

            Current.player.selection.EndUpdate();
        }

        private void unitsLV_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                Current.player.selection.BeginUpdate();
        }

        private void unitsLV_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                switch (unitsLV.SelectedItems.Count)
                {
                    case 0:
                        break;

                    default:
                        unitsContextMenuStrip.Show(MousePosition);
                        break;
                }
            else
            {                
                makeSelectionValid();
                Current.player.selection.EndUpdate();
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i=0; i<unitsLV.SelectedItems.Count;)
            {
                unit u = unitsLV.SelectedItems[i].Tag as unit;
                unitsLV.Items.Remove(unitsLV.SelectedItems[i]);
                u.destroy();                
            }            
        }

        private void unitsLV_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            Current.player.selection.BeginUpdate();

            unit u = e.Item.Tag as unit;

            if (e.IsSelected)
            {
                if (!Current.player.selection.Contains(u))
                    Current.player.selection.Add(u);
            }
            else
                Current.player.selection.Remove(e.Item.Tag as unit);

            Current.player.selection.EndUpdate();            
        }

        private void unitsLV_DrawItem(object sender, DrawListViewItemEventArgs e)
        {            
            e.DrawBackground();

            Image image = (Image)DHRC.Default.GetImage(e.Item.ImageKey);

            if (image != null)
                e.Graphics.DrawImage(image,
                                e.Bounds.X + borderWidth, e.Bounds.Y + borderWidth-1 /*+ borderWidth*/,
                                fakeImgList.ImageSize.Width - borderWidth*2, fakeImgList.ImageSize.Height - borderWidth);
            
            if (e.Item.Selected)
                e.Graphics.DrawRectangle(framePen,
                e.Bounds.X + 1, e.Bounds.Y + borderWidth-1,
                fakeImgList.ImageSize.Width - borderWidth, fakeImgList.ImageSize.Height - borderWidth);
            
            e.Graphics.DrawString(e.Item.Text, e.Item.ListView.Font, Brushes.White,
                e.Bounds.X + fakeImgList.ImageSize.Width, e.Bounds.Y + ((e.Bounds.Height-e.Item.ListView.Font.GetHeight()) / 2) -1, StringFormat.GenericDefault);
        }                
    }
}