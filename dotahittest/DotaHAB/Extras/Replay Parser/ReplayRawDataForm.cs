using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DotaHIT.Core;
using Deerchao.War3Share.W3gParser;
using System.Globalization;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using DotaHIT.Jass.Types;
using DotaHIT.Core.Resources;
using System.Diagnostics;
using DotaHIT.Jass.Native.Constants;

namespace DotaHIT.Extras.Replay_Parser
{
    public partial class ReplayRawDataForm : Form
    {
        Replay replay = null;       
        List<Action> actionList = new List<Action>();
        BindingList<Action> filteredActionList = new BindingList<Action>();
        Dictionary<int, List<KeyValuePair<int, string>>> dcObjectCache = new Dictionary<int, List<KeyValuePair<int, string>>>();
        Dictionary<int, string> dcActionDescriptions = new Dictionary<int, string>();
        Dictionary<int, string> dcPlayerHero = new Dictionary<int, string>();        

        class DBListView : ListView
        {
            public DBListView()
            {
                this.DoubleBuffered = true;                
            }
        }

        public ReplayRawDataForm()
        {
            InitializeComponent();

            foreach(string item in actionsCLB.Items)
                if (item.StartsWith("0x"))
                {
                    int actionId = Convert.ToInt32(item.Substring(0, 4), 16);
                    dcActionDescriptions[actionId] = item.Substring(7);
                }

            actionAllB_Click(null, EventArgs.Empty);            
        }

        public void Init(Replay replay)
        {
            this.replay = replay;

            InitListByTeam(sentinelCLB, replay.GetTeamByType(TeamType.Sentinel));
            InitListByTeam(scourgeCLB, replay.GetTeamByType(TeamType.Scourge));
            InitListByTeam(observersCLB, replay.GetTeamByType(TeamType.Observers));

            playerAllB_Click(null, EventArgs.Empty);

            timeTrackBar.Maximum = (int)replay.GameLength.TotalSeconds;
            timeTrackBar.Value = 0;
            timeTextBox.Text = SecondsToTimeString(timeTrackBar.Value);

            timeFrame5mRB.Checked = true;

            InitActions();
        }

        void InitListByTeam(CheckedListBox clb, Team team)
        {
            clb.Items.Clear();

            foreach (Player p in team.Players)
                clb.Items.Add(p);
        }

        void InitActions()
        {
            actionList.Clear();
            dcObjectCache.Clear();

            if (string.IsNullOrEmpty(replay.FileName))
                return;

            Stream stream = File.OpenRead(replay.FileName);

            using (stream)
            {
                MemoryStream blocksData = LoadHeader(stream);

                using (BinaryReader reader = new BinaryReader(blocksData))
                {
                    reader.BaseStream.Position = replay.ActionListOffset;
                    LoadActions(reader);
                }
            }
        }

        private MemoryStream LoadHeader(Stream stream)
        {
            MemoryStream blocksData = new MemoryStream();
            using (BinaryReader reader = new BinaryReader(stream))
            {
                #region 2.0 [Header]

                #region doc

                //offset | size/type | Description
                //-------+-----------+-----------------------------------------------------------
                //0x0000 | 28 chars  | zero terminated string "Warcraft III recorded game\0x1A\0"
                //0x001c |  1 dword  | fileoffset of first compressed data block (header size)
                //       |           |  0x40 for WarCraft III with patch <= v1.06
                //       |           |  0x44 for WarCraft III patch >= 1.07 and TFT replays
                //0x0020 |  1 dword  | overall size of compressed file
                //0x0024 |  1 dword  | replay header version:
                //       |           |  0x00 for WarCraft III with patch <= 1.06
                //       |           |  0x01 for WarCraft III patch >= 1.07 and TFT replays
                //0x0028 |  1 dword  | overall size of decompressed data (excluding header)
                //0x002c |  1 dword  | number of compressed data blocks in file
                //0x0030 |  n bytes  | SubHeader (see section 2.1 and 2.2)

                #endregion

                //header string
                reader.BaseStream.Seek(28, SeekOrigin.Begin);

                int headerSize = reader.ReadInt32();

                //overall size of compressed file
                reader.ReadInt32();
                //version flag
                reader.ReadInt32();
                //overall size of decompressed data (excluding header)
                reader.ReadInt32();

                int nBlocks = reader.ReadInt32();

                #endregion                

                reader.BaseStream.Seek(headerSize, SeekOrigin.Begin);
                for (int i = 0; i < nBlocks; i++)
                {
                    #region [Data block header]

                    #region doc

                    //offset | size/type | Description
                    //-------+-----------+-----------------------------------------------------------
                    //0x0000 |  1  word  | size n of compressed data block (excluding header)
                    //0x0002 |  1  word  | size of decompressed data block (currently 8k)
                    //0x0004 |  1 dword  | unknown (probably checksum)
                    //0x0008 |  n bytes  | compressed data (decompress using zlib)

                    #endregion

                    ushort compressedSize = reader.ReadUInt16();
                    ushort decompressedSize = reader.ReadUInt16();
                    //unknown (probably checksum)
                    reader.ReadInt32();

                    byte[] decompressed = new byte[decompressedSize];
                    byte[] compressed = reader.ReadBytes(compressedSize);
                    using (InflaterInputStream zipStream = new InflaterInputStream(new MemoryStream(compressed)))
                    {
                        zipStream.Read(decompressed, 0, decompressedSize);
                    }
                    blocksData.Write(decompressed, 0, decompressedSize);

                    #endregion
                }
            }
            blocksData.Seek(0, SeekOrigin.Begin);            
            return blocksData;
        }

