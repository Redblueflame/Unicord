using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Models;
using Newtonsoft.Json;

namespace DSharpPlus.Entities
{
    /// <summary>
    /// Represents a discord channel.
    /// </summary>
    public class DiscordChannel : SnowflakeObject, IEquatable<DiscordChannel>, IComparable<DiscordChannel>
    {
        [JsonProperty("permission_overwrites", NullValueHandling = NullValueHandling.Ignore)]
        internal List<DiscordOverwrite> _permission_overwrites = new List<DiscordOverwrite>();

        private string _name;
        private ChannelType _type;
        private string _topic;
        private int _userLimit;
        private bool _isNSFW;

        /// <summary>
        /// Gets ID of the guild to which this channel belongs.
        /// </summary>
        [JsonProperty("guild_id", NullValueHandling = NullValueHandling.Ignore)]
        public ulong GuildId { get; internal set; }

        /// <summary>
        /// Gets ID of the category that contains this channel.
        /// </summary>
        [JsonProperty("parent_id", NullValueHandling = NullValueHandling.Include)]
        public ulong? ParentId { get; internal set; }

        /// <summary>
        /// Gets the category that contains this channel.
        /// </summary>
        [JsonIgnore]
        public DiscordChannel Parent
            => ParentId.HasValue ? Guild?.Channels[ParentId.Value] : null;

        /// <summary>
        /// Gets the name of this channel.
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public virtual string Name { get => _name; internal set => OnPropertySet(ref _name, value); }

        [JsonIgnore]
        public virtual string DisplayName
            => Type == ChannelType.Text ? $"#{Name}" : Name;

        /// <summary>
        /// Gets the type of this channel.
        /// </summary>
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public virtual ChannelType Type { get => _type; internal set => OnPropertySet(ref _type, value); }

        /// <summary>
        /// Gets the position of this channel.
        /// </summary>
        [JsonProperty("position", NullValueHandling = NullValueHandling.Ignore)]
        public int Position { get; set; }

        /// <summary>
        /// Gets whether this channel is a DM channel.
        /// </summary>
        [JsonIgnore]
        public bool IsPrivate
            => Type == ChannelType.Private || Type == ChannelType.Group;

        /// <summary>
        /// Gets whether this channel is a channel category.
        /// </summary>
        [JsonIgnore]
        public bool IsCategory
            => Type == ChannelType.Category;

        [JsonIgnore]
        public bool IsVoice
            => Type == ChannelType.Voice;

        /// <summary>
        /// Gets the guild to which this channel belongs.
        /// </summary>
        [JsonIgnore]
        public DiscordGuild Guild
            => Discord.Guilds.ContainsKey(GuildId) ? Discord.Guilds[GuildId] : null;

        /// <summary>
        /// Gets a collection of permission overwrites for this channel.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<DiscordOverwrite> PermissionOverwrites
            => new ReadOnlyList<DiscordOverwrite>(_permission_overwrites);

        /// <summary>
        /// Gets the channel's topic. This is applicable to text channels only.
        /// </summary>
        [JsonProperty("topic", NullValueHandling = NullValueHandling.Ignore)]
        public virtual string Topic { get => _topic; internal set => OnPropertySet(ref _topic, value); }

        /// <summary>
        /// Gets the ID of the last message sent in this channel. This is applicable to text channels only.
        /// </summary>
        [JsonProperty("last_message_id", NullValueHandling = NullValueHandling.Ignore)]
        public ulong LastMessageId { get; internal set; } = 0;

        /// <summary>
        /// Gets this channel's bitrate. This is applicable to voice channels only.
        /// </summary>
        [JsonProperty("bitrate", NullValueHandling = NullValueHandling.Ignore)]
        public int Bitrate { get; internal set; }

        /// <summary>
        /// Gets this channel's user limit. This is applicable to voice channels only.
        /// </summary>
        [JsonProperty("user_limit", NullValueHandling = NullValueHandling.Ignore)]
        public int UserLimit { get => _userLimit; internal set => OnPropertySet(ref _userLimit, value); }

        /// <summary>
        /// Gets this channel's mention string.
        /// </summary>
        [JsonIgnore]
        public string Mention
            => Formatter.Mention(this);

        [JsonIgnore]
        public DiscordReadState ReadState
        {
            get
            {
                if (Discord is DiscordClient client)
                {
                    if (client.ReadStates.TryGetValue(Id, out var v))
                    {
                        return v;
                    }
                    else
                    {
                        return DiscordReadState.Default;
                    }
                }

                return DiscordReadState.Default;
            }
        }

