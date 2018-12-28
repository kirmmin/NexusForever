using NexusForever.Shared.Network;
using NexusForever.Shared.Network.Message;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Entity.Static;
using System;
using System.Collections.Generic;

namespace NexusForever.WorldServer.Network.Message.Model
{
    [Message(GameMessageOpcode.ServerCommunicatorSpeech)]
    public class ServerCommunicatorSpeech : IWritable
    {
        public enum ActorType
        {
            Creature,
            TextString,
            TextId,
            Player,
            CreatureUnit,
            LocalPlayer
        }

        public interface IActor
        {
            void Write(GamePacketWriter writer);
        }

        public class Actor : IWritable
        {

            public class Creature : IWritable, IActor
            {
                public uint CreatureId { get; set; } // 18

                public void Write(GamePacketWriter writer)
                {
                    writer.Write(CreatureId, 18u);
                }
            }

            public class TextString : IWritable, IActor
            {
                public string String { get; set; }

                public void Write(GamePacketWriter writer)
                {
                    writer.WriteStringWide(String);
                }
            }

            public class TextId : IWritable, IActor
            {
                public uint Id { get; set; } // 21

                public void Write(GamePacketWriter writer)
                {
                    writer.Write(Id, 21u);
                }
            }

            public class Player : IWritable, IActor
            {
                public uint Guid { get; set; }
                public string Name { get; set; }
                public uint Level { get; set; }
                public Sex Gender { get; set; } // 2
                public Race Race { get; set; } // 5
                public Class Class { get; set; } // 5
                public Faction Faction { get; set; } // 14
                public Path Path { get; set; } // 3
                public ushort Title { get; set; } // 14

                public void Write(GamePacketWriter writer)
                {
                    writer.Write(Guid);
                    writer.WriteStringWide(Name);
                    writer.Write(Level);
                    writer.Write(Gender, 2u);
                    writer.Write(Race, 5u);
                    writer.Write(Class, 5u);
                    writer.Write(Faction, 14u);
                    writer.Write(Path, 3u);
                    writer.Write(Title, 14u);
                }
            }

            public class CreatureUnit : IWritable, IActor
            {
                public uint Guid { get; set; }
                public uint CreatureId { get; set; } // 18

                public void Write(GamePacketWriter writer)
                {
                    writer.Write(Guid);
                    writer.Write(CreatureId, 18u);
                }
            }

            public class LocalPlayer : IWritable, IActor
            {
                public uint Guid { get; set; }

                public void Write(GamePacketWriter writer)
                {
                    writer.Write(Guid);
                }
            }

            public ActorType Type { get; set; } // 3
            public IActor ActorModel { get; set; }
            public List<byte> Unknown0 { get; set; } = new List<byte> { 0 };

            public void Write(GamePacketWriter writer)
            {
                writer.Write(Type, 3u);
                ActorModel.Write(writer);
                writer.Write(Unknown0.Count);
                Unknown0.ForEach(i => writer.Write(i));
            }
        }

        public uint MessageId { get; set; }
        public uint VoiceOverId { get; set; }
        public List<Actor> Actors { get; set; } = new List<Actor>();
        public uint SoundEventId { get; set; } // 18
        public uint DurationMs { get; set; } = 10000;
        public byte WindowTypeId { get; set; } = 0; // 2 
        public byte StoryPanelType { get; set; } = 0; // 2
        public byte Unknown0 { get; set; } // 3

        public void AddActor(ActorType actorType, uint creatureId = 0, Player player = null, string textString = "")
        {
            Actor actor = new Actor
            {
                Type = actorType
            };

            switch (actorType)
            {
                case ActorType.Creature:
                    actor.ActorModel = new Actor.Creature
                    {
                        CreatureId = creatureId
                    };
                    SoundEventId = creatureId;
                    break;
                case ActorType.Player:
                    actor.ActorModel = new Actor.Player
                    {
                        Guid = player.Guid,
                        Name = player.Name,
                        Level = player.Level,
                        Class = player.Class,
                        Faction = player.Faction1,
                        Gender = player.Sex,
                        Path = player.Path,
                        Race = player.Race,
                        Title = player.TitleManager.ActiveTitleId
                    };
                    break;
                case ActorType.TextString:
                    actor.ActorModel = new Actor.TextString
                    {
                        String = textString
                    };
                    break;
                default:
                    throw new NotImplementedException();
            }

            Actors.Add(actor);
        }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(MessageId);
            writer.Write(VoiceOverId);
            writer.Write(Actors.Count, 8u);
            Actors.ForEach(i => i.Write(writer));
            writer.Write(SoundEventId, 18u);
            writer.Write(DurationMs);
            writer.Write(WindowTypeId, 2u);
            writer.Write(StoryPanelType, 2u);
            writer.Write(Unknown0, 3u);
        }
    }
}
