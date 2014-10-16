﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Network;
using Wonderland_Private_Server.Code.Interface;

namespace Wonderland_Private_Server.Code.Objects
{
    /// <summary>
    /// Manages all Guilds in game
    /// </summary>
    public class GuildSystem
    {
        readonly object mylock = new object();
        Dictionary<UInt16, Guild> GlobalGuild;

        ushort AvailableGuildID { get { ushort a = 1000; while (GlobalGuild.ContainsKey(a)) { a++; } return a; } }
        public GuildSystem()
        {
            GlobalGuild = new Dictionary<UInt16, Guild>();
        }
        public bool CreateNewGuild(ref Player src, string GuildName)
        {
            lock (mylock)
            {
                //Prechecks
                //if name is the same,if player is in a guild, is required lvl, has the funds

                if (GlobalGuild.Values.Count(c => c.GuildName == GuildName) > 0)//name exists
                {
                    return false;
                }
                else if (src.Level < 1)//20
                {
                    return false;
                }
                //else if (src.inGuild)
                //{

                //}
                else if (src.Gold < 1)//100000
                {

                }
                else
                {
                    try
                    {
                        //take money
                        src.TakeGold(20000);

                        Guild cg = new Guild();
                        cg.GuildName = GuildName;
                        cg.GuildID = AvailableGuildID;//this way ist will get the next available ID
                        cg.Leader = new GuildMember(src);
                        cg.DateCreator = DateTime.Today; // data create guild need fix
                        cg.IconGuil = 3402; //default insigna
                        cg.Rules = "";
                        
                        SendPacket s = new SendPacket();
                        s.PackArray(new byte[] { 39, 1, 0 }); // clean Tab guild
                        src.Send(s);

                        cg.AddMember(src,true);
                        
                        GlobalGuild.Add(cg.GuildID, cg); // add new guild here <--

                        onPlayerLogin(ref src,cg.GuildID);
                      
                    }
                    catch { return false; }
                    
                }
                return true;
            }
        }
        
        public void onPlayerLogin(ref Player src,ushort guildID)
        {

            if(GlobalGuild.ContainsKey(guildID))
            {                
                src.CurGuild = GlobalGuild[guildID];
                GlobalGuild[guildID].SendInfo(ref src); 
            }
        }
    }


    public class Guild
    {

        #region propety

        public UInt16 GuildID;        
        public string GuildName;
        public string Rules;
        public uint IconGuil;
        public DateTime DateCreator;

        public GuildMember Leader;
        Dictionary<int, MessageGuild> Message = new Dictionary<int, MessageGuild>();
        List<GuildMember> ViceOrg = new List<GuildMember>(4);
        List<GuildMember> Members = new List<GuildMember>(50);
        List<byte> ImgInsigne = new List<byte>();
        public int TotalMembers { get { return (4 + (Members.Count - TotalViceOrg)); } }
        public int TotalViceOrg { get { return (ViceOrg.Count(c => c.ID > 0)); } }


        public Dictionary<uint,GuildMember>MembersOnlinne 
        {
            get{
                Dictionary<uint,GuildMember> tmp = new Dictionary<uint,GuildMember>();
                foreach(var t in Members.Where(c=>c.isOnline).ToList())
                tmp.Add(t.ID,t);
                return tmp;
            }
        }
       

        public Guild()
        {

        }
        #endregion