        /// <summary>
        /// Gets this channel's children. This applies only to channel categories.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<DiscordChannel> Children
        {
            get
            {
                if (!IsCategory)
                {
                    throw new ArgumentException("Only channel categories contain children");
                }

                return Guild._channels.Values.Where(e => e.ParentId == Id);
            }
        }

        /// <summary>
        /// Gets the list of members currently in the channel (if voice channel), or members who can see the channel (otherwise).
        /// </summary>
        [JsonIgnore]
        public virtual IEnumerable<DiscordMember> Users
        {
            get
            {
                if (Guild == null)
                {
                    throw new InvalidOperationException("Cannot query users outside of guild channels.");
                }

                if (Type == ChannelType.Voice)
                {
                    return Guild.Members.Values.Where(x => x.VoiceState?.ChannelId == Id).Distinct();
                }

                return Guild.Members.Values.Where(x => (PermissionsFor(x) & Permissions.AccessChannels) == Permissions.AccessChannels);
            }
        }

        [JsonIgnore]
        public virtual IEnumerable<DiscordMember> ConnectedUsers
        {
            get
            {
                if (Type == ChannelType.Voice)
                {
                    return Guild.Members.Values.Where(x => x.VoiceState?.ChannelId == Id).Distinct();
                }

                return Enumerable.Empty<DiscordMember>();
            }
        }

        public int UserCount => ConnectedUsers.Count();

        /// <summary>
        /// Gets whether this channel is an NSFW channel.
        /// </summary>
        [JsonProperty("nsfw")]
        public bool IsNSFW { get => _isNSFW; internal set => OnPropertySet(ref _isNSFW, value, ""); }

        /// <summary>
        /// <para>Gets the slow mode delay configured for this channel.</para>
        /// <para>All bots, as well as users with <see cref="Permissions.ManageChannels"/> or <see cref="Permissions.ManageMessages"/> permissions in the channel are exempt from slow mode.</para>
        /// </summary>
        [JsonProperty("rate_limit_per_user")]
        public int? PerUserRateLimit { get; internal set; }

        [JsonIgnore]
        public bool Muted
        {
            get => Discord.Configuration.MutedStore.GetMutedChannel(Id);
            set
            {
                Discord.Configuration.MutedStore.SetMutedChannel(Id, value);
                InvokePropertyChanged(nameof(Muted));
                InvokePropertyChanged(nameof(ReadState));
            }
        }

        [JsonIgnore]
        public bool Visible => Guild != null ? PermissionsFor(Guild.CurrentMember).HasPermission(Permissions.AccessChannels) : true;

        #region Methods
        /// <summary>
        /// Sends a message to this channel.
        /// </summary>
        /// <param name="content">Content of the message to send.</param>
        /// <param name="tts">Whether the message is to be read using TTS.</param>
        /// <param name="embed">Embed to attach to the message.</param>
        /// <returns>The sent message.</returns>
        public Task<DiscordMessage> SendMessageAsync(string content = null, bool tts = false, DiscordEmbed embed = null)
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("Cannot send a text message to a non-text channel");
            }

            if (string.IsNullOrWhiteSpace(content) && embed == null)
            {
                throw new ArgumentNullException("Must provide either content, embed or both, and content may not consist only of whitespace");
            }

            if (content != null && content.Length > 2000)
            {
                throw new ArgumentException("Message must be less than or exactly 2000 characters", nameof(content));
            }

            return Discord.ApiClient.CreateMessageAsync(Id, content, tts, embed);
        }

        /// <summary>
        /// Sends a message containing an attached file to this channel.
        /// </summary>
        /// <param name="file_data">Stream containing the data to attach to the message as a file.</param>
        /// <param name="file_name">Name of the file to attach to the message.</param>
        /// <param name="content">Content of the message to send.</param>
        /// <param name="tts">Whether the message is to be read using TTS.</param>
        /// <param name="embed">Embed to attach to the message.</param>
        /// <returns>The sent message.</returns>
        public Task<DiscordMessage> SendFileAsync(Stream file_data, string file_name, string content = null, bool tts = false, DiscordEmbed embed = null)
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("Cannot send a file to a non-text channel");
            }

            return Discord.ApiClient.UploadFileAsync(Id, file_data, file_name, content, tts, embed);
        }