        private void LoadActions(BinaryReader reader)
        {
            int time = 0;            
            while (reader.BaseStream.Length - reader.BaseStream.Position > 0)
            {
                byte blockId = reader.ReadByte();
                switch (blockId)
                {
                    case 0x1A:
                    case 0x1B:
                    case 0x1C:                        
                        reader.BaseStream.Seek(reader.BaseStream.Position + 4, SeekOrigin.Begin);
                        break;
                    case 0x22:
                        reader.BaseStream.Seek(reader.BaseStream.Position + 5, SeekOrigin.Begin);
                        break;
                    case 0x23:
                        reader.BaseStream.Seek(reader.BaseStream.Position + 10, SeekOrigin.Begin);
                        break;
                    case 0x2F:
                        reader.BaseStream.Seek(reader.BaseStream.Position + 8, SeekOrigin.Begin);
                        break;
                    case 0x30: // 3 bytes (unknown & undocumented)
                        reader.BaseStream.Seek(reader.BaseStream.Position + 3, SeekOrigin.Begin);
                        break;
                    //leave game
                    case 0x17:
                        int reason = reader.ReadInt32();
                        byte playerId = reader.ReadByte();                        
                        Player p = replay.GetPlayerById(playerId);                                                
                        reader.ReadInt64();
                        actionList.Add(new Action(time, p, 0xFF, reason));                        
                        break;
                    //chat
                    case 0x20:
                        byte fromId = reader.ReadByte();
                        reader.ReadBytes(2);
                        byte chatType = reader.ReadByte();
                        TalkTo to = TalkTo.All;
                        if (chatType != 0x10)
                        {
                            to = (TalkTo)reader.ReadInt32();
                        }
                        string message = ParserUtility.ReadString(reader);
                        actionList.Add(new Action(time, replay.GetPlayerById(fromId), 0, to, replay.GetPlayerById((byte)(to - 3)), message));
                        break;
                    //time slot
                    case 0x1E:
                    case 0x1F:
                        short rest = reader.ReadInt16();
                        short increasedTime = reader.ReadInt16();
                        
                        time += increasedTime;
                        rest -= 2;
                        LoadTimeSlot(reader, rest, time);
                        break;
                    case 0:
                        return;
                    case 0x01: // 0 bytes (unknown & undocumented)                       
                        break;
                    default:
                        throw new W3gParserException("Unknown Block ID:" + blockId);
                }
            }
        }

