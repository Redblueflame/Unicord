﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace DSharpPlus.Net.Models
{
    public class ChannelEditModel : BaseEditModel
    {
        public string Name { internal get; set; }
        public int? Position { internal get; set; }
        public string Topic { internal get; set; }
        public bool? Nsfw { internal get; set; }
        public Optional<DiscordChannel> Parent { internal get; set; }
        public int? Bitrate { internal get; set; }
        public int? Userlimit { internal get; set; }
        public Optional<int?> PerUserRateLimit { internal get; set; }

        internal ChannelEditModel()
        {

        }

        public ChannelEditModel(DiscordChannel channel)
        {
            Name = channel.Name;
            Topic = channel.Topic;
            Parent = channel.Parent;
            Bitrate = channel.Bitrate == 0 ? (int?)null : channel.Bitrate;
            Userlimit = channel.UserLimit == 0 ? (int?)null : channel.UserLimit;
            PerUserRateLimit = channel.PerUserRateLimit;
        }
    }
}