#if !NETSTANDARD1_1 && !WINDOWS_8
        /// <summary>
        /// Sends a message containing an attached file to this channel.
        /// </summary>
        /// <param name="file_data">Stream containing the data to attach to the message as a file.</param>
        /// <param name="content">Content of the message to send.</param>
        /// <param name="tts">Whether the message is to be read using TTS.</param>
        /// <param name="embed">Embed to attach to the message.</param>
        /// <returns>The sent message.</returns>
        public Task<DiscordMessage> SendFileAsync(FileStream file_data, string content = null, bool tts = false, DiscordEmbed embed = null)
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("Cannot send a file to a non-text channel");
            }

            return Discord.ApiClient.UploadFileAsync(Id, file_data, Path.GetFileName(file_data.Name), content,
                tts, embed);
        }

        /// <summary>
        /// Sends a message containing an attached file to this channel.
        /// </summary>
        /// <param name="file_path">Path to the file to be attached to the message.</param>
        /// <param name="content">Content of the message to send.</param>
        /// <param name="tts">Whether the message is to be read using TTS.</param>
        /// <param name="embed">Embed to attach to the message.</param>
        /// <returns>The sent message.</returns>
        public async Task<DiscordMessage> SendFileAsync(string file_path, string content = null, bool tts = false, DiscordEmbed embed = null)
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("Cannot send a file to a non-text channel");
            }

            using (var fs = File.OpenRead(file_path))
            {
                return await Discord.ApiClient.UploadFileAsync(Id, fs, Path.GetFileName(fs.Name), content, tts, embed).ConfigureAwait(false);
            }
        }
