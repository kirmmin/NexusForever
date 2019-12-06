using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.Social
{
    public class CustomChannel
    {
        public ulong ChannelId { get; }
        public string Name { get; }

        public CustomChannel(string name)
        {
            ChannelId = SocialManager.Instance.NextCustomChannelId;
            Name = name;
        }
    }
}
