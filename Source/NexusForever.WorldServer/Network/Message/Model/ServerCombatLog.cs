using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Combat.Static;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerCombatLog)]
    public class ServerCombatLog : IWritable
    {
        public class CCStateLog : IWritable
        {
            public CCState CCState { get; set; } // 5
            public bool BRemoved { get; set; }
            public uint InterruptArmorTaken { get; set; }
            public byte Result { get; set; } // 4
            public ushort Unknown0 { get; set; } // 14
            public uint CasterId { get; set; }
            public uint TargetId { get; set; }
            public uint Spell4Id { get; set; } // 18
            public byte CombatResult { get; set; } // 4

            public void Write(GamePacketWriter writer)
            {
                writer.Write(CCState, 5u);
                writer.Write(BRemoved);
                writer.Write(InterruptArmorTaken);
                writer.Write(Result, 4u);
                writer.Write(Unknown0, 14u);
                writer.Write(CasterId);
                writer.Write(TargetId);
                writer.Write(Spell4Id, 18u);
                writer.Write(CombatResult, 4u);
            }
        }

        public byte LogType { get; set; }
        public CCStateLog CCStateData { get; set; } = new CCStateLog();

        public void Write(GamePacketWriter writer)
        {
            writer.Write(LogType, 6u);

            switch (LogType)
            {
                case 1:
                    CCStateData.Write(writer);
                    break;
            }
        }
    }
}