#endif

        /// <summary>
        /// Sends a message with several attached files to this channel.
        /// </summary>
        /// <param name="files">A filename to data stream mapping.</param>
        /// <param name="content">Content of the message to send.</param>
        /// <param name="tts">Whether the message is to be read using TTS.</param>
        /// <param name="embed">Embed to attach to the message.</param>
        /// <returns>The sent message.</returns>
        public Task<DiscordMessage> SendMultipleFilesAsync(Dictionary<string, Stream> files, string content = "", bool tts = false, DiscordEmbed embed = null)
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("Cannot send a file to a non-text channel");
            }

            return Discord.ApiClient.UploadFilesAsync(Id, files, content, tts, embed);
        }

        // Please send memes to Naamloos#2887 at discord <3 thank you

        /// <summary>
        /// Deletes a guild channel
        /// </summary>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task DeleteAsync(string reason = null)
            => Discord.ApiClient.DeleteChannelAsync(Id, reason);

        /// <summary>
        /// Clones this channel. This operation will create a channel with identical settings to this one. Note that this will not copy messages.
        /// </summary>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns>Newly-created channel.</returns>
        public async Task<DiscordChannel> CloneAsync(string reason = null)
        {
            if (Guild == null)
            {
                throw new InvalidOperationException("Non-guild channels cannot be cloned.");
            }

            var ovrs = new List<DiscordOverwriteBuilder>();
            foreach (var ovr in _permission_overwrites)
            {
                ovrs.Add(await new DiscordOverwriteBuilder().FromAsync(ovr).ConfigureAwait(false));
            }

            int? bitrate = Bitrate;
            int? userLimit = UserLimit;
            Optional<int?> perUserRateLimit = PerUserRateLimit;
            if (Type != ChannelType.Voice)
            {
                bitrate = null;
                userLimit = null;
            }
            if (Type != ChannelType.Text)
            {
                perUserRateLimit = Optional.FromNoValue<int?>();
            }

            return await Guild.CreateChannelAsync(Name, Type, Parent, bitrate, userLimit, ovrs, IsNSFW, perUserRateLimit, reason).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a specific message
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<DiscordMessage> GetMessageAsync(ulong id, bool cacheOnly = false)
        {
            if (Discord.Configuration.MessageCacheSize > 0 && Discord is DiscordClient dc && dc.MessageCache.TryGet(xm => xm.Id == id && xm.ChannelId == Id, out var msg))
            {
                return msg;
            }

            if (!cacheOnly)
            {
                return await Discord.ApiClient.GetMessageAsync(Id, id).ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>
        /// Modifies the current channel.
        /// </summary>
        /// <param name="action">Action to perform on this channel</param>
        /// <returns></returns>
        public Task ModifyAsync(Action<ChannelEditModel> action)
        {
            var mdl = new ChannelEditModel(this);
            action(mdl);
            return Discord.ApiClient.ModifyChannelAsync(Id, mdl.Name, mdl.Position, mdl.Topic, mdl.Nsfw,
                mdl.Parent.HasValue ? mdl.Parent.Value?.Id : default(Optional<ulong?>), mdl.Bitrate, mdl.Userlimit, mdl.PerUserRateLimit, mdl.AuditLogReason);
        }

        /// <summary>
        /// Updates the channel position
        /// </summary>
        /// <param name="position"></param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task ModifyPositionAsync(int position, string reason = null)
        {
            if (Guild == null)
            {
                throw new InvalidOperationException("Cannot modify order of non-guild channels.");
            }

            var chns = Guild._channels.Values.Where(xc => xc.Type == Type).OrderBy(xc => xc.Position).ToArray();
            var pmds = new RestGuildChannelReorderPayload[chns.Length];
            for (var i = 0; i < chns.Length; i++)
            {
                pmds[i] = new RestGuildChannelReorderPayload
                {
                    ChannelId = chns[i].Id
                };

                if (chns[i].Id == Id)
                {
                    pmds[i].Position = position;
                }
                else
                {
                    pmds[i].Position = chns[i].Position >= position ? chns[i].Position + 1 : chns[i].Position;
                }
            }

            return Discord.ApiClient.ModifyGuildChannelPosition(Guild.Id, pmds, reason);
        }

        /// <summary>  
        /// Returns a list of messages before a certain message.
        /// <param name="limit">The amount of messages to fetch, up to a maximum of 100</param>
        /// <param name="before">Message to fetch before from.</param>
        /// </summary> 
        public Task<IReadOnlyList<DiscordMessage>> GetMessagesBeforeAsync(ulong before, int limit = 100)
            => GetMessagesInternalAsync(limit, before, null, null);

        /// <summary>  
        /// Returns a list of messages after a certain message.
        /// <param name="limit">The amount of messages to fetch, up to a maximum of 100</param>
        /// <param name="after">Message to fetch after from.</param>
        /// </summary> 
        public Task<IReadOnlyList<DiscordMessage>> GetMessagesAfterAsync(ulong after, int limit = 100)
            => GetMessagesInternalAsync(limit, null, after, null);

        /// <summary>  
        /// Returns a list of messages around a certain message.
        /// <param name="limit">The amount of messages to fetch, up to a maximum of 100</param>
        /// <param name="around">Message to fetch around from.</param>
        /// </summary> 
        public Task<IReadOnlyList<DiscordMessage>> GetMessagesAroundAsync(ulong around, int limit = 100)
            => GetMessagesInternalAsync(limit, null, null, around);

        /// <summary>  
        /// Returns a list of messages from the last message in the channel.
        /// <param name="limit">The amount of messages to fetch, up to a maximum of 100</param>
        /// </summary> 
        public Task<IReadOnlyList<DiscordMessage>> GetMessagesAsync(int limit = 100) =>
            GetMessagesInternalAsync(limit, null, null, null);

        private Task<IReadOnlyList<DiscordMessage>> GetMessagesInternalAsync(int limit = 100, ulong? before = null, ulong? after = null, ulong? around = null)
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("Cannot get the messages of a non-text channel");
            }

            return Discord.ApiClient.GetChannelMessagesAsync(Id, limit, before, after, around);
        }

        /// <summary>
        /// Deletes multiple messages
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task DeleteMessagesAsync(IEnumerable<DiscordMessage> messages, string reason = null)
        {
            // don't enumerate more than once
            var msgs = messages as DiscordMessage[] ?? messages.ToArray();
            if (messages == null || !msgs.Any())
            {
                throw new ArgumentException("You need to specify at least one message to delete.");
            }

            if (msgs.Count() < 2)
            {
                return Discord.ApiClient.DeleteMessageAsync(Id, msgs.Single().Id, reason);
            }

            return Discord.ApiClient.DeleteMessagesAsync(Id, msgs.Where(xm => xm.Channel.Id == Id).Select(xm => xm.Id), reason);
        }

        /// <summary>
        /// Deletes a message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task DeleteMessageAsync(DiscordMessage message, string reason = null)
            => Discord.ApiClient.DeleteMessageAsync(Id, message.Id, reason);

        /// <summary>
        /// Returns a list of invite objects
        /// </summary>
        /// <returns></returns>
        public Task<IReadOnlyList<DiscordInvite>> GetInvitesAsync()
        {
            if (Guild == null)
            {
                throw new ArgumentException("Cannot get the invites of a channel that does not belong to a Guild");
            }

            return Discord.ApiClient.GetChannelInvitesAsync(Id);
        }

        /// <summary>
        /// Create a new invite object
        /// </summary>
        /// <param name="max_age"></param>
        /// <param name="max_uses"></param>
        /// <param name="temporary"></param>
        /// <param name="unique"></param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task<DiscordInvite> CreateInviteAsync(int max_age = 86400, int max_uses = 0, bool temporary = false, bool unique = false, string reason = null)
            => Discord.ApiClient.CreateChannelInviteAsync(Id, max_age, max_uses, temporary, unique, reason);

        /// <summary>
        /// Adds a channel permission overwrite for specified member.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="allow"></param>
        /// <param name="deny"></param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task AddOverwriteAsync(DiscordMember member, Permissions allow = Permissions.None, Permissions deny = Permissions.None, string reason = null)
            => Discord.ApiClient.EditChannelPermissionsAsync(Id, member.Id, allow, deny, "member", reason);

        /// <summary>
        /// Adds a channel permission overwrite for specified role.
        /// </summary>
        /// <param name="role"></param>
        /// <param name="allow"></param>
        /// <param name="deny"></param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task AddOverwriteAsync(DiscordRole role, Permissions allow = Permissions.None, Permissions deny = Permissions.None, string reason = null)
            => Discord.ApiClient.EditChannelPermissionsAsync(Id, role.Id, allow, deny, "role", reason);

        /// <summary>
        /// Post a typing indicator
        /// </summary>
        /// <returns></returns>
        public Task TriggerTypingAsync()
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("Cannot start typing in a non-text channel");
            }

            return Discord.ApiClient.TriggerTypingAsync(Id);
        }

        /// <summary>
        /// Returns all pinned messages
        /// </summary>
        /// <returns></returns>
        public Task<IReadOnlyList<DiscordMessage>> GetPinnedMessagesAsync()
        {
            if (Type != ChannelType.Text && Type != ChannelType.Private && Type != ChannelType.Group && Type != ChannelType.News)
            {
                throw new ArgumentException("A non-text channel does not have pinned messages");
            }

            return Discord.ApiClient.GetPinnedMessagesAsync(Id);
        }

        /// <summary>
        /// Create a new webhook
        /// </summary>
        /// <param name="name"></param>
        /// <param name="avatar"></param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public async Task<DiscordWebhook> CreateWebhookAsync(string name, Optional<Stream> avatar = default, string reason = null)
        {
            var av64 = Optional.FromNoValue<string>();
            if (avatar.HasValue && avatar.Value != null)
            {
                using (var imgtool = new ImageTool(avatar.Value))
                {
                    av64 = imgtool.GetBase64();
                }
            }
            else if (avatar.HasValue)
            {
                av64 = null;
            }

            return await Discord.ApiClient.CreateWebhookAsync(Id, name, av64, reason).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a list of webhooks
        /// </summary>
        /// <returns></returns>
        public Task<IReadOnlyList<DiscordWebhook>> GetWebhooksAsync()
            => Discord.ApiClient.GetChannelWebhooksAsync(Id);

        /// <summary>
        /// Moves a member to this voice channel
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        public async Task PlaceMemberAsync(DiscordMember member)
        {
            if (Type != ChannelType.Voice)
            {
                throw new ArgumentException("Cannot place member in a non-voice channel!"); // be a little more angery, let em learn!!1
            }

            await Discord.ApiClient.ModifyGuildMemberAsync(Guild.Id, member.Id, default, default, default,
                default, Id, null).ConfigureAwait(false);
        }

        /// <summary>
        /// Calculates permissions for a given member.
        /// </summary>
        /// <param name="mbr">Member to calculate permissions for.</param>
        /// <returns>Calculated permissions for a given member.</returns>
        public Permissions PermissionsFor(DiscordMember mbr)
        {
            // default permissions
            const Permissions def = Permissions.None;

            if (mbr == null)
                return def;

            // future note: might be able to simplify @everyone role checks to just check any role ... but i'm not sure
            // xoxo, ~uwx
            //
            // you should use a single tilde
            // ~emzi

            // user > role > everyone
            // allow > deny > undefined
            // =>
            // user allow > user deny > role allow > role deny > everyone allow > everyone deny
            // thanks to meew0

            if (IsPrivate || Guild == null)
            {
                return def;
            }

            if (Guild.OwnerId == mbr?.Id)
            {
                return ~def;
            }

            Permissions perms;

            // assign @everyone permissions
            var everyoneRole = Guild.EveryoneRole;
            perms = everyoneRole.Permissions;

            // roles that member is in
            var mbRoles = mbr.Roles.Where(xr => xr.Id != everyoneRole.Id).ToArray();
            // channel overrides for roles that member is in
            var mbRoleOverrides = mbRoles
                .Select(xr => _permission_overwrites.FirstOrDefault(xo => xo.Id == xr.Id))
                .Where(xo => xo != null)
                .ToList();

            // assign permissions from member's roles (in order)
            perms |= mbRoles.Aggregate(def, (c, role) => c | role.Permissions);

            // assign channel permission overwrites for @everyone pseudo-role
            var everyoneOverwrites = _permission_overwrites.FirstOrDefault(xo => xo.Id == everyoneRole.Id);
            if (everyoneOverwrites != null)
            {
                perms &= ~everyoneOverwrites.Denied;
                perms |= everyoneOverwrites.Allowed;
            }

            // assign channel permission overwrites for member's roles (explicit deny)
            perms &= ~mbRoleOverrides.Aggregate(def, (c, overs) => c | overs.Denied);
            // assign channel permission overwrites for member's roles (explicit allow)
            perms |= mbRoleOverrides.Aggregate(def, (c, overs) => c | overs.Allowed);

            // channel overrides for just this member
            var mbOverrides = _permission_overwrites.FirstOrDefault(xo => xo.Id == mbr.Id);
            if (mbOverrides == null)
            {
                return perms;
            }

            // assign channel permission overwrites for just this member
            perms &= ~mbOverrides.Denied;
            perms |= mbOverrides.Allowed;

            return perms;
        }

        /// <summary>
        /// Returns a string representation of this channel.
        /// </summary>
        /// <returns>String representation of this channel.</returns>
        public override string ToString()
        {
            if (Type == ChannelType.Category)
            {
                return $"Channel Category {Name} ({Id})";
            }

            if (Type == ChannelType.Text)
            {
                return $"Channel #{Name} ({Id})";
            }

            if (!string.IsNullOrWhiteSpace(Name))
            {
                return $"Channel {Name} ({Id})";
            }

            return $"Channel {Id}";
        }
        #endregion

        /// <summary>
        /// Checks whether this <see cref="DiscordChannel"/> is equal to another object.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>Whether the object is equal to this <see cref="DiscordChannel"/>.</returns>
        public override bool Equals(object obj) => Equals(obj as DiscordChannel);

        /// <summary>
        /// Checks whether this <see cref="DiscordChannel"/> is equal to another <see cref="DiscordChannel"/>.
        /// </summary>
        /// <param name="e"><see cref="DiscordChannel"/> to compare to.</param>
        /// <returns>Whether the <see cref="DiscordChannel"/> is equal to this <see cref="DiscordChannel"/>.</returns>
        public bool Equals(DiscordChannel e)
        {
            if (ReferenceEquals(e, null))
            {
                return false;
            }

            if (ReferenceEquals(this, e))
            {
                return true;
            }

            return Id == e.Id;
        }

        /// <summary>
        /// Gets the hash code for this <see cref="DiscordChannel"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="DiscordChannel"/>.</returns>
        public override int GetHashCode() => Id.GetHashCode();

        public int CompareTo(DiscordChannel other) => Position.CompareTo(other?.Position ?? 0);

        /// <summary>
        /// Gets whether the two <see cref="DiscordChannel"/> objects are equal.
        /// </summary>
        /// <param name="e1">First channel to compare.</param>
        /// <param name="e2">Second channel to compare.</param>
        /// <returns>Whether the two channels are equal.</returns>
        public static bool operator ==(DiscordChannel e1, DiscordChannel e2)
        {
            var o1 = e1 as object;
            var o2 = e2 as object;

            if ((o1 == null && o2 != null) || (o1 != null && o2 == null))
            {
                return false;
            }

            if (o1 == null && o2 == null)
            {
                return true;
            }

            return e1.Id == e2.Id;
        }

        /// <summary>
        /// Gets whether the two <see cref="DiscordChannel"/> objects are not equal.
        /// </summary>
        /// <param name="e1">First channel to compare.</param>
        /// <param name="e2">Second channel to compare.</param>
        /// <returns>Whether the two channels are not equal.</returns>
        public static bool operator !=(DiscordChannel e1, DiscordChannel e2)
            => !(e1 == e2);
    }
}
