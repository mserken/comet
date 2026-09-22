// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) FTW! Masters
// Keep the headers and the patterns adopted by the project. If you changed anything in the file just insert
// your name below, but don't remove the names of who worked here before.
//
// This project is a fork from Comet, a Conquer Online Server Emulator created by Spirited, which can be
// found here: https://gitlab.com/spirited/comet
//
// Comet - Comet.Game - MsgPlayer.cs
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
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Comet.Game.States;
    using Comet.Network.Packets;

    public sealed class MsgPlayer : MsgBase<Client>
    {
        public MsgPlayer(Character character)
        {
            Type = PacketType.MsgPlayer;
            Identity = character.CharacterID;
            Mesh = character.Mesh + (character.Avatar * 10000u);
            MapX = character.X;
            MapY = character.Y;
            Hairstyle = character.Hairstyle;
            Level = character.Level;
            Name = character.Name ?? string.Empty;

            Console.WriteLine(
                "[MsgPlayer] Constructed identity={0}, name={1}, mesh={2}, map=({3}, {4}), hairstyle={5}, level={6}",
                Identity, Name, Mesh, MapX, MapY, Hairstyle, Level);
        }

        public static async Task BroadcastAsync(Client client)
        {
            if (client.Character == null)
            {
                Console.WriteLine("[MsgPlayer] Broadcast skipped: client has no character.");
                return;
            }

            var players = Kernel.Clients.Values
                .Where(x => x != client && x.Character != null && x.Socket.Connected &&
                    x.Character.MapID == client.Character.MapID)
                .ToArray();

            Console.WriteLine(
                "[MsgPlayer] Broadcast source identity={0}, name={1}, map={2}, position=({3}, {4}), candidates={5}",
                client.ID, client.Character.Name, client.Character.MapID,
                client.Character.X, client.Character.Y, players.Length);

            foreach (var player in players)
            {
                Console.WriteLine(
                    "[MsgPlayer] Candidate identity={0}, name={1}, map={2}, connected={3}, position=({4}, {5})",
                    player.ID, player.Character.Name, player.Character.MapID, player.Socket.Connected,
                    player.Character.X, player.Character.Y);
            }

            foreach (var player in players)
            {
                await SendAsync(player, new MsgPlayer(client.Character),
                    "source to existing player");
            }

            foreach (var player in players)
            {
                await SendAsync(client, new MsgPlayer(player.Character),
                    "existing player to source");
            }
        }

        private static async Task SendAsync(Client recipient, MsgPlayer packet, string route)
        {
            Console.WriteLine(
                "[MsgPlayer] Sending route={0}, recipient={1}, packet identity={2}, name={3}",
                route, recipient.ID, packet.Identity, packet.Name);

            try
            {
                var encoded = packet.Encode();
                Console.WriteLine(
                    "[MsgPlayer] Encoded packet type={0}, identity={1}, length={2}",
                    packet.Type, packet.Identity, encoded.Length);
                await recipient.SendAsync(encoded);
                Console.WriteLine(
                    "[MsgPlayer] Sent packet identity={0} to recipient={1}",
                    packet.Identity, recipient.ID);
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "[MsgPlayer] Send failed identity={0} to recipient={1}: {2}",
                    packet.Identity, recipient.ID, exception);
            }
        }

        public uint Identity { get; set; }
        public uint Mesh { get; set; }

        public ushort MapX { get; set; }
        public ushort MapY { get; set; }
        public ushort Hairstyle { get; set; }
        public ushort Level { get; set; }

        public string Name { get; set; }

        /// <summary>
        ///     Encodes the packet structure defined by this message class into a byte packet
        ///     that can be sent to the client. Invoked automatically by the client's send
        ///     method. Encodes using byte ordering rules interoperable with the game client.
        /// </summary>
        /// <returns>Returns a byte packet of the encoded packet.</returns>
        public override byte[] Encode()
        {
            var writer = new PacketWriter();
            writer.Write((ushort) Type);
            writer.Write(Mesh); // 4
            writer.Write(Identity); // 8

            writer.Write(0u); // 12
            writer.Write(0u); // 16

            writer.Write((ushort)0);

            writer.Write(0ul); // 24
            writer.Write(0u); // 30
            writer.Write(0u); // 34
            writer.Write(0u); // 38
            writer.Write(0u); // 42
            writer.Write(0u); // 46
            writer.Write(0u); // 50
            writer.Write(0u); // 54
            writer.Write((ushort)0); // 58
            writer.Write((ushort)0); // 60
            writer.Write(Hairstyle); // 62
            writer.Write(MapX); // 64
            writer.Write(MapY); // 66
            writer.Write((byte)0); // 68
            writer.Write((byte)0); // 69
            writer.BaseStream.Seek(4, SeekOrigin.Current);
            writer.Write((byte)0); // 73
            writer.Write(Level); // 74
            writer.Write((byte)0); // 76
            writer.Write((byte)0); // 77
            writer.Write(0u); // 78
            writer.BaseStream.Seek(8, SeekOrigin.Current); // 82
            writer.Write(0u); // 91
            writer.Write(0u); // 95
            writer.Write((ushort)0); // 99
            writer.Write((ushort)0); // 101
            writer.Write((ushort)0); // 103
            writer.Write(0u); // 105
            writer.Write((byte)0); // 107
            writer.Write(0); // 108
            writer.Write(0u); // 112
            writer.Write((byte) 0); // 116
            writer.Write((ushort)0); // 117
            writer.BaseStream.Seek(10, SeekOrigin.Current); // 119
            writer.Write(0u); // 129
            writer.Write(0u); // 133
            writer.Write(0); // 137
            writer.Write(0u); // 141
            writer.BaseStream.Seek(8, SeekOrigin.Current); // 145
            writer.Write(new List<string> // 95
            {
                Name ?? string.Empty,
                string.Empty
            });

            return writer.ToArray();
        }
    }
}