        private void LoadTimeSlot(BinaryReader reader, short rest, int time)
        {            
            short flag;
            uint itemId;
            float x;
            float y;
            int objectId1;
            int objectId2;
            uint itemId1;
            uint itemId2;
            float x2;
            float y2;
            short unitCount;
            byte groupNo;
            byte slotNo;
            int len;
            string gamecache;
            string missonKey;
            string key;
            int value;            
            List<object> objects;

            while (rest > 0)
            {
                byte playerId = reader.ReadByte();
                Player player = replay.GetPlayerById(playerId);                
                short playerBlockRest = reader.ReadInt16();
                rest -= 3;
                short prest = playerBlockRest;

                while (prest > 0)
                {
                    #region
                    byte actionId = reader.ReadByte();
                    switch (actionId)
                    {
                        //pause game
                        case 0x01:                            
                            actionList.Add(new Action(time, player, actionId));
                            prest--;                            
                            break;
                        //resume game
                        case 0x02:
                            actionList.Add(new Action(time, player, actionId));
                            prest--;
                            break;
                        //set game speed
                        case 0x03:
                            actionList.Add(new Action(time, player, actionId));
                            prest -= 2;
                            break;
                        //icrease, decrease game speed
                        case 0x04:
                        case 0x05:
                            actionList.Add(new Action(time, player, actionId));
                            prest--;
                            break;
                        //save game
                        case 0x06:
                            len = 0;
                            while (reader.ReadByte() != 0)
                                len++;
                            actionList.Add(new Action(time, player, actionId));
                            prest -= (short)(len + 2);
                            break;
                        //game saved
                        case 0x07:
                            actionList.Add(new Action(time, player, actionId));
                            reader.ReadInt32();
                            prest -= 5;
                            break;
                        //unit ability without target
                        case 0x10:
                            flag = reader.ReadInt16();
                            itemId = reader.ReadUInt32();
                            //unknownA, unknownB
                            reader.ReadInt64();

                            actionList.Add(new Action(time, player, actionId, itemId));                            
                            prest -= 15;
                            break;
                        //unit ability with target position
                        case 0x11:
                            flag = reader.ReadInt16();
                            itemId = reader.ReadUInt32();
                            //unknownA, unknownB
                            reader.ReadInt64();
                            x = reader.ReadSingle();
                            y = reader.ReadSingle();                            
                            
                            actionList.Add(new Action(time, player, actionId, itemId, x, y)); 
                            prest -= 23;
                            break;
                        //unit ability with target position and target object
                        case 0x12:
                            flag = reader.ReadInt16();
                            itemId = reader.ReadUInt32();
                            //unknownA, unknownB
                            reader.ReadInt64();
                            x = reader.ReadSingle();
                            y = reader.ReadSingle();
                            objectId1 = reader.ReadInt32();
                            objectId2 = reader.ReadInt32();                            
                            
                            actionList.Add(new Action(time, player, actionId, itemId, x, y, objectId1, objectId2)); 
                            prest -= 31;
                            break;
                        //unit ability with target position, target object, and target item (give item action)
                        case 0x13:
                            flag = reader.ReadInt16();
                            itemId = reader.ReadUInt32();
                            //unknownA, unknownB
                            reader.ReadInt64();
                            x = reader.ReadSingle();
                            y = reader.ReadSingle();
                            objectId1 = reader.ReadInt32();
                            objectId2 = reader.ReadInt32();
                            itemId1 = reader.ReadUInt32();
                            itemId2 = reader.ReadUInt32();

                            actionList.Add(new Action(time, player, actionId, itemId, x, y, objectId1, objectId2, itemId1, itemId2)); 

                            prest -= 39;
                            break;
                        //unit ability with two target positions and two item IDs
                        case 0x14:
                            flag = reader.ReadInt16();
                            itemId = reader.ReadUInt32();
                            //unknownA, unknownB
                            reader.ReadInt64();
                            x = reader.ReadSingle();
                            y = reader.ReadSingle();
                            itemId1 = reader.ReadUInt32();
                            reader.ReadBytes(9);
                            x2 = reader.ReadSingle();
                            y2 = reader.ReadSingle();
                            
                            actionList.Add(new Action(time, player, actionId, itemId, x, y, itemId1, x2, y2)); 
                            prest -= 44;
                            break;
                        //change selection
                        case 0x16:
                            byte selectMode = reader.ReadByte();
                            unitCount = reader.ReadInt16();

                            objects = new List<object>();
                            objects.Add(selectMode);
                            
                            //object ids                            
                            for (int i = 0; i < unitCount; i++)
                            {
                                objectId1 = reader.ReadInt32();
                                objectId2 = reader.ReadInt32();

                                objects.Add(objectId1);                               
                            }

                            actionList.Add(new Action(time, player, actionId, objects.ToArray())); 

                            prest -= (short)(unitCount * 8 + 4);
                            break;
                        //create group
                        case 0x17:
                            groupNo = reader.ReadByte();
                            unitCount = reader.ReadInt16();

                            //unit ids
                            //reader.ReadBytes(unitCount * 8);
                            objects = new List<object>();
                            objects.Add(groupNo);

                            for (int i = 0; i < unitCount; i++)
                            {
                                objectId1 = reader.ReadInt32();
                                objectId2 = reader.ReadInt32();

                                objects.Add(objectId1);                                
                            }

                            actionList.Add(new Action(time, player, actionId, objects.ToArray())); 
                                                        
                            prest -= (short)(unitCount * 8 + 4);
                            break;
                        //select group
                        case 0x18:
                            groupNo = reader.ReadByte();
                            //unknown
                            reader.ReadByte();

                            actionList.Add(new Action(time, player, actionId, groupNo));

                            prest -= 3;
                            break;
                        //select sub group
                        case 0x19:
                            //itemId, objectId1, objectId2
                            itemId = reader.ReadUInt32();
                            objectId1 = reader.ReadInt32();
                            reader.ReadInt32();

                            string codeID = DHJassInt.int2id(itemId);

                            List<KeyValuePair<int, string>> list;
                            if (!dcObjectCache.TryGetValue(objectId1, out list))
                            {
                                list = new List<KeyValuePair<int, string>>();
                                list.Add(new KeyValuePair<int, string>(time, codeID));

                                dcObjectCache.Add(objectId1, list);
                            }
                            else
                                // if last value does not match (object value has changed in the game)
                                if (list[list.Count - 1].Value != codeID)                 
                                    // update the list
                                    list.Add(new KeyValuePair<int, string>(time, codeID));

                            actionList.Add(new Action(time, player, actionId, codeID, objectId1));
                                                        
                            prest -= 13;
                            break;
                        //pre select sub group
                        case 0x1A:
                            actionList.Add(new Action(time, player, actionId));
                            prest--;
                            break;
                        //unknown
                        case 0x1B:
                            //unknown, objectid1, objectid2
                            reader.ReadByte();
                            reader.ReadInt64();
                            actionList.Add(new Action(time, player, actionId));
                            prest -= 10;
                            break;
                        //select ground item
                        case 0x1C:
                            //unknown, objectid1, objectid2
                            reader.ReadByte();
                            reader.ReadInt64();

                            actionList.Add(new Action(time, player, actionId));
                            prest -= 10;
                            break;
                        //cancel hero revival
                        case 0x1D:
                            reader.ReadInt64();
                            actionList.Add(new Action(time, player, actionId));
                            prest -= 9;
                            break;
                        //remove unit from order queue
                        case 0x1E:
                            slotNo = reader.ReadByte();
                            itemId = reader.ReadUInt32();
                                                        
                            actionList.Add(new Action(time, player, actionId, itemId));
                            prest -= 6;
                            break;
                        //unknown
                        case 0x21:
                            reader.ReadInt64();
                            prest -= 9;
                            actionList.Add(new Action(time, player, actionId));
                            break;
                        //cheats
                        case 0x20:
                        case 0x22:
                        case 0x23:
                        case 0x24:
                        case 0x25:
                        case 0x26:
                        case 0x29:
                        case 0x2A:
                        case 0x2B:
                        case 0x2C:
                        case 0x2F:
                        case 0x30:
                        case 0x31:
                        case 0x32:
                            prest--;
                            break;
                        //cheats
                        case 0x27:
                        case 0x28:
                        case 0x2D:
                            reader.ReadByte();
                            reader.ReadInt32();
                            prest -= 6;
                            break;
                        //cheats
                        case 0x2E:
                            reader.ReadInt32();
                            prest -= 5;
                            break;
                        //change ally option
                        case 0x50:
                            slotNo = reader.ReadByte();
                            int flags = reader.ReadInt32();
                            prest -= 6;                            

                            actionList.Add(new Action(time, player, actionId, slotNo, flags));
                            break;
                        //transfer resource
                        case 0x51:
                            slotNo = reader.ReadByte();
                            int gold = reader.ReadInt32();
                            int lumber = reader.ReadInt32();
                            prest -= 10;

                            actionList.Add(new Action(time, player, actionId, slotNo, gold, lumber));
                            break;
                        //trigger chat
                        case 0x60:
                            //unknownA, unknownB                            
                            //byte[] array = reader.ReadBytes(8);
                            reader.ReadInt64();
                            key = ParserUtility.ReadString(reader, out len);

                            actionList.Add(new Action(time, player, actionId, key));

                            prest -= (short)(9 + len);
                            break;
                        //esc pressed
                        case 0x61:
                            actionList.Add(new Action(time, player, actionId));
                            prest--;
                            break;
                        //Scenario Trigger
                        case 0x62:
                            //unknownABC
                            reader.ReadInt32();
                            reader.ReadInt64();

                            prest -= 13;

                            actionList.Add(new Action(time, player, actionId));
                            break;
                        //begin choose hero skill
                        case 0x66:
                            actionList.Add(new Action(time, player, actionId));
                            prest--;
                            break;
                        //begin choose building
                        case 0x67:
                            actionList.Add(new Action(time, player, actionId));
                            prest--;
                            break;
                        //ping mini map
                        case 0x68:
                            //x, y
                            reader.ReadInt64();
                            //unknown
                            reader.ReadInt32();

                            prest -= 13;

                            actionList.Add(new Action(time, player, actionId));
                            break;
                        //continue game
                        case 0x69:
                        case 0x6A:
                            reader.ReadInt64();
                            reader.ReadInt64();
                            prest -= 17;

                            actionList.Add(new Action(time, player, actionId));
                            break;
                        // SyncStoredInteger actions
                        case 0x6B:
                            gamecache = ParserUtility.ReadString(reader);
                            missonKey = ParserUtility.ReadString(reader);
                            key = ParserUtility.ReadString(reader);
                            value = reader.ReadInt32();
                            //Console.WriteLine("Sync: [" + gamecache + "][" + missonKey + "][" + key + "]=" + value);
                            prest -= (short)((gamecache.Length + 1) + (missonKey.Length + 1) + (key.Length + 1) + 4 + 1);

                            actionList.Add(new Action(time, player, actionId, gamecache, missonKey, key, value));
                            break;
                        case 0x6C:
                            gamecache = ParserUtility.ReadString(reader);
                            missonKey = ParserUtility.ReadString(reader);
                            key = ParserUtility.ReadString(reader);
                            value = reader.ReadInt32();
                            prest -= (short)((gamecache.Length + 1) + (missonKey.Length + 1) + (key.Length + 1) + 4 + 1);

                            actionList.Add(new Action(time, player, actionId, gamecache, missonKey, key, value));
                            break;
                        case 0x70:
                            gamecache = ParserUtility.ReadString(reader);
                            missonKey = ParserUtility.ReadString(reader);
                            key = ParserUtility.ReadString(reader);
                            prest -= (short)((gamecache.Length + 1) + (missonKey.Length + 1) + (key.Length + 1) + 1);

                            actionList.Add(new Action(time, player, actionId, gamecache, missonKey, key));
                            break;
                        case 0x75:
                            reader.ReadByte();
                            prest -= 2;
                            actionList.Add(new Action(time, player, actionId));
                            break;
                        default:
                            if (actionId == 0)
                            {
                                prest--;
                                byte[] bskipped = reader.ReadBytes(prest);
                                prest = 0;                                
                            }
                            else
                                throw new W3gParserException("Unknown ActionID: " + actionId + " (0x" + actionId.ToString("X") + ")");
                            break;
                    }                    
                    #endregion
                }
                rest -= playerBlockRest;
            }
        }

