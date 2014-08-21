﻿using Engine;
using Engine.EntitySystem;
using Engine.MapSystem;
using Engine.Utils;
using ProjectCommon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Timers;

namespace ProjectEntities
{
    public class DetonationObjectType : DynamicType
    {
        [FieldSerialize]
        UInt32 secondsToUse = 10;

        [DefaultValue(10)]
        public UInt32 SecondsToUse
        {
            get { return secondsToUse; }
            set { secondsToUse = value; }
        }

        [FieldSerialize]
        string detonationItem;

        

        public string DetonationItem
        {
            get { return detonationItem; }
            set { detonationItem = value; }
        }

        [FieldSerialize]
        string soundUseStart;

        [FieldSerialize]
        string soundUseDuring;

        [FieldSerialize]
        string soundUserEnd;

    }

    public class DetonationObject : Dynamic
    {
        DetonationObjectType _type = null; public new DetonationObjectType Type { get { return _type; } }
        Timer aTimer;
        private bool firstUse=true;
        private int timeToGo;

        enum NetworkMessages
        {
            StartUseToServer,
            EndUseToServer,
            UseableToClient,
            RemoveUseItemToClient,
        }

        public delegate void PreparedDelegate();

        [LogicSystemBrowsable(true)]
        public event PreparedDelegate Prepared;


        private UInt32 useStart;

        private UInt32 UseStart
        {
            get { return useStart; }
            set { useStart = value; }
        }

        bool useable = true;

        public bool Useable
        {
            get { return useable; }
            set
            {
                useable = value;
                if (!useable)
                    foreach (MapObjectAttachedObject obj in AttachedObjects)
                    {
                        MapObjectAttachedMesh mesh = obj as MapObjectAttachedMesh;
                        if (mesh != null && mesh.Alias == "dynamesh")
                        {
                            mesh.Visible = true;
                        }
                    }
                if (EntitySystemWorld.Instance.IsServer())
                    Server_SendUsable(useable);
            }
        }


        public void StartUse(Unit unit)
        {
            if (useable && HasItem(unit))
            {
                if (EntitySystemWorld.Instance.IsClientOnly())
                {
                    Client_SendStartUse();
                    timeToGo = (int)Type.SecondsToUse;
                    StatusMessageHandler.sendMessage("Bringe Dynamit an..");
                    StatusMessageHandler.sendMessage(".." + timeToGo--);
                    aTimer = new System.Timers.Timer(1000);
                    aTimer.Elapsed += usingMessage;
                    aTimer.AutoReset = true;
                    aTimer.Enabled = true;
                }
                    

            }
            else if (!HasItem(unit))
                StatusMessageHandler.sendMessage("Du brauchst Dynamit.");
        }

        private void usingMessage(object sender, ElapsedEventArgs e)
        {
            if(timeToGo>=0)
            {
                StatusMessageHandler.sendMessage(".." + timeToGo--);
            }
            else
            {
                aTimer.Enabled = false;
            }
        }

        public void EndUse(Unit unit)
        {
            if (useable && HasItem(unit))
            {
                if (EntitySystemWorld.Instance.IsClientOnly())
                {
                    Client_SendEndUse(unit);
                    aTimer.Enabled = false;
                    aTimer.Elapsed -= usingMessage;
                }
            }

            EngineConsole.Instance.Print(this.Name+": "+Useable);
        }

        public bool HasItem(Unit unit)
        {
            string useItem = "";
            if (unit.Inventar.useItem != null)
                useItem = unit.Inventar.useItem.Type.FullName.ToLower();
            return !String.IsNullOrEmpty(Type.DetonationItem) && useItem.Equals(Type.DetonationItem.ToLower());
        }

        private void Client_SendStartUse()
        {
            SendDataWriter writer = BeginNetworkMessage(typeof(DetonationObject),
                (ushort)NetworkMessages.StartUseToServer);
            EndNetworkMessage();
        }

        [NetworkReceive(NetworkDirections.ToServer, (ushort)NetworkMessages.StartUseToServer)]
        private void Server_ReceiveStartUse(RemoteEntityWorld sender, ReceiveDataReader reader)
        {
            if (!reader.Complete())
                return;

            UseStart = (UInt32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        private void Client_SendEndUse(Unit unit)
        {
            SendDataWriter writer = BeginNetworkMessage(typeof(DetonationObject),
               (ushort)NetworkMessages.EndUseToServer);

            writer.Write(unit.NetworkUIN);

            EndNetworkMessage();
        }

        [NetworkReceive(NetworkDirections.ToServer, (ushort)NetworkMessages.EndUseToServer)]
        private void Server_ReceiveEndUse(RemoteEntityWorld sender, ReceiveDataReader reader)
        {
            uint uin = reader.ReadUInt32();

            if (!reader.Complete())
                return;

            UInt32 now = (UInt32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            if ((int)(now - useStart - Type.SecondsToUse) >= 0)
            {
                Useable = false;

                if (Prepared != null)
                    Prepared();

                Server_SendRemoveUseItem(uin, sender);
            }
        }

        private void Server_SendUsable(bool useable)
        {
            SendDataWriter writer = BeginNetworkMessage(typeof(DetonationObject),
                   (ushort)NetworkMessages.UseableToClient);

            writer.Write(useable);

            EndNetworkMessage();
        }

        private void Server_SendRemoveUseItem(uint uin, RemoteEntityWorld target)
        {
            SendDataWriter writer = BeginNetworkMessage(target, typeof(DetonationObject), (ushort)NetworkMessages.RemoveUseItemToClient);

            writer.Write(uin);

            EndNetworkMessage();
        }

        [NetworkReceive(NetworkDirections.ToClient, (ushort)NetworkMessages.UseableToClient)]
        private void Client_ReceiveUsuable(RemoteEntityWorld sender, ReceiveDataReader reader)
        {
            bool useable = reader.ReadBoolean();

            if (!reader.Complete())
                return;

            Useable = useable;
        }

        [NetworkReceive(NetworkDirections.ToClient, (ushort)NetworkMessages.RemoveUseItemToClient)]
        private void Client_ReceiveRemoveUseItem(RemoteEntityWorld sender, ReceiveDataReader reader)
        {
            uint uin = reader.ReadUInt32();

            if (!reader.Complete())
                return;

            Unit unit = (Unit)Entities.Instance.GetByNetworkUIN(uin);

            unit.Inventar.removeItem(unit.Inventar.useItem);
        }
    }
}
