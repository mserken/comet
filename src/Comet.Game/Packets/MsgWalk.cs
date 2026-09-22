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
        private const int MinimumWalkMilliseconds = 100;
        private const int WalkWindowMilliseconds = 5000;
        private const int MaximumWalksPerWindow = 26;
        public const int ViewDistance = 18;

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
            if (client.Character == null)
                return;

            Identity = client.ID;
            Console.WriteLine("MsgWalk: {0} Direction: {1} Identity: {2} Mode: {3} Padding: {4}", client.ID, Direction, Identity, Mode, Padding);

            var now = DateTime.UtcNow;
            while (client.WalkHistory.Count > 0 &&
                (now - client.WalkHistory.Peek()).TotalMilliseconds > WalkWindowMilliseconds)
            {
                client.WalkHistory.Dequeue();
            }

            client.WalkHistory.Enqueue(now);
            var elapsed = (now - client.LastWalkAt).TotalMilliseconds;
            var tooSoon = client.LastWalkAt != DateTime.MinValue &&
                elapsed < MinimumWalkMilliseconds;
            var tooMany = client.WalkHistory.Count > MaximumWalksPerWindow;

            if (tooSoon || tooMany)
            {
                Console.WriteLine(
                    "[MsgWalk] Rejected identity={0}, tooSoon={1}, tooMany={2}, elapsed={3}ms, windowCount={4}",
                    client.ID, tooSoon, tooMany, elapsed, client.WalkHistory.Count);
                await SendCorrectionAsync(client);
                return;
            }

            client.LastWalkAt = now;
            client.LastWalkDirection = Direction;
            await client.SendAsync(this);

            var previousX = client.Character.X;
            var previousY = client.Character.Y;
            var nextX = previousX;
            var nextY = previousY;

            switch (Direction)
            {
                case 0: // Down-Left
                    nextY += 1;
                    break;
                case 1: // Left
                    nextX -= 1;
                    nextY += 1;
                    break;
                case 2: // Up-Left
                    nextX -= 1;
                    break;
                case 3: // Up
                    nextX -= 1;
                    nextY -= 1;
                    break;
                case 4: // Up-Right
                    nextY -= 1;
                    break;
                case 5: // Right
                    nextX += 1;
                    nextY -= 1;
                    break;
                case 6: // Down-Right
                    nextX += 1;
                    break;
                case 7: // Down
                    nextX += 1;
                    nextY += 1;
                    break;
                default:
                    Console.WriteLine("Invalid direction: {0}", Direction);
                    break;
            }

            if (nextX < 0 || nextX > ushort.MaxValue ||
                nextY < 0 || nextY > ushort.MaxValue)
            {
                Console.WriteLine(
                    "[MsgWalk] Rejected identity={0}: position ({1}, {2}) is outside coordinate bounds",
                    client.ID, nextX, nextY);
                await SendCorrectionAsync(client);
                return;
            }

            client.Character.X = (ushort)nextX;
            client.Character.Y = (ushort)nextY;
            Console.WriteLine("Character Position: X: {0} Y: {1}", client.Character.X, client.Character.Y);

            var players = Kernel.Clients.Values
                .Where(x => x != client && x.Character != null && x.Socket.Connected &&
                    x.Character.MapID == client.Character.MapID)
                .ToArray();

            Console.WriteLine(
                "[MsgWalk] Processing identity={0}, direction={1}, map={2}, old=({3}, {4}), new=({5}, {6}), candidates={7}",
                Identity, Direction, client.Character.MapID, previousX, previousY,
                client.Character.X, client.Character.Y, players.Length);

            foreach (var player in players)
            {
                var isInView = IsWithin(client.Character.X, client.Character.Y,
                    player.Character.X, player.Character.Y, ViewDistance);

                if (isInView)
                {
                    await player.SendAsync(new MsgAction
                    {
                        CharacterID = Identity,
                        Action = MsgAction.ActionType.CharacterDirection,
                        Direction = Direction
                    });
                    await player.SendAsync(this);
                    Console.WriteLine("[MsgWalk] Refreshed and sent movement identity={0} to recipient={1}", Identity, player.ID);
                }
                else
                {
                    await player.SendAsync(new MsgAction
                    {
                        CharacterID = Identity,
                        Action = MsgAction.ActionType.MapRemoveSpawn
                    });
                    await client.SendAsync(new MsgAction
                    {
                        CharacterID = player.ID,
                        Action = MsgAction.ActionType.MapRemoveSpawn
                    });
                    Console.WriteLine("[MsgWalk] Removed out-of-view identity={0} from recipient={1}", Identity, player.ID);
                }
            }
        }

        private static bool IsWithin(ushort x, ushort y, ushort otherX, ushort otherY, int distance)
        {
            return Math.Abs(x - otherX) <= distance && Math.Abs(y - otherY) <= distance;
        }

        private static Task SendCorrectionAsync(Client client)
        {
            return client.SendAsync(new MsgAction
            {
                CharacterID = client.ID,
                Action = MsgAction.ActionType.MapKickBack,
                Command = (uint)((client.Character.Y << 16) | client.Character.X),
                Direction = client.LastWalkDirection,
                X = client.Character.X,
                Y = client.Character.Y
            });
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