// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) FTW! Masters
// Keep the headers and the patterns adopted by the project. If you changed anything in the file just insert
// your name below, but don't remove the names of who worked here before.
// 
// This project is a fork from Comet, a Conquer Online Server Emulator created by Spirited, which can be
// found here: https://gitlab.com/spirited/comet
// 
// Comet - Comet.Game - MsgName.cs
// Description:
// 
// Creator: FELIPEVIEIRAVENDRAMI [FELIPE VIEIRA VENDRAMINI]
// 
// Developed by:
// Felipe Vieira Vendramini <felipevendramini@live.com>
// 
// Programming today is a race between software engineers striving to build bigger and better
// idiot-proof programs, and the Universe trying to produce bigger and better idiots.
// So far, the Universe is winning.
// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Comet.Game.Packets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Comet.Game.States;
    using Comet.Network.Packets;

    public sealed class MsgName : MsgBase<Client>
    {
        public MsgName()
        {
            Type = PacketType.MsgName;
        }

        public uint Identity { get; set; }
        public StringAction Action { get; set; }
        public List<string> Strings { get; set; } = new List<string>();

        public override void Decode(byte[] bytes)
        {
            var reader = new PacketReader(bytes);
            Length = reader.ReadUInt16();
            Type = (PacketType)reader.ReadUInt16();
            Identity = reader.ReadUInt32();
            Action = (StringAction)reader.ReadByte();
            Strings = reader.ReadStrings();
        }

        public override byte[] Encode()
        {
            var writer = new PacketWriter();
            writer.Write((ushort)Type);
            writer.Write(Identity);
            writer.Write((byte)Action);
            writer.Write(Strings ?? new List<string>());
            return writer.ToArray();
        }

        public override async Task ProcessAsync(Client client)
        {
            if (Action != StringAction.WhisperWindowInfo || Strings == null || Strings.Count == 0)
                return;

            var target = Kernel.Clients.Values.FirstOrDefault(x =>
                x.Character != null &&
                string.Equals(x.Character.Name, Strings[0]));

            if (target?.Character == null)
            {
                await client.SendAsync(this);
                return;
            }

            Identity = target.ID;
            Strings.Add(string.Format(
                "{0} {1} 0 # #None None 0 0",
                target.ID, target.Character.Level));

            await client.SendAsync(this);
        }
    }

    public enum StringAction : byte
    {
        WhisperWindowInfo = 26
    }
}
