namespace Comet.Game.Packets
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Comet.Game.States;
    using Comet.Network.Packets;

    public sealed class MsgUserAttrib : MsgBase<Client>
    {
        private readonly List<UserAttribute> Attributes = new List<UserAttribute>();

        public MsgUserAttrib(uint identity, ClientUpdateType type, ulong value)
        {
            Type = PacketType.MsgUserAttrib;
            Identity = identity;
            Attributes.Add(new UserAttribute((uint)type, value));
        }

        public uint Identity { get; set; }
        public int Amount => Attributes.Count;

        public void Append(ClientUpdateType type, ulong value)
        {
            Attributes.Add(new UserAttribute((uint)type, value));
        }

        public override byte[] Encode()
        {
            var writer = new PacketWriter();
            writer.Write((ushort)Type);
            writer.Write(Identity);
            writer.Write(Amount);

            foreach (var attribute in Attributes)
            {
                writer.Write(attribute.Type);
                writer.Write(attribute.Data);
            }

            return writer.ToArray();
        }

        public override Task ProcessAsync(Client client)
        {
            return Task.CompletedTask;
        }

        private readonly struct UserAttribute
        {
            public UserAttribute(uint type, ulong data)
            {
                Type = type;
                Data = data;
            }

            public uint Type { get; }
            public ulong Data { get; }
        }
    }

    public enum ClientUpdateType
    {
        Hitpoints = 0,
        MaxHitpoints = 1
    }
}