        private void actionAllB_Click(object sender, EventArgs e)
        {
            UICheckedListBox.SetItemsChecked(actionsCLB, true);
        }

        private void actionNoneB_Click(object sender, EventArgs e)
        {
            UICheckedListBox.SetItemsChecked(actionsCLB, false);
        }

        private void playerAllB_Click(object sender, EventArgs e)
        {
            UICheckedListBox.SetItemsChecked(sentinelCLB, true);
            UICheckedListBox.SetItemsChecked(scourgeCLB, true);
            UICheckedListBox.SetItemsChecked(observersCLB, true);
        }

        private void playerNoneB_Click(object sender, EventArgs e)
        {
            UICheckedListBox.SetItemsChecked(sentinelCLB, false);
            UICheckedListBox.SetItemsChecked(scourgeCLB, false);
            UICheckedListBox.SetItemsChecked(observersCLB, false);
        }

        private void timeTrackBar_ValueChanged(object sender, EventArgs e)
        {
            if (!timeTextBox.Focused)
                timeTextBox.Text = SecondsToTimeString(timeTrackBar.Value);
        }

        string MillisecondsToTimeString(int totalMilliseconds)
        {
            int totalSeconds = totalMilliseconds / 1000;

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;            
            int milliseconds = totalMilliseconds % 1000;

            string result = minutes.ToString("00", NumberFormatInfo.InvariantInfo) + ":" + seconds.ToString("00", NumberFormatInfo.InvariantInfo) + ":" + milliseconds.ToString("000", NumberFormatInfo.InvariantInfo);

            return result;
        }