        public void ChangePermissionMember(uint target,RecvPacket r)
        {
            if (Members.Count(c=>c.ID == target)> 0)
            {

                Members.Find(c => c.ID == target).can_Invite = Convert.ToBoolean(r.Data[8]);
                Members.Find(c => c.ID == target).can_Modify_Rights = Convert.ToBoolean(r.Data[10]);

                int b = 8;
                for (int a = 1; a < 6; a++)
                {
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 39, 24 });
                    s.Pack32(target);
                    s.Pack8(1);// fix
                    s.Pack8((byte)a); // 1,2,3,4,5
                    s.Pack8((byte)r.Data[b]); //8,10,12,14,16
                    BroadCastGuild(s, 0); // all receive                
                    cGlobal.WLO_World.BroadcastTo(s, directTo: target); // new vice receive 2x same packet

                    b += 2;
                }             
            }
        }
        public void HoldThePostOfViceOrgleader(uint target, byte type)
        {
            if (Members.Count(c => c.ID == target) > 0)
            {
                var tmp = Members.Find(c => c.ID == target);

                ViceOrg.Add(tmp);

                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 39, 12 });
                s.Pack32(target);
                BroadCastGuild(s, 0); // all receiv
            }
        }
        public void RemoveHoldThePostOfViceOrgleader(Player src, uint target)
        {
            if (Members.Count(c => c.ID == target) > 0)
            {
                var tmp = Members.Find(c => c.ID == target);

                if (tmp != null)
                {
                    ViceOrg.Remove(tmp);

                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 39, 13 });
                    s.Pack32(target);
                    BroadCastGuild(s, 0); // all receiv
                    s = new SendPacket();
                    s.PackArray(new byte[] { 39, 15, 0 });
                    s.Pack32(target);
                    src.Send(s); // holy leader receiv
                }
            }
        }
        public void AddNewMemberGuild(Player src, uint Actor)
        {
            if (Members.Count(c => c.ID == src.ID) > 0) return;
            else
            {
                // messagem new player add
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] {39,4 });
                s.Pack32(src.UserID);
                s.Pack8(1);
                src.Send(s);
                cGlobal.WLO_World.BroadcastTo(s, Actor);
                AddMember(src, false);
                SendInfo(ref src);

                s = new SendPacket();
                s.PackArray(new byte[] {39,62,1});                
                src.Send(s);

                GetGuilNickName(src); //Send 39,9             

                GetInsigneGuild(src); //Send 39,30                


                #region packet global Except Me
                Send39_8(src); // new member in guild cur members.
                Send39_60(src); // send me to cur members
                Send39_61(src); // Send my state

                #endregion
                s = new SendPacket();
                s.PackArray(new byte[] {24,5,1,1,0});                
                src.Send(s);
            }
        }
        
        public void AddMember(Player src,bool Master)
        {
            if (Members.Count(c=>c.ID == src.ID) > 0) return;            
            else
            {
                GuildMember mw = new GuildMember();
                mw.OnlineSrc = src;

                #region Restriciton member
                if (Master)
                {
                    mw.can_Modify_Badge = true;
                    mw.can_Invite = true;
                    mw.can_DisBand_Guild = true;
                    mw.can_Modify_Rights = true;
                    mw.can_Modify_Rules = true;

                }
                #endregion

                Members.Add(mw);
            }
        }

        public bool SendInfo(ref Player src)
        {
            Send39_2(src); // get all database members 
            //Send39_17(src);
            Send39_21_26_27(src); 
            GetMemberOnLinne(src); // get player logged
            GetMemberState(src); // onlinne or busy
            GetGuilNickName(src);           
            return true;
        }

        #region Packets Loggin
        void Send39_2(Player src) // here send database member ON and OFF
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 39, 2 });
            s.PackString(GuildName);//guild name
            s.Pack8((byte)TotalViceOrg); // total leaders 
            s.Pack8((byte)TotalMembers);// total player in guild +4 - number leaders

            #region LoadPLayers
            foreach (var pair in Members.ToList())
            {
                s.Pack32(pair.ID);
                s.PackString(pair.CharacterName);
                s.Pack8(pair.Level);
                s.Pack8((byte)pair.Job);
                s.PackBoolean(pair.Reborn);
                s.Pack8((byte)pair.Element);
                s.Pack8((byte)pair.Body);
                s.Pack8(pair.Head);
                s.Pack16(pair.HairColor);
                s.Pack16(pair.SkinColor);
                s.Pack16(pair.ClothingColor);
                s.Pack16(pair.EyeColor);
                s.PackString(pair.Nickname);
                s.Pack32(0);
                s.PackBoolean(pair.can_DisBand_Guild);
                s.PackBoolean(pair.can_Invite);
                s.PackBoolean(pair.can_Modify_Rules);
                s.PackBoolean(pair.can_Modify_Rights);
                s.PackBoolean(pair.can_Modify_Badge);
                s.Pack32(0);
                s.Pack8(0);
            }
            #endregion

            s.PackString(Rules); // rules! defalt = 0
            s.PackArray(new byte[] {000, 000, 000, 000, 000, 000, 000, 000, 157,
                115, 241, 148, 144, 109, 228, 064, 000 }); // 17 need fix here.data timer creator:??
            src.Send(s);
        }
        void Send39_17(Player src)
        {
            if (Members.Count(c => c.ID == src.ID) > 0)
            {
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 39, 17 });                
                src.Send(s);
            }

        }        
        void Send39_21_26_27(Player src)
        {
            if (Members.Count(c => c.ID == src.ID) > 0)
            {
                #region Packet 39,17 39,21  39,26 39, 27


                for (int a = 1; a < 8; a++)
                {
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 39, 21 });
                    s.Pack8((byte)a);
                    s.Pack32(0);
                    src.Send(s);
                }
                for (int a = 1; a < 5; a++)
                {
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 39, 26 });
                    s.Pack8((byte)a);
                    s.Pack32(0);
                    src.Send(s);
                }
                for (int a = 1; a < 8; a++)
                {
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 39, 27 });
                    s.Pack8((byte)a);
                    s.Pack32(0);
                    src.Send(s);
                }

                #endregion

            }
        }       
        //39,60 holy player Logged
        public void GetMemberOnLinne(Player src)
        {
            foreach (var pair in MembersOnlinne.ToList())
            {
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 39, 60 });
                s.Pack32(pair.Value.ID);
                s.PackString(pair.Value.CharacterName);
                s.Pack8(pair.Value.Level);
                s.Pack8((byte)pair.Value.Job);// job

                if (pair.Value.Reborn) s.Pack8(1);//reborn
                else s.Pack8(0);

                s.Pack8((byte)pair.Value.Element);//elemnt
                s.Pack8((byte)pair.Value.Body);// body
                s.Pack8((byte)pair.Value.Head);// head
                s.Pack16(pair.Value.HairColor);
                s.Pack16(pair.Value.SkinColor);
                s.Pack16(pair.Value.ClothingColor);
                s.Pack16(pair.Value.EyeColor);
                s.PackString(pair.Value.Nickname);
                src.Send(s);
            }
        }
        //39,61
        public void GetMemberState(Player src)
        {
           
            // 0 = online
            // 1 = busy
            foreach (var pair in MembersOnlinne.ToList())
            {
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 39, 61 });
                s.Pack32(pair.Value.ID);
                s.Pack8(pair.Value.Busy);                
                src.Send(s);
            }
        }
        //39,30
        void GetInsigneGuild(Player src)
        {            
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 39, 30,2 });
                s.PackArray(ImgInsigne.ToArray());
                src.Send(s);            
        }
        //39,9
        void GetGuilNickName(Player src)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 39, 9 });
            s.Pack32(src.UserID);
            s.Pack32(IconGuil); // UINT ICON GUILD
            s.PackString(GuildName); // name guild NICK SHOW IN MAP (acima do nick name)
            //src.Send(s);
            src.CurrentMap.Broadcast(s);            
        }

        #endregion


        void BroadCastGuild(SendPacket spk,uint exceptID)
        {
            foreach (var pair in MembersOnlinne)
            {
                if (pair.Value.ID != exceptID)
                {
                    cGlobal.WLO_World.BroadcastTo(spk, directTo: pair.Value.ID);
                }
            }            
        }

        #region Global Packet
        void Send39_8(Player src)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[]{39, 8});
            s.Pack32(src.UserID);
            BroadCastGuild(s, src.UserID);
        }
        //send state
        void Send39_61(Player src)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 39, 61 });
            s.Pack32(src.UserID);
            s.Pack8(0);// 0 = on  1 = busy  3 = offlinne
            BroadCastGuild(s, src.UserID);// send all except me
        }
        void Send39_60(Player src)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 39, 60 });
            s.Pack32(src.UserID);
            s.PackString(src.CharacterName);
            s.Pack8(src.Level);
            s.Pack8((byte)src.Job);// job

            if (src.Reborn) s.Pack8(1);//reborn
            else s.Pack8(0);

            s.Pack8((byte)src.Element);//elemnt
            s.Pack8((byte)src.Body);// body
            s.Pack8((byte)src.Head);// head
            s.Pack16(src.HairColor);
            s.Pack16(src.SkinColor);
            s.Pack16(src.ClothingColor);
            s.Pack16(src.EyeColor);
            s.PackString(src.Nickname);

            BroadCastGuild(s, src.UserID);

        }

        #endregion

        #region Methods Guild

        public void ChangInsigneGuild(ref Player src, RecvPacket r)
        {
            // need verify here if have permission member change guildinsigne

            ImgInsigne.AddRange(r.Data.Skip(2).Take(r.Data.Count - 2).ToArray());
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 39, 30, 1 });
            s.PackArray(ImgInsigne.ToArray());
            BroadCastGuild(s, 0);// all
        }

        public void Edit_Rule(string text)
        {
            // need verify here if have permission member change guildinsigne
            Rules = text;
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 39, 11 });
            s.PackNString(text);
            BroadCastGuild(s, 0);// all
        }
        public void Dismiss(uint target, uint actor)
        {    
            var tmp = Members.Find(c => c.ID == target);

            if(tmp!=null)
            {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 39,6});
            s.Pack32(target);
            BroadCastGuild(s, target);// All except target demiss

            s = new SendPacket();
            s.PackArray(new byte[] { 39,7, 0 });
            s.Pack32(target);
            cGlobal.WLO_World.BroadcastTo(s, directTo: actor); // actor 
            cGlobal.WLO_World.BroadcastTo(s, directTo: target); // target demiss
                            
            Members.Remove(tmp);
                // here need add CURMAP MAP RECEIVE 39,9
                // remove guild nickname player

            }
            
        }
        public void LeaveGuild(ref Player src)
        {
            uint id = src.UserID;
            var tmp = Members.Find(c => c.ID == id);

            SendPacket s = new SendPacket();
            s.PackArray(new byte[] {39,7,0 });
            s.Pack32(src.UserID);
            src.Send(s);

            s = new SendPacket();
            s.PackArray(new byte[] { 39, 6 });
            s.Pack32(src.UserID);
            BroadCastGuild(s, src.UserID);// all except id
          
            s = new SendPacket();
            s.PackArray(new byte[] { 39,9 });
            s.Pack32(src.UserID);
            s.PackArray(new byte[] {0,0,0,0,0 });
            src.CurrentMap.Broadcast(s); // all local map.

            src.CurGuild.GuildID = 0;
            Members.Remove(tmp);
        }
        
        #endregion

        public void GuilMail(uint actor, uint dst, string text)
        {

            SendPacket s = new SendPacket();
            s.PackArray(new byte[] {39,5});
            s.Pack32(actor);
            s.Pack16(62860);// data ??
            s.Pack16(20548); //data ??
            s.Pack16(30000);// data time ??
            s.Pack16(16612);// data time ??
            s.PackNString(text);
            // need create code to verify if player on
            //if off store in list, to send later...
            cGlobal.WLO_World.BroadcastTo(s, directTo: dst);
        }

        #region Message Guild

        public void AddMessage(Player src,RecvPacket r)
        {
            int count = r.Unpack8(2);
            int count2 = r.Unpack8(6 + count);
            byte[] data1 = r.Data.Skip(6).Take(count).ToArray();
            byte[] data2 = r.Data.Skip(6 + count+count2).Take(count2).ToArray();


            string Subject = System.Text.Encoding.Default.GetString(data1);
            string content = System.Text.Encoding.Default.GetString(data2);

            MessageGuild msg = new MessageGuild();
            msg.Sender = src.UserName;
            msg.Subject = Subject;
            msg.Content = content;
            msg.data1 = 20582;
            msg.data2 = 29938;
            msg.data3 = 20922;
            msg.data4 = 16612;
            msg.UserID = src.UserID;
            msg.unknow = 31; // time seconds ?
            Message.Add(Message.Count, msg);



            OpenTab(src);
        }

        public void OpenTab(Player src)
        {
            
            if (Message.Count > 0)
            {
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 82, 14});
                s.Pack32((uint)Message.Count);
                s.Pack8(1); // current tab
                src.Send(s);

                Send82_11(src);
            }
            else
            {
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 82, 13,0, 0, 0, 0, 1 });// null tab
                src.Send(s);
            }

        }
        void Send82_11(Player src)
        {

            //orde per current
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] { 82, 11 });

            foreach(var t in Message.Values)
            {            
            s.Pack32((UInt32)t.Sender.Length);
            s.PackNString(t.Sender);
            s.Pack32((UInt32)t.Subject.Length);
            s.PackNString(t.Subject);
            s.Pack16(t.data1);
            s.Pack16(t.data2);
            s.Pack16(t.data3);
            s.Pack16(t.data4);
            s.Pack32(t.UserID);
            s.Pack32((uint)t.unknow);// seconds ??           
            }
            src.Send(s);
        }
        // problem here, get message per data time.... >.>
        public void OpenMessage(Player src, RecvPacket r)
        {
            if (Message.Count > 0)
            {
                var t = Message[0];
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 82, 12 });
                s.Pack32((UInt32)t.Sender.Length);
                s.PackNString(t.Sender);
                s.Pack32((UInt32)t.Subject.Length);
                s.PackNString(t.Subject);
                s.Pack16(t.data1);
                s.Pack16(t.data2);
                s.Pack16(t.data3);
                s.Pack16(t.data4);
                s.Pack32((UInt32)t.Content.Length);
                s.Pack32(t.UserID);
                s.PackNString(t.Content);
                src.Send(s);
            }
        }

        public void OpenPainelWriteMessage(Player src)
        {
            SendPacket s = new SendPacket();
            s.PackArray(new byte[] {82,8,2 });
            src.Send(s);
        }

        #endregion
    }

}
