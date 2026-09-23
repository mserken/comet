namespace Comet.Game.Packets
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Comet.Game.States;
    using Comet.Network.Packets;

    public sealed class MsgInteract : MsgBase<Client>
    {
        private const int CombatDistance = 2;
        private const int AttackIntervalMilliseconds = 1000;

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

            if (Action == MsgInteractType.InteractStop)
            {
                StopBattle(client);
                return;
            }

            if (!IsCombatAction(Action))
            {
                await BroadcastAsync(client, this);
                return;
            }

            SenderIdentity = client.ID;
            PosX = client.Character.X;
            PosY = client.Character.Y;

            var target = FindTarget(client, TargetIdentity);
            if (target == null || !IsWithin(PosX, PosY,
                target.Character.X, target.Character.Y, CombatDistance))
            {
                Console.WriteLine(
                    "[MsgInteract] Rejected action={0}, sender={1}, target={2}: target is missing or out of range",
                    Action, SenderIdentity, TargetIdentity);
                return;
            }

            StartBattle(client, TargetIdentity, Action);
        }

        private static bool IsCombatAction(MsgInteractType action)
        {
            return action == MsgInteractType.Attack ||
                action == MsgInteractType.Shoot ||
                action == MsgInteractType.MagicAttack;
        }

        private static Client FindTarget(Client client, uint identity)
        {
            return Kernel.Clients.Values.FirstOrDefault(x =>
                x != client && x.Character != null && x.Socket.Connected &&
                x.ID == identity && x.Character.MapID == client.Character.MapID);
        }

        private static void StartBattle(Client client, uint targetIdentity, MsgInteractType action)
        {
            if (client.BattleActive && client.BattleTargetIdentity == targetIdentity &&
                client.BattleAction == action)
                return;

            client.BattleGeneration++;
            client.BattleActive = true;
            client.BattleTargetIdentity = targetIdentity;
            client.BattleAction = action;
            _ = BattleLoopAsync(client, targetIdentity, action, client.BattleGeneration);
        }

        private static void StopBattle(Client client)
        {
            client.BattleGeneration++;
            client.BattleActive = false;
            client.BattleTargetIdentity = 0;
        }

        private static async Task BattleLoopAsync(
            Client client, uint targetIdentity, MsgInteractType action, int generation)
        {
            try
            {
                while (client.Socket.Connected &&
                    client.BattleActive &&
                    client.BattleGeneration == generation &&
                    client.BattleTargetIdentity == targetIdentity &&
                    client.BattleAction == action)
                {
                    var elapsed = DateTime.UtcNow - client.LastBattleAt;
                    var remaining = AttackIntervalMilliseconds - (int)elapsed.TotalMilliseconds;
                    if (remaining > 0)
                        await Task.Delay(remaining);

                    if (!client.BattleActive ||
                        client.BattleGeneration != generation ||
                        client.BattleTargetIdentity != targetIdentity ||
                        client.BattleAction != action)
                        break;

                    var target = FindTarget(client, targetIdentity);
                    if (target == null || target.Character.HealthPoints == 0 ||
                        !IsWithin(client.Character.X, client.Character.Y,
                            target.Character.X, target.Character.Y, CombatDistance))
                        break;

                    await AttackOnceAsync(client, target, action);
                }

                if (client.BattleGeneration == generation)
                {
                    client.BattleActive = false;
                    client.BattleTargetIdentity = 0;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("[MsgInteract] Battle loop failed for {0}: {1}", client.ID, exception);
                if (client.BattleGeneration == generation)
                {
                    client.BattleActive = false;
                    client.BattleTargetIdentity = 0;
                }
            }
        }

        private static async Task AttackOnceAsync(Client client, Client target, MsgInteractType action)
        {
            client.LastBattleAt = DateTime.UtcNow;
            var damage = CalculateDamage(client, action);
            target.Character.HealthPoints = (ushort)Math.Max(0,
                target.Character.HealthPoints - damage);

            var interaction = new MsgInteract
            {
                Timestamp = Environment.TickCount,
                SenderIdentity = client.ID,
                TargetIdentity = target.ID,
                PosX = client.Character.X,
                PosY = client.Character.Y,
                Action = action,
                Data = damage
            };

            await BroadcastAsync(client, interaction);
            await BroadcastAttributeAsync(target, new MsgUserAttrib(
                target.ID,
                ClientUpdateType.Hitpoints,
                target.Character.HealthPoints));
            Console.WriteLine(
                "[MsgInteract] Attack action={0}, sender={1}, target={2}, damage={3}, remainingHealth={4}",
                action, client.ID, target.ID, damage, target.Character.HealthPoints);
        }

        private static async Task BroadcastAttributeAsync(Client source, MsgUserAttrib attribute)
        {
            var recipients = Kernel.Clients.Values
                .Where(x => x.Character != null && x.Socket.Connected &&
                    x.Character.MapID == source.Character.MapID &&
                    IsWithin(source.Character.X, source.Character.Y,
                        x.Character.X, x.Character.Y, MsgWalk.ViewDistance))
                .ToArray();

            await Task.WhenAll(recipients.Select(x => x.SendAsync(attribute)));
        }

        private static async Task BroadcastAsync(Client client, MsgInteract interaction)
        {
            var recipients = Kernel.Clients.Values
                .Where(x => x != client && x.Character != null && x.Socket.Connected &&
                    x.Character.MapID == client.Character.MapID &&
                    IsWithin(client.Character.X, client.Character.Y,
                        x.Character.X, x.Character.Y, MsgWalk.ViewDistance))
                .ToArray();

            await client.SendAsync(interaction);
            await Task.WhenAll(recipients.Select(x => x.SendAsync(interaction)));
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