        string SecondsToTimeString(int seconds)
        {
            int minutes = (seconds / 60);
            seconds = (seconds % 60);

            string result = minutes.ToString("00", NumberFormatInfo.InvariantInfo) + ":" + seconds.ToString("00", NumberFormatInfo.InvariantInfo);

            return result;
        }

        void ParseDesiredTime()
        {
            int desiredPosition = 0;

            string[] parts = timeTextBox.Text.Split(':');

            int numberOfParts = Math.Min(parts.Length, 2); // no more than 2 parts can be accepted (ex: "23:40")                       
            int multiplier = 1;

            for (int i = numberOfParts - 1; i >= 0; i--)
            {
                int value;
                int.TryParse(parts[i], out value);

                if (i == 0)
                    value = Math.Min(59, value);

                value *= multiplier;

                multiplier *= 60;

                desiredPosition += value;
            }

            try
            {
                timeTrackBar.Value = Math.Max(timeTrackBar.Minimum, Math.Min(timeTrackBar.Maximum, desiredPosition));
            }
            catch { }
        }

        private void timeTextBox_TextChanged(object sender, EventArgs e)
        {
            ParseDesiredTime();
        }

        private void timeTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) // ENTER key
            {
                e.Handled = true;
                ParseDesiredTime();

                showB.PerformClick();
            }
        }

        private void backB_Click(object sender, EventArgs e)
        {            
            try
            {
                timeTrackBar.Value = Math.Max(timeTrackBar.Minimum, Math.Min(timeTrackBar.Maximum, timeTrackBar.Value - 60));
            }
            catch { }
        }

        private void forwardB_Click(object sender, EventArgs e)
        {
            try
            {
                timeTrackBar.Value = Math.Max(timeTrackBar.Minimum, Math.Min(timeTrackBar.Maximum, timeTrackBar.Value + 60));
            }
            catch { }
        }

        private void showB_Click(object sender, EventArgs e)
        {            
            DHTIMER.StartNewCount();            

            int timeFrame = TimeFrame;

            int from = timeTrackBar.Value * 1000;
            int to = (timeFrame == -1) ? timeTrackBar.Maximum * 1000 : (from + timeFrame * 1000);

            List<int> allowedActions = new List<int>(actionsCLB.CheckedItems.Count);

            if (actionsCLB.GetItemChecked(0)) allowedActions.Add(0);
            if (actionsCLB.GetItemChecked(1)) allowedActions.Add(0xFF);

            foreach (string item in actionsCLB.CheckedItems)
                if (item.StartsWith("0x"))
                {
                    int actionId = Convert.ToInt32(item.Substring(0, 4), 16);
                    allowedActions.Add(actionId);
                }

            List<int> allowedPlayers = new List<int>(15);
            foreach (Player player in sentinelCLB.CheckedItems)
                allowedPlayers.Add(player.Id);
            foreach (Player player in scourgeCLB.CheckedItems)
                allowedPlayers.Add(player.Id);
            foreach (Player player in observersCLB.CheckedItems)
                allowedPlayers.Add(player.Id);
            
            dcPlayerHero.Clear();
            Stopwatch sw = new Stopwatch();

            actionsDataGridView.SuspendLayout();
            actionsBindingSource.DataSource = null;

            filteredActionList.Clear();

            foreach (Action action in actionList)
                if (action.time < from)
                    continue;
                else
                    if (action.time > to)
                        break;
                    else
                        if (allowedPlayers.Contains(action.player.Id))
                            if (allowedActions.Contains(action.actionId))
                            {
                                filteredActionList.Add(action);   
                            }           

            actionsBindingSource.DataSource = filteredActionList;            
            actionsDataGridView.ResumeLayout();                

            Console.WriteLine("TimeSlot: " + sw.ElapsedMilliseconds);
            DHTIMER.PrintEndCount("Show TimeSlot");            
        }

        Color GetActionColor(byte actionId)
        {
            switch (actionId)
            {
                    // chat
                case 0:
                    return Color.DimGray;

                case 0x10:
                case 0x11:
                case 0x12:                
                case 0x13:
                case 0x14:
                    return Color.DarkSlateGray;

                    // change selection
                case 0x16:
                    // select group hotkey
                case 0x18:
                    return Color.SeaGreen;

                    // assign group hotkey
                case 0x17:
                    return Color.DarkGoldenrod;

                    // pre sub-selection
                case 0x1A:
                    return Color.Brown;

                    // select subgroup
                case 0x19:
                    //return Color.DarkGreen;
                    return Color.SaddleBrown;

                case 0x6B:
                    return Color.RoyalBlue;

                    // darkslategray
            }

            return Color.Black;
        }

        Color GetActionBackColor(byte actionId)
        {
            switch (actionId)
            {
                case 0:
                    return Color.White;
            }

            return actionsDataGridView.BackColor;
        }

        string GetDataForAction(Action action)
        {
            string data = string.Empty;
            byte bValue;
            string sValue;
            int iValue;
            uint uValue;            

            switch (action.actionId)
            {
                    // unit/building ability without target
                case 0x10:
                    data += GetOrderItemString((uint)action.values[0]);
                    break;

                    // unit/building ability
                case 0x11:
                    data += GetOrderItemString((uint)action.values[0]);
                    data += " ";
                    data += action.values[1].ToString() + "[X] ";
                    data += action.values[2].ToString() + "[Y] ";
                    break;

                    // unit/building ability
                case 0x12:                    
                    data += GetOrderItemString((uint)action.values[0]);
                    data += " ";

                    data += action.values[1].ToString() + "[X] ";
                    data += action.values[2].ToString() + "[Y] ";

                    iValue = (int)action.values[3];
                    data += "0x" + iValue.ToString("X");
                    
                    data += GetTargetName(action.time, iValue);
                    break;

                    // give item to unit / drop item on ground 
                case 0x13:
                    data += GetOrderItemString((uint)action.values[0]);
                    data += " ";
                    data += action.values[1].ToString() + "[X] ";
                    data += action.values[2].ToString() + "[Y] ";

                    iValue = (int)action.values[3];
                    data += "0x" + iValue.ToString("X");

                    data += GetTargetName(action.time, iValue);
                    data += " ";

                    data += "0x" + Convert.ToInt32(action.values[5]).ToString("X");
                    data += " ";
                    data += "0x" + Convert.ToInt32(action.values[6]).ToString("X");
                    break;

                    //unit ability with two target positions and two item IDs
                case 0x14:
                    data += GetOrderItemString((uint)action.values[0]);
                    data += " ";
                    data += action.values[1].ToString() + "[X] ";
                    data += action.values[2].ToString() + "[Y] ";
                    data += " ";
                    data += GetOrderItemString((uint)action.values[3]);
                    data += " ";
                    data += action.values[4].ToString() + "[X] ";
                    data += action.values[5].ToString() + "[Y] ";
                    break;

                    // change selection
                case 0x16:
                    bValue = (byte)action.values[0];
                    data += "0x0"+bValue.ToString("X");
                    data += ((byte)action.values[0] == 1) ? "[SELECT]" : "[DESELECT]";
                    data += "  ";
                    for (int i = 1; i < action.values.Length; i++)
                    {
                        iValue = (int)action.values[i];
                        data += "0x" + iValue.ToString("X") + "[" + GetUnitName(GetObjectName(action.time, iValue)) + "] ";
                    }
                    break;

                    // assign group hotkey
                case 0x17:
                    bValue = (byte)action.values[0];
                    data += "0x0" + bValue.ToString("X"); bValue++;
                    data += (bValue == 10) ? "[Group0]" : ("[Group"+bValue+"]");
                    data += "  ";
                    for (int i = 1; i < action.values.Length; i++)
                    {
                        iValue = (int)action.values[i];
                        data += "0x" + iValue.ToString("X") + "[" + GetUnitName(GetObjectName(action.time, iValue)) + "] ";
                    }
                    break;

                // select group hotkey
                case 0x18:
                    bValue = (byte)action.values[0];
                    data += "0x0" + bValue.ToString("X"); bValue++;
                    data += (bValue == 10) ? "[Group0]" : ("[Group" + bValue + "]");                    
                    break;

                    // select subgroup
                case 0x19:
                    sValue = action.values[0] as string;
                    iValue = (int)action.values[1];
                    data += sValue + "[" + GetUnitName(sValue) + "]";
                    data += "  ";
                    data += "0x" + iValue.ToString("X");
                    break;

                default:                    
                    foreach (object obj in action.values)
                        if (obj != null)
                        {
                            if (obj is int)
                            {
                                iValue = (int)obj;
                                sValue = GetNameByCodeId(DHJassInt.int2id(iValue));
                                if (!string.IsNullOrEmpty(sValue))
                                    data += DHJassInt.int2id(iValue) + "[" + sValue + "]";
                                else
                                {
                                    data += "0x" + iValue.ToString("X");

                                    string name = GetUnitName(GetObjectName(action.time, iValue));
                                    if (!string.IsNullOrEmpty(name))
                                        data += "[" + name + "]";
                                }

                                data += " ";
                            }
                            else if (obj is uint)
                            {
                                uValue = (uint)obj;
                                sValue = GetNameByCodeId(DHJassInt.int2id(uValue));
                                if (!string.IsNullOrEmpty(sValue))
                                    data += DHJassInt.int2id(uValue) + "[" + sValue + "]";
                                else
                                {
                                    data += "0x" + uValue.ToString("X");
                                    sValue = GetUnitName(GetObjectName(action.time, (int)uValue));
                                    if (!string.IsNullOrEmpty(sValue))
                                        data += "[" + sValue + "]";
                                }

                                data += " ";
                            }
                            else
                                data += obj.ToString() + " ";
                        }
                    break;
            }

            return data;
        }

        string GetOrderItemString(uint itemId)
        {
            string result = string.Empty;

            // if this is orderID (not an objectID)
            if ((itemId >> 16) == 0x000D)
            {
                result += "0x" + itemId.ToString("X");

                // if it's a rightclick
                if (itemId == 0x000D0003)
                    result += "[rightclick]";
                else
                    if (Enum.IsDefined(typeof(OrderID), itemId))
                        result += "[" + Enum.GetName(typeof(OrderID), itemId) + "]";
            }
            else
            {
                string sValue = DHJassInt.int2id(itemId);
                result += sValue;

                sValue = GetNameByCodeId(sValue);
                result += (sValue != "") ? "[" + sValue + "]" : sValue;
            }

            return result;
        }

        string GetNameByCodeId(string codeId)
        {
            string name;
            name = replay.MapCache.hpcUnitProfiles.GetStringValue(codeId, "Name").Trim('"');

            if (name == "")
                name = replay.MapCache.hpcAbilityData.GetStringValue(codeId, "Name").Trim('"');

            if (name == "")
                name = replay.MapCache.hpcItemProfiles.GetStringValue(codeId, "Name").Trim('"');

            if (name == "")
                switch (codeId.ToUpper())
                {
                    case "NTTW":
                    case "ATTR":
                        return "Tree";
                }

            return DHSTRINGS.GetUntaggedString(name);
        }

        string GetUnitName(string unitId)
        {
            return replay.MapCache.hpcUnitProfiles.GetStringValue(unitId, "Name").Trim('"');
        }

        string GetObjectName(int time, int objectId)
        {
            List<KeyValuePair<int, string>> list;
            if (dcObjectCache.TryGetValue(objectId, out list))
            {
                string currentName = null;
                foreach (KeyValuePair<int, string> kvp in list)
                    if (kvp.Key < time)
                        currentName = kvp.Value;
                    else
                    {
                        if (currentName == null)
                            currentName = kvp.Value;

                        break;
                    }

                return currentName;
            }

            return "";
        }

        string GetTargetName(int time, int targetId)
        {
            if (targetId == -1)
                return "[GROUND]";
            else
            {
                string name = GetUnitName(GetObjectName(time, targetId));
                return (name != "") ? "[" + name + "]" : name;
            }
        }

        /*private void actionsLV_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.C:
                    if (e.Control)
                        CopyListViewItemsToClipboard(actionsLV, actionsLV.SelectedItems);
                    break;

                case Keys.A:
                    if (e.Control)
                    {
                        DHTIMER.StartNewCount();

                        isSelecting = true;                        
                        actionsLV.SuspendLayout();
                        actionsLV.BeginUpdate();
                        actionsLV.SelectedIndices.Clear();                        
                        for (int i = 0; i < filteredActionList.Count; i++)
                            actionsLV.Items[i].Selected = true;// SelectedIndices.Add(i);
                        actionsLV.EndUpdate();
                        actionsLV.ResumeLayout();                        
                        isSelecting = false;

                        DHTIMER.PrintEndCount("SelectAll");
                    }
                    break;
            }
        }*/

        public void CopyListViewItemsToClipboard(ListView listView, System.Collections.IList items)
        {
            DHTIMER.StartNewCount();

            StringBuilder buffer = new StringBuilder();

            for (int i = 0; i < listView.Columns.Count; i++)
            {
                buffer.Append(listView.Columns[i].Text);
                buffer.Append("\t");
            }

            buffer.Append(Environment.NewLine);

            for (int i = 0; i < listView.SelectedIndices.Count; i++)
            {
                int index = listView.SelectedIndices[i];

                buffer.Append(getItemClipboardString(index));
                buffer.Append(Environment.NewLine);
            }
         
            Clipboard.SetText(buffer.ToString());

            DHTIMER.PrintEndCount("CopyListViewItemsToClipboard");
        } 

        public int TimeFrame
        {
            get
            {
                foreach (Control c in controlPanel.Controls)
                    if (c is RadioButton && (c as RadioButton).Checked)
                        return Convert.ToInt32(c.Tag);

                return -1;
            }
        }

        public struct Action
        {
            public int time { get; set; }
            public Player player { get; set; }
            public byte actionId { get; set; }
            public object[] values { get; set; }

            public Action(int time, Player player, byte actionId, params object[] values) : this()
            {
                this.time = time;
                this.player = player;
                this.actionId = actionId;
                this.values = values;
            }
        }
        
        string getItemClipboardString(int index)
        {
            string output = "";

            Action action = filteredActionList[index];

            // time            
            output += SecondsToTimeString(action.time / 1000);
            output += "\t";            
            
            output += action.player.Name;
            output += "\t"; 
                        
            string heroClass;
            if (!dcPlayerHero.TryGetValue(action.player.Id, out heroClass))
            {
                heroClass = action.player.GetMostUsedHeroClass();
                dcPlayerHero.Add(action.player.Id, heroClass);
            }
            output += heroClass;
            output += "\t";
            
            switch (action.actionId)
            {
                // chat
                case 0:
                    if (action.values[1] != null)
                    {

                        output += "Private";
                        output += "\t";
                        output += (action.values[1] as Player).Name;
                        output += "\t";
                    }
                    else
                    {
                        output += action.values[0].ToString();
                        output += "\t";
                        output += "";
                        output += "\t";                        
                    }

                    output += action.values[2].ToString();
                    output += "\t";                    
                    break;

                // leave game
                case 0xFF:
                    output += "Leave";
                    output += "\t";                       

                    output += "Leave Game";
                    output += "\t";                       

                    output += action.values[0].ToString();
                    output += "\t";
                    break;

                default:
                    output += "0x" + action.actionId.ToString("X");
                    output += "\t"; 

                    string description; dcActionDescriptions.TryGetValue(action.actionId, out description);
                    output += description;
                    output += "\t"; 

                    output += GetDataForAction(action);
                    output += "\t"; 
                    break;
            }

            return output;
        }

        private void actionsDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            DataGridViewRow row = actionsDataGridView.Rows[e.RowIndex];
            Action action = (Action)row.DataBoundItem;

            if (e.ColumnIndex == timeDataGridViewTextBoxColumn.DisplayIndex)
            {
                e.Value = MillisecondsToTimeString(action.time);
                e.FormattingApplied = true;
                return;
            }

            if (e.ColumnIndex == playerDataGridViewTextBoxColumn.DisplayIndex)
            {
                e.Value = action.player.Name;
                e.CellStyle.ForeColor = ReplayBrowserForm.playerColorToColor(action.player.Color);
                e.FormattingApplied = true;
                return;
            }

            if (e.ColumnIndex == heroDataGridViewTextBoxColumn.DisplayIndex)
            {
                string heroClass;
                if (!dcPlayerHero.TryGetValue(action.player.Id, out heroClass))
                {
                    heroClass = action.player.GetMostUsedHeroClass();
                    dcPlayerHero.Add(action.player.Id, heroClass);
                }
                e.Value = heroClass;
                e.CellStyle.ForeColor = ReplayBrowserForm.playerColorToColor(action.player.Color);
                e.FormattingApplied = true;
                return;
            }

            if (e.ColumnIndex == actionDataGridViewTextBoxColumn.DisplayIndex)
            {
                switch (action.actionId)
                {
                    // chat
                    case 0:
                        if (action.values[1] != null)                        
                            e.Value = "Private";                                                    
                        else                        
                            e.Value = action.values[0].ToString();                            
                        
                        e.CellStyle.ForeColor = GetActionColor(action.actionId);                        
                        break;

                    // leave game
                    case 0xFF:
                        e.Value = "Leave";
                        break;

                    default:
                        e.Value = "0x" + action.actionId.ToString("X");                        
                        break;
                }

                e.FormattingApplied = true;
                return;
            }

            if (e.ColumnIndex == descriptionDataGridViewTextBoxColumn.DisplayIndex)
            {
                switch (action.actionId)
                {
                    // chat
                    case 0:
                        if (action.values[1] != null)
                        {                            
                            e.Value = (action.values[1] as Player).Name;
                            e.CellStyle.ForeColor = ReplayBrowserForm.playerColorToColor((action.values[1] as Player).Color);
                        }
                        else                                                    
                            e.Value = "";                        
                        break;

                    // leave game
                    case 0xFF:
                        e.Value = "Leave Game";                        
                        break;

                    default:
                        string description; dcActionDescriptions.TryGetValue(action.actionId, out description);
                        e.Value = description;
                        e.CellStyle.ForeColor = GetActionColor(action.actionId);                        
                        break;
                }

                e.FormattingApplied = true;
                return;
            }

            if (e.ColumnIndex == dataDataGridViewTextBoxColumn.DisplayIndex)
            {
                switch (action.actionId)
                {
                    // chat
                    case 0:
                        e.Value = action.values[2].ToString();
                        e.CellStyle.ForeColor = GetActionColor(action.actionId);
                        break;

                    // leave game
                    case 0xFF:
                        e.Value = action.values[0].ToString();                        
                        break;

                    default:                 
                        e.Value = GetDataForAction(action);
                        e.CellStyle.ForeColor = GetActionColor(action.actionId);            
                        break;
                }

                e.FormattingApplied = true;
                return;
            }
        }
    }
}