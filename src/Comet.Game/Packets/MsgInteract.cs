namespace Comet.Game.Packets
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Comet.Game.States;
    using Comet.Network.Packets;

    public sealed class MsgInteract : MsgBase<Client>
    {
        public MsgInteract()
        {
            Type = PacketType.MsgInteract;
            Timestamp = Environment.TickCount;
        }

        public int Timestamp { get; set; }
        public uint SenderIdentity { get; set; }
        public uint TargetIdentity { get; set; }
        public ushort PosX { get; set; }
        public ushort PosY { get; set; }
        public MsgInteractType Action { get; set; }
        public int Data { get; set; }
        public int Command { get; set; }

        public override void Decode(byte[] bytes)
        {
            var reader = new PacketReader(bytes);
            Length = reader.ReadUInt16();
            Type = (PacketType)reader.ReadUInt16();
            Timestamp = reader.ReadInt32();
            SenderIdentity = reader.ReadUInt32();
            TargetIdentity = reader.ReadUInt32();
            PosX = reader.ReadUInt16();
            PosY = reader.ReadUInt16();
            Action = (MsgInteractType)reader.ReadUInt32();
            Data = reader.ReadInt32();
            Command = reader.ReadInt32();
        }

        public override byte[] Encode()
        {
            var writer = new PacketWriter();
            writer.Write((ushort)Type);
            writer.Write(Timestamp);
            writer.Write(SenderIdentity);
            writer.Write(TargetIdentity);
            writer.Write(PosX);
            writer.Write(PosY);
            writer.Write((uint)Action);
            writer.Write(Data);
            writer.Write(Command);
            return writer.ToArray();
        }

        public override async Task ProcessAsync(Client client)
        {
            if (client.Character == null)
                return;

            SenderIdentity = client.ID;
            PosX = client.Character.X;
            PosY = client.Character.Y;

            var isCombatAction = Action == MsgInteractType.Attack ||
                Action == MsgInteractType.Shoot ||
                Action == MsgInteractType.MagicAttack;
            var target = isCombatAction
                ? Kernel.Clients.Values.FirstOrDefault(x =>
                    x != client && x.Character != null && x.Socket.Connected &&
                    x.ID == TargetIdentity &&
                    x.Character.MapID == client.Character.MapID)
                : null;

            if (isCombatAction && (target == null || !IsWithin(PosX, PosY,
                target.Character.X, target.Character.Y, MsgWalk.ViewDistance)))
            {
                Console.WriteLine(
                    "[MsgInteract] Rejected action={0}, sender={1}, target={2}: target is missing or out of range",
                    Action, SenderIdentity, TargetIdentity);
                return;
            }

            if (isCombatAction)
            {
                var damage = CalculateDamage(client, Action);
                target.Character.HealthPoints = (ushort)Math.Max(
                    0, target.Character.HealthPoints - damage);
                Data = damage;

                Console.WriteLine(
                    "[MsgInteract] Attack action={0}, sender={1}, target={2}, damage={3}, remainingHealth={4}",
                    Action, SenderIdentity, TargetIdentity, damage,
                    target.Character.HealthPoints);
            }

            var recipients = Kernel.Clients.Values
                .Where(x => x != client && x.Character != null && x.Socket.Connected &&
                    x.Character.MapID == client.Character.MapID &&
                    IsWithin(PosX, PosY, x.Character.X, x.Character.Y, MsgWalk.ViewDistance))
                .ToArray();

            await client.SendAsync(this);
            await Task.WhenAll(recipients.Select(x => x.SendAsync(this)));
        }

        private static int CalculateDamage(Client client, MsgInteractType action)
        {
            var character = client.Character;
            var damage = Math.Max(1, character.Strength + character.Level);

            if (action == MsgInteractType.MagicAttack)
                damage = Math.Max(1, character.Spirit + character.Level);

            return damage;
        }

        private static bool IsWithin(ushort x, ushort y, ushort otherX, ushort otherY, int distance)
        {
            return Math.Abs((int)x - otherX) <= distance &&
                Math.Abs((int)y - otherY) <= distance;
        }
    }

    public enum MsgInteractType : uint
    {
        None = 0,
        Attack = 2,
        Heal = 3,
        MagicAttack = 24,
        Dash = 27,
        Shoot = 28,
        InteractRequest = 46,
        InteractConfirm = 47,
        Interact = 48,
        InteractStop = 50
    }
}
