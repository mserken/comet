// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) FTW! Masters
// Keep the headers and the patterns adopted by the project. If you changed anything in the file just insert
// your name below, but don't remove the names of who worked here before.
// 
// This project is a fork from Comet, a Conquer Online Server Emulator created by Spirited, which can be
// found here: https://gitlab.com/spirited/comet
// 
// Comet - Comet.Game - MsgWalk.cs
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

#region References

using System;
using System.Linq;
using System.Threading.Tasks;
using Comet.Game.States;
using Comet.Network.Packets;

#endregion

namespace Comet.Game.Packets
{
    public sealed class MsgWalk : MsgBase<Client>
    {
        public MsgWalk()
        {
            Type = PacketType.MsgWalk;
        }

        public uint Identity { get; set; }
        public byte Direction { get; set; }
        public byte Mode { get; set; }
        public ushort Padding { get; set; }

        /// <summary>
        ///     Decodes a byte packet into the packet structure defined by this message class.
        ///     Should be invoked to structure data from the client for processing. Decoding
        ///     follows TQ Digital's byte ordering rules for an all-binary protocol.
        /// </summary>
        /// <param name="bytes">Bytes from the packet processor or client socket</param>
        public override void Decode(byte[] bytes)
        {
            var reader = new PacketReader(bytes);
            Length = reader.ReadUInt16();
            Type = (PacketType) reader.ReadUInt16();
            Direction = (byte) (reader.ReadUInt32() % 8);
            Identity = reader.ReadUInt32();
            Mode = reader.ReadByte();
            Padding = reader.ReadUInt16();
        }

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
            writer.Write((int) Direction);
            writer.Write(Identity);
            writer.Write(Mode);
            writer.Write(Padding);
            return writer.ToArray();
        }

        /// <summary>
        ///     Process can be invoked by a packet after decode has been called to structure
        ///     packet fields and properties. For the server implementations, this is called
        ///     in the packet handler after the message has been dequeued from the server's
        ///     <see cref="PacketProcessor" />.
        /// </summary>
        /// <param name="client">Client requesting packet processing</param>
        public override async Task ProcessAsync(Client client)
        {
            Identity = client.ID;
            await Task.WhenAll(client.SendAsync(this));
            Console.WriteLine("MsgWalk: {0} Direction: {1} Identity: {2} Mode: {3} Padding: {4}", client.ID, Direction, Identity, Mode, Padding);
            // update x and y coordinates of the character based on the direction of movement
            switch (Direction)
            {
                case 0: // Down-Left
                    client.Character.Y += 1;
                    break;
                case 1: // Left
                    client.Character.X -= 1;
                    client.Character.Y += 1;
                    break;
                case 2: // Up-Left
                    client.Character.X -= 1;
                    break;
                case 3: // Up
                    client.Character.X -= 1;
                    client.Character.Y -= 1;
                    break;
                case 4: // Up-Right
                    client.Character.Y -= 1;
                    break;
                case 5: // Right
                    client.Character.X += 1;
                    client.Character.Y -= 1;
                    break;
                case 6: // Down-Right
                    client.Character.X += 1;
                    break;
                case 7: // Down
                    client.Character.X += 1;
                    client.Character.Y += 1;
                    break;
                default:
                    Console.WriteLine("Invalid direction: {0}", Direction);
                    break;
            }
            Console.WriteLine("Character Position: X: {0} Y: {1}", client.Character.X, client.Character.Y);

            var recipients = Kernel.Clients.Values
                .Where(x => x != client && x.Character != null && x.Socket.Connected &&
                    x.Character.MapID == client.Character.MapID)
                .ToArray();

            Console.WriteLine(
                "[MsgWalk] Broadcasting identity={0}, direction={1}, mode={2}, map={3}, position=({4}, {5}), recipients={6}",
                Identity, Direction, Mode, client.Character.MapID,
                client.Character.X, client.Character.Y, recipients.Length);

            await Task.WhenAll(recipients.Select(async recipient =>
            {
                try
                {
                    await recipient.SendAsync(this);
                    Console.WriteLine(
                        "[MsgWalk] Sent movement identity={0} to recipient={1}",
                        Identity, recipient.ID);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        "[MsgWalk] Send failed identity={0} to recipient={1}: {2}",
                        Identity, recipient.ID, exception);
                }
            }));
        }
    }

    public enum RoleMoveMode
    {
        Walk = 0,

        // PathMove()
        Run,
        Shift,

        // to server only
        Jump,
        Trans,
        Chgmap,
        JumpMagicAttack,
        Collide,
        Synchro,

        // to server only
        Track,

        RunDir0 = 20,

        RunDir7 = 27
    }
}