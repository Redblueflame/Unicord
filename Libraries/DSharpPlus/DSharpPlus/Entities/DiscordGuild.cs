﻿#pragma warning disable CS0618
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Models;
using DSharpPlus.Net.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSharpPlus.Entities
{
    /// <summary>
    /// Represents a Discord guild.
    /// </summary>
    public class DiscordGuild : SnowflakeObject, IEquatable<DiscordGuild>
    {
        private bool _isSynced;
        private string _name;
        private string _iconHash;
        private string _splashHash;
        private ulong _ownerId;
        private string _voiceRegionId;
        private ulong _afkChannelId = 0;
        private int _afkTimeout;
        private bool _embedEnabled;
        private ulong _embedChannelId;
        private VerificationLevel _verificationLevel;
        private DefaultMessageNotifications _defaultMessageNotifications;
        private ExplicitContentFilter _explicitContentFilter;
        private ulong? _systemChannelId;

        /// <summary>
        /// Gets the guild's name.
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get => _name; internal set => OnPropertySet(ref _name, value); }

        /// <summary>
        /// Gets the guild icon's hash.
        /// </summary>
        [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
        public string IconHash { get => _iconHash; internal set { OnPropertySet(ref _iconHash, value); InvokePropertyChanged(nameof(IconUrl)); } }

        /// <summary>
        /// Gets the guild icon's url.
        /// </summary>
        [JsonIgnore]
        public string IconUrl
            => !string.IsNullOrWhiteSpace(IconHash) ? $"https://cdn.discordapp.com/icons/{Id.ToString(CultureInfo.InvariantCulture)}/{IconHash}.jpg" : null;

        /// <summary>
        /// Gets the guild splash's hash.
        /// </summary>
        [JsonProperty("splash", NullValueHandling = NullValueHandling.Ignore)]
        public string SplashHash
        {
            get => _splashHash;
            internal set
            {
                OnPropertySet(ref _splashHash, value);
                InvokePropertyChanged(nameof(SplashUrl));
            }
        }

        /// <summary>
        /// Gets the guild splash's url.
        /// </summary>
        [JsonIgnore]
        public string SplashUrl
            => !string.IsNullOrWhiteSpace(SplashHash) ? $"https://cdn.discordapp.com/splashes/{Id.ToString(CultureInfo.InvariantCulture)}/{SplashHash}.jpg" : null;

        [JsonProperty("banner", NullValueHandling = NullValueHandling.Ignore)]
        public string BannerHash
        {
            get => _bannerHash;
            internal set
            {
                OnPropertySet(ref _bannerHash, value);
                InvokePropertyChanged(nameof(BannerUrl));
            }
        }

        [JsonIgnore]
        public string BannerUrl 
            => !string.IsNullOrWhiteSpace(BannerHash) ? $"https://cdn.discordapp.com/banners/{Id.ToString(CultureInfo.InvariantCulture)}/{BannerHash}.png?size=256" : null;

        /// <summary>
        /// Gets the ID of the guild's owner.
        /// </summary>
        [JsonProperty("owner_id", NullValueHandling = NullValueHandling.Ignore)]
        public ulong OwnerId
        {
            get => _ownerId;
            set
            {
                OnPropertySet(ref _ownerId, value);
                InvokePropertyChanged(nameof(Owner));
            }
        }

        /// <summary>
        /// Gets the guild's owner.
        /// </summary>
        [JsonIgnore]
        public DiscordMember Owner
            => Members.TryGetValue(OwnerId, out var memb) ? memb : null;

        /// <summary>
        /// Gets the guild's voice region ID.
        /// </summary>
        [JsonProperty("region", NullValueHandling = NullValueHandling.Ignore)]
        internal string VoiceRegionId
        {
            get => _voiceRegionId;
            set
            {
                OnPropertySet(ref _voiceRegionId, value);
                InvokePropertyChanged(nameof(VoiceRegion));
            }
        }

        /// <summary>
        /// Gets the guild's voice region.
        /// </summary>
        [JsonIgnore]
        public DiscordVoiceRegion VoiceRegion
            => Discord.VoiceRegions.TryGetValue(VoiceRegionId, out var region) ? region : null;

        /// <summary>
        /// Gets the guild's AFK voice channel ID.
        /// </summary>
        [JsonProperty("afk_channel_id", NullValueHandling = NullValueHandling.Ignore)]
        internal ulong AfkChannelId
        {
            get => _afkChannelId;
            set
            {
                OnPropertySet(ref _afkChannelId, value);
                InvokePropertyChanged(nameof(AfkChannel));
            }
        }

        /// <summary>
        /// Gets the guild's AFK voice channel.
        /// </summary>
        [JsonIgnore]
        public DiscordChannel AfkChannel
            => Channels.TryGetValue(AfkChannelId, out var memb) ? memb : null;

        /// <summary>
        /// Gets the guild's AFK timeout.
        /// </summary>
        [JsonProperty("afk_timeout", NullValueHandling = NullValueHandling.Ignore)]
        public int AfkTimeout { get => _afkTimeout; internal set => OnPropertySet(ref _afkTimeout, value); }

        /// <summary>
        /// Gets whether this guild has the guild embed enabled.
        /// </summary>
        [JsonProperty("embed_enabled", NullValueHandling = NullValueHandling.Ignore)]
        public bool EmbedEnabled { get => _embedEnabled; internal set => OnPropertySet(ref _embedEnabled, value); }

        /// <summary>
        /// Gets the ID of the channel from the guild's embed.
        /// </summary>
        [JsonProperty("embed_channel_id", NullValueHandling = NullValueHandling.Ignore)]
        internal ulong EmbedChannelId
        {
            get => _embedChannelId;
            set
            {
                OnPropertySet(ref _embedChannelId, value);
                InvokePropertyChanged(nameof(EmbedChannel));
            }
        }

        /// <summary>
        /// Gets the channel from the guild's embed.
        /// </summary>
        [JsonIgnore]
        public DiscordChannel EmbedChannel
            => Channels.TryGetValue(EmbedChannelId, out var chan) ? chan : null;

        /// <summary>
        /// Gets the guild's verification level.
        /// </summary>
        [JsonProperty("verification_level", NullValueHandling = NullValueHandling.Ignore)]
        public VerificationLevel VerificationLevel { get => _verificationLevel; internal set => OnPropertySet(ref _verificationLevel, value); }

        /// <summary>
        /// Gets the guild's default notification settings.
        /// </summary>
        [JsonProperty("default_message_notifications", NullValueHandling = NullValueHandling.Ignore)]
        public DefaultMessageNotifications DefaultMessageNotifications { get => _defaultMessageNotifications; internal set => OnPropertySet(ref _defaultMessageNotifications, value); }

        /// <summary>
        /// Gets the guild's explicit content filter settings.
        /// </summary>
        [JsonProperty("explicit_content_filter")]
        public ExplicitContentFilter ExplicitContentFilter { get => _explicitContentFilter; internal set => OnPropertySet(ref _explicitContentFilter, value); }

        [JsonProperty("system_channel_id", NullValueHandling = NullValueHandling.Include)]
        internal ulong? SystemChannelId
        {
            get => _systemChannelId;
            set
            {
                OnPropertySet(ref _systemChannelId, value);
                InvokePropertyChanged(nameof(SystemChannel));
            }
        }

        /// <summary>
        /// Gets the channel to which system messages (such as join notifications) are sent.
        /// </summary>
        [JsonIgnore]
        public DiscordChannel SystemChannel => SystemChannelId.HasValue && Channels.TryGetValue(SystemChannelId.Value, out var chan)
            ? chan
            : null;

        /// <summary>
        /// Gets a collection of this guild's roles.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<ulong, DiscordRole> Roles =>
            new ReadOnlyConcurrentDictionary<ulong, DiscordRole>(_roles);

        [JsonProperty("roles", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(SnowflakeArrayAsDictionaryJsonConverter))]
        internal ConcurrentDictionary<ulong, DiscordRole> _roles;

        /// <summary>
        /// Gets a collection of this guild's emojis.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<ulong, DiscordEmoji> Emojis =>
            new ReadOnlyConcurrentDictionary<ulong, DiscordEmoji>(_emojis);

        [JsonProperty("emojis", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(SnowflakeArrayAsDictionaryJsonConverter))]
        internal ConcurrentDictionary<ulong, DiscordEmoji> _emojis;

        /// <summary>
        /// Gets a collection of this guild's features.
        /// </summary>
        [JsonProperty("features", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string> Features { get; internal set; }

        /// <summary>
        /// Gets the required multi-factor authentication level for this guild.
        /// </summary>
        [JsonProperty("mfa_level", NullValueHandling = NullValueHandling.Ignore)]
        public MfaLevel MfaLevel { get; internal set; }

        /// <summary>
        /// Gets this guild's join date.
        /// </summary>
        [JsonProperty("joined_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset JoinedAt { get; internal set; }

        /// <summary>
        /// Gets whether this guild is considered to be a large guild.
        /// </summary>
        [JsonProperty("large", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsLarge { get; internal set; }

        /// <summary>
        /// Gets whether this guild is unavailable.
        /// </summary>
        [JsonProperty("unavailable", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsUnavailable { get; internal set; }

        /// <summary>
        /// Gets the total number of members in this guild.
        /// </summary>
        [JsonProperty("member_count", NullValueHandling = NullValueHandling.Ignore)]
        public int MemberCount { get; internal set; }

        /// <summary>
        /// Gets vanity URL code for this guild, when applicable.
        /// </summary>
        [JsonProperty("vanity_url_code")]
        public string VanityUrlCode { get; internal set; }

        /// <summary>
        /// Gets guild description, when applicable.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; internal set; }

        /// <summary>
        /// Gets this guild's premium tier (Nitro boosting).
        /// </summary>
        [JsonProperty("premium_tier")]
        public PremiumTier PremiumTier { get; internal set; }

        /// <summary>
        /// Gets the amount of members that boosted this guild.
        /// </summary>
        [JsonProperty("premium_subscription_count", NullValueHandling = NullValueHandling.Ignore)]
        public int PremiumSubscriptionCount { get; internal set; }


        /// <summary>
        /// Gets a collection of all the voice states for this guilds.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<ulong, DiscordVoiceState> VoiceStates
            => new ReadOnlyConcurrentDictionary<ulong, DiscordVoiceState>(_voice_states);

        [JsonProperty("voice_states", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(SnowflakeArrayAsDictionaryJsonConverter))]
        internal ConcurrentDictionary<ulong, DiscordVoiceState> _voice_states;

        /// <summary>
        /// Gets a collection of all the members that belong to this guild.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<ulong, DiscordMember> Members
            => new ReadOnlyConcurrentDictionary<ulong, DiscordMember>(_members);

        [JsonProperty("members", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(SnowflakeArrayAsDictionaryJsonConverter))]
        internal ConcurrentDictionary<ulong, DiscordMember> _members;

        /// <summary>
        /// Gets a collection of all the channels associated with this guild.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<ulong, DiscordChannel> Channels
            => new ReadOnlyConcurrentDictionary<ulong, DiscordChannel>(_channels);

        [JsonProperty("channels", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(SnowflakeArrayAsDictionaryJsonConverter))]
        internal ConcurrentDictionary<ulong, DiscordChannel> _channels;
        private string _bannerHash;

        /// <summary>
        /// Gets the guild member for current user.
        /// </summary>
        [JsonIgnore]
        public DiscordMember CurrentMember
            => _members.TryGetValue(Discord.CurrentUser.Id, out var memb) ? memb : null;

        /// <summary>
        /// Gets the @everyone role for this guild.
        /// </summary>
        [JsonIgnore]
        public DiscordRole EveryoneRole
            => _roles.TryGetValue(Id, out var role) ? role : null;

        /// <summary>
        /// Gets whether the current user is the guild's owner.
        /// </summary>
        [JsonProperty("is_owner", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsOwner
            => OwnerId == Discord.CurrentUser.Id;

        // I need to work on this
        // 
        // /// <summary>
        // /// Gets channels ordered in a manner in which they'd be ordered in the UI of the discord client.
        // /// </summary>
        // [JsonIgnore]
        // public IEnumerable<DiscordChannel> OrderedChannels 
        //    => this._channels.OrderBy(xc => xc.Parent?.Position).ThenBy(xc => xc.Type).ThenBy(xc => xc.Position);

        public bool Muted
        {
            get => Discord.Configuration.MutedStore.GetMutedGuild(Id);
            set
            {
                Discord.Configuration.MutedStore.SetMutedGuild(Id, value);
                InvokePropertyChanged(nameof(Muted));
                InvokePropertyChanged(nameof(Unread));
            }
        }

        public bool Unread
        {
            get
            {
                if (CurrentMember == null || Muted)
                    return false;

                return (IsOwner ? _channels : _channels.Where(c => c.Value.PermissionsFor(CurrentMember).HasPermission(Permissions.AccessChannels)))
                    .Any(r => r.Value.ReadState?.Unread == true);
            }
        }

        public int MentionCount => Channels.Select(r => r.Value.ReadState?.MentionCount ?? 0).Sum();

        [JsonIgnore]
        public bool IsSynced { get => _isSynced; set => OnPropertySet(ref _isSynced, value); }

        [JsonProperty("lazy", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsLazy { get; set; }

        internal DiscordGuild()
        {
        }

        #region Guild Methods
        /// <summary>
        /// Adds a new member to this guild
        /// </summary>
        /// <param name="user">User to add</param>
        /// <param name="access_token">User's access token (OAuth2)</param>
        /// <param name="nickname">new nickame</param>
        /// <param name="roles">new roles</param>
        /// <param name="muted">whether this user has to be muted</param>
        /// <param name="deaf">whether this user has to be deafened</param>
        /// <returns></returns>
        public Task AddMemberAsync(DiscordUser user, string access_token, string nickname = null, IEnumerable<DiscordRole> roles = null,
            bool muted = false, bool deaf = false)
            => Discord.ApiClient.AddGuildMemberAsync(Id, user.Id, access_token, nickname, roles, muted, deaf);

        /// <summary>
        /// Deletes this guild. Requires the caller to be the owner of the guild.
        /// </summary>
        /// <returns></returns>
        public Task DeleteAsync()
            => Discord.ApiClient.DeleteGuildAsync(Id);

        /// <summary>
        /// Modifies this guild.
        /// </summary>
        /// <param name="action">Action to perform on this guild..</param>
        /// <returns>The modified guild object.</returns>
        public async Task<DiscordGuild> ModifyAsync(Action<GuildEditModel> action)
        {
            var mdl = new GuildEditModel();
            action(mdl);
            if (mdl.AfkChannel.HasValue && mdl.AfkChannel.Value.Type != ChannelType.Voice)
            {
                throw new ArgumentException("AFK channel needs to be a voice channel.");
            }

            var iconb64 = Optional.FromNoValue<string>();
            if (mdl.Icon.HasValue && mdl.Icon.Value != null)
            {
                using (var imgtool = new ImageTool(mdl.Icon.Value))
                {
                    iconb64 = imgtool.GetBase64();
                }
            }
            else if (mdl.Icon.HasValue)
            {
                iconb64 = null;
            }

            var splashb64 = Optional.FromNoValue<string>();
            if (mdl.Splash.HasValue && mdl.Splash.Value != null)
            {
                using (var imgtool = new ImageTool(mdl.Splash.Value))
                {
                    splashb64 = imgtool.GetBase64();
                }
            }
            else if (mdl.Splash.HasValue)
            {
                splashb64 = null;
            }

            return await Discord.ApiClient.ModifyGuildAsync(Id, mdl.Name, mdl.Region.IfPresent(e => e.Id),
                mdl.VerificationLevel, mdl.DefaultMessageNotifications, mdl.MfaLevel, mdl.ExplicitContentFilter,
                mdl.AfkChannel.IfPresent(e => e?.Id), mdl.AfkTimeout, iconb64, mdl.Owner.IfPresent(e => e.Id), splashb64,
                mdl.SystemChannel.IfPresent(e => e?.Id), mdl.AuditLogReason).ConfigureAwait(false);
        }

        /// <summary>
        /// Bans a specified member from this guild.
        /// </summary>
        /// <param name="member">Member to ban.</param>
        /// <param name="delete_message_days">How many days to remove messages from.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task BanMemberAsync(DiscordMember member, int delete_message_days = 0, string reason = null)
            => Discord.ApiClient.CreateGuildBanAsync(Id, member.Id, delete_message_days, reason);

        /// <summary>
        /// Bans a specified user by ID. This doesn't require the user to be in this guild.
        /// </summary>
        /// <param name="user_id">ID of the user to ban.</param>
        /// <param name="delete_message_days">How many days to remove messages from.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task BanMemberAsync(ulong user_id, int delete_message_days = 0, string reason = null)
            => Discord.ApiClient.CreateGuildBanAsync(Id, user_id, delete_message_days, reason);

        /// <summary>
        /// Unbans a user from this guild.
        /// </summary>
        /// <param name="user">User to unban.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task UnbanMemberAsync(DiscordUser user, string reason = null)
            => Discord.ApiClient.RemoveGuildBanAsync(Id, user.Id, reason);

        /// <summary>
        /// Unbans a user by ID.
        /// </summary>
        /// <param name="user_id">ID of the user to unban.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns></returns>
        public Task UnbanMemberAsync(ulong user_id, string reason = null)
            => Discord.ApiClient.RemoveGuildBanAsync(Id, user_id, reason);

        /// <summary>
        /// Leaves this guild.
        /// </summary>
        /// <returns></returns>
        public Task LeaveAsync()
            => Discord.ApiClient.LeaveGuildAsync(Id);

        /// <summary>
        /// Gets the bans for this guild.
        /// </summary>
        /// <returns>Collection of bans in this guild.</returns>
        public Task<IReadOnlyList<DiscordBan>> GetBansAsync()
            => Discord.ApiClient.GetGuildBansAsync(Id);

        /// <summary>
        /// Creates a new text channel in this guild.
        /// </summary>
        /// <param name="name">Name of the new channel.</param>
        /// <param name="parent">Category to put this channel in.</param>
        /// <param name="overwrites">Permission overwrites for this channel.</param>
        /// <param name="nsfw">Whether the channel is to be flagged as not safe for work.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns>The newly-created channel.</returns>
        public Task<DiscordChannel> CreateTextChannelAsync(string name, DiscordChannel parent = null, IEnumerable<DiscordOverwriteBuilder> overwrites = null, bool? nsfw = null, Optional<int?> perUserRateLimit = default, string reason = null)
                    => CreateChannelAsync(name, ChannelType.Text, parent, null, null, overwrites, nsfw, perUserRateLimit, reason);

        /// <summary>
        /// Creates a new channel category in this guild.
        /// </summary>
        /// <param name="name">Name of the new category.</param>
        /// <param name="overwrites">Permission overwrites for this category.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns>The newly-created channel category.</returns>
        public Task<DiscordChannel> CreateChannelCategoryAsync(string name, IEnumerable<DiscordOverwriteBuilder> overwrites = null, string reason = null)
            => CreateChannelAsync(name, ChannelType.Category, null, null, null, overwrites, null, Optional.FromNoValue<int?>(), reason);

        /// <summary>
        /// Creates a new voice channel in this guild.
        /// </summary>
        /// <param name="name">Name of the new channel.</param>
        /// <param name="parent">Category to put this channel in.</param>
        /// <param name="bitrate">Bitrate of the channel.</param>
        /// <param name="user_limit">Maximum number of users in the channel.</param>
        /// <param name="overwrites">Permission overwrites for this channel.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns>The newly-created channel.</returns>
        public Task<DiscordChannel> CreateVoiceChannelAsync(string name, DiscordChannel parent = null, int? bitrate = null, int? user_limit = null, IEnumerable<DiscordOverwriteBuilder> overwrites = null, string reason = null)
            => CreateChannelAsync(name, ChannelType.Voice, parent, bitrate, user_limit, overwrites, null, Optional.FromNoValue<int?>(), reason);

        /// <summary>
        /// Creates a new channel in this guild.
        /// </summary>
        /// <param name="name">Name of the new channel.</param>
        /// <param name="type">Type of the new channel.</param>
        /// <param name="parent">Category to put this channel in.</param>
        /// <param name="bitrate">Bitrate of the channel. Applies to voice only.</param>
        /// <param name="user_limit">Maximum number of users in the channel. Applies to voice only.</param>
        /// <param name="overwrites">Permission overwrites for this channel.</param>
        /// <param name="nsfw">Whether the channel is to be flagged as not safe for work. Applies to text only.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns>The newly-created channel.</returns>
        public Task<DiscordChannel> CreateChannelAsync(string name, ChannelType type, DiscordChannel parent = null, int? bitrate = null, int? userLimit = null, IEnumerable<DiscordOverwriteBuilder> overwrites = null, bool? nsfw = null, Optional<int?> perUserRateLimit = default, string reason = null)
        {
            if (type != ChannelType.Text && type != ChannelType.Voice && type != ChannelType.Category)
            {
                throw new ArgumentException("Channel type must be text, voice, or category.", nameof(type));
            }

            if (type == ChannelType.Category && parent != null)
            {
                throw new ArgumentException("Cannot specify parent of a channel category.", nameof(parent));
            }

            return Discord.ApiClient.CreateGuildChannelAsync(Id, name, type, parent?.Id, bitrate, userLimit, overwrites, nsfw, perUserRateLimit, reason);
        }

        // this is to commemorate the Great DAPI Channel Massacre of 2017-11-19.
        /// <summary>
        /// <para>Deletes all channels in this guild.</para>
        /// <para>Note that this is irreversible. Use carefully!</para>
        /// </summary>
        /// <returns></returns>
        public Task DeleteAllChannelsAsync()
        {
            var tasks = Channels.Select(xc => xc.Value.DeleteAsync());
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Estimates the number of users to be pruned.
        /// </summary>
        /// <param name="days">Minimum number of inactivity days required for users to be pruned.</param>
        /// <returns>Number of users that will be pruned.</returns>
        public Task<int> GetPruneCountAsync(int days)
            => Discord.ApiClient.GetGuildPruneCountAsync(Id, days);

        /// <summary>
        /// Prunes inactive users from this guild.
        /// </summary>
        /// <param name="days">Minimum number of inactivity days required for users to be pruned.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns>Number of users pruned.</returns>
        public Task<int> PruneAsync(int days, string reason = null)
            => Discord.ApiClient.BeginGuildPruneAsync(Id, days, reason);

        /// <summary>
        /// Gets integrations attached to this guild.
        /// </summary>
        /// <returns>Collection of integrations attached to this guild.</returns>
        public Task<IReadOnlyList<DiscordIntegration>> GetIntegrationsAsync()
            => Discord.ApiClient.GetGuildIntegrationsAsync(Id);

        /// <summary>
        /// Attaches an integration from current user to this guild.
        /// </summary>
        /// <param name="integration">Integration to attach.</param>
        /// <returns>The integration after being attached to the guild.</returns>
        public Task<DiscordIntegration> AttachUserIntegrationAsync(DiscordIntegration integration)
            => Discord.ApiClient.CreateGuildIntegrationAsync(Id, integration.Type, integration.Id);

        /// <summary>
        /// Modifies an integration in this guild.
        /// </summary>
        /// <param name="integration">Integration to modify.</param>
        /// <param name="expire_behaviour">Number of days after which the integration expires.</param>
        /// <param name="expire_grace_period">Length of grace period which allows for renewing the integration.</param>
        /// <param name="enable_emoticons">Whether emotes should be synced from this integration.</param>
        /// <returns>The modified integration.</returns>
        public Task<DiscordIntegration> ModifyIntegrationAsync(DiscordIntegration integration, int expire_behaviour, int expire_grace_period, bool enable_emoticons)
            => Discord.ApiClient.ModifyGuildIntegrationAsync(Id, integration.Id, expire_behaviour, expire_grace_period, enable_emoticons);

        /// <summary>
        /// Removes an integration from this guild.
        /// </summary>
        /// <param name="integration">Integration to remove.</param>
        /// <returns></returns>
        public Task DeleteIntegrationAsync(DiscordIntegration integration)
            => Discord.ApiClient.DeleteGuildIntegrationAsync(Id, integration);

        /// <summary>
        /// Forces re-synchronization of an integration for this guild.
        /// </summary>
        /// <param name="integration">Integration to synchronize.</param>
        /// <returns></returns>
        public Task SyncIntegrationAsync(DiscordIntegration integration)
            => Discord.ApiClient.SyncGuildIntegrationAsync(Id, integration.Id);

        /// <summary>
        /// Gets the guild widget.
        /// </summary>
        /// <returns>This guild's widget.</returns>
        public Task<DiscordGuildEmbed> GetEmbedAsync()
            => Discord.ApiClient.GetGuildEmbedAsync(Id);

        /// <summary>
        /// Gets the voice regions for this guild.
        /// </summary>
        /// <returns>Voice regions available for this guild.</returns>
        public async Task<IReadOnlyList<DiscordVoiceRegion>> ListVoiceRegionsAsync()
        {
            var vrs = await Discord.ApiClient.GetGuildVoiceRegionsAsync(Id).ConfigureAwait(false);
            foreach (var xvr in vrs)
            {
                Discord.InternalVoiceRegions.TryAdd(xvr.Id, xvr);
            }

            return vrs;
        }

        /// <summary>
        /// Gets all the invites created for all the channels in this guild.
        /// </summary>
        /// <returns>A collection of invites.</returns>
        public Task<IReadOnlyList<DiscordInvite>> GetInvitesAsync()
            => Discord.ApiClient.GetGuildInvitesAsync(Id);

        /// <summary>
        /// Gets all the webhooks created for all the channels in this guild.
        /// </summary>
        /// <returns>A collection of webhooks this guild has.</returns>
        public Task<IReadOnlyList<DiscordWebhook>> GetWebhooksAsync()
            => Discord.ApiClient.GetGuildWebhooksAsync(Id);

        /// <summary>
        /// Gets a member of this guild by their user ID.
        /// </summary>
        /// <param name="user_id">ID of the member to get.</param>
        /// <returns>The requested member.</returns>
        public async Task<DiscordMember> GetMemberAsync(ulong user_id)
        {
            if (!_members.TryGetValue(user_id, out var mbr))
            {
                return mbr;
            }

            mbr = await Discord.ApiClient.GetGuildMemberAsync(Id, user_id).ConfigureAwait(false);
            _members[user_id] = mbr;
            return mbr;
        }

        /// <summary>
        /// Requests a full list of members from Discord.
        /// </summary>
        /// <returns>A collection of all members in this guild.</returns>
        public async Task<IReadOnlyCollection<DiscordMember>> GetAllMembersAsync(CancellationToken token = default)
        {
            var recmbr = new HashSet<DiscordMember>();

            var recd = 1000;
            var last = 0ul;
            while (recd > 0)
            {
                var tms = await Discord.ApiClient.ListGuildMembersAsync(Id, 1000, last == 0 ? null : (ulong?)last).ConfigureAwait(false);
                recd = tms.Count;

                foreach (var xtm in tms)
                {
                    var usr = new DiscordUser(xtm.User) { Discord = Discord };
                    usr = Discord.UserCache.AddOrUpdate(xtm.User.Id, usr, (id, old) =>
                    {
                        old.Username = usr.Username;
                        old.Discord = usr.Discord;
                        old.AvatarHash = usr.AvatarHash;

                        return old;
                    });

                    recmbr.Add(new DiscordMember(xtm) { Discord = Discord, _guild_id = Id });
                }

                var tm = tms.LastOrDefault();
                last = tm?.User.Id ?? 0;
            }

            return new ReadOnlySet<DiscordMember>(recmbr);
        }

        /// <summary>
        /// Gets all the channels this guild has.
        /// </summary>
        /// <returns>A collection of this guild's channels.</returns>
        public Task<IReadOnlyList<DiscordChannel>> GetChannelsAsync()
            => Discord.ApiClient.GetGuildChannelsAsync(Id);

        /// <summary>
        /// Creates a new role in this guild.
        /// </summary>
        /// <param name="name">Name of the role.</param>
        /// <param name="permissions">Permissions for the role.</param>
        /// <param name="color">Color for the role.</param>
        /// <param name="hoist">Whether the role is to be hoisted.</param>
        /// <param name="mentionable">Whether the role is to be mentionable.</param>
        /// <param name="reason">Reason for audit logs.</param>
        /// <returns>The newly-created role.</returns>
        public Task<DiscordRole> CreateRoleAsync(string name = null, Permissions? permissions = null, DiscordColor? color = null, bool? hoist = null, bool? mentionable = null, string reason = null)
            => Discord.ApiClient.CreateGuildRole(Id, name, permissions, color?.Value, hoist, mentionable, reason);

        /// <summary>
        /// Gets a role from this guild by its ID.
        /// </summary>
        /// <param name="id">ID of the role to get.</param>
        /// <returns>Requested role.</returns>
        public DiscordRole GetRole(ulong id)
            => _roles.TryGetValue(id, out var role) ? role : null;

        /// <summary>
        /// Gets a channel from this guild by its ID.
        /// </summary>
        /// <param name="id">ID of the channel to get.</param>
        /// <returns>Requested channel.</returns>
        public DiscordChannel GetChannel(ulong id)
            => _channels.TryGetValue(id, out var chan) ? chan : null;

        /// <summary>
        /// Gets audit log entries for this guild.
        /// </summary>
        /// <param name="limit">Maximum number of entries to fetch.</param>
        /// <param name="by_member">Filter by member responsible.</param>
        /// <param name="action_type">Filter by action type.</param>
        /// <returns>A collection of requested audit log entries.</returns>
        public async Task<IReadOnlyList<DiscordAuditLogEntry>> GetAuditLogsAsync(int? limit = null, DiscordMember by_member = null, AuditLogActionType? action_type = null)
        {
            var alrs = new List<AuditLog>();
            int ac = 1, tc = 0, rmn = 100;
            var last = 0ul;
            while (ac > 0)
            {
                rmn = limit != null ? limit.Value - tc : 100;
                rmn = Math.Min(100, rmn);
                if (rmn <= 0) break;

                var alr = await Discord.ApiClient.GetAuditLogsAsync(Id, rmn, null, last == 0 ? null : (ulong?)last, by_member?.Id, (int?)action_type).ConfigureAwait(false);
                ac = alr.Entries.Count();
                tc += ac;
                if (ac > 0)
                {
                    last = alr.Entries.Last().Id;
                    alrs.Add(alr);
                }
            }

            var amr = alrs.SelectMany(xa => xa.Users)
                .GroupBy(xu => xu.Id)
                .Select(xgu => xgu.First());
            foreach (var xau in amr)
            {
                if (Discord.UserCache.ContainsKey(xau.Id))
                    continue;

                var xtu = new TransportUser
                {
                    Id = xau.Id,
                    Username = xau.Username,
                    Discriminator = xau.Discriminator,
                    AvatarHash = xau.AvatarHash
                };
                var xu = new DiscordUser(xtu) { Discord = Discord };
                xu = Discord.UserCache.AddOrUpdate(xu.Id, xu, (id, old) =>
                {
                    old.Username = xu.Username;
                    old.Discriminator = xu.Discriminator;
                    old.AvatarHash = xu.AvatarHash;
                    return old;
                });
            }

            var ahr = alrs.SelectMany(xa => xa.Webhooks)
                .GroupBy(xh => xh.Id)
                .Select(xgh => xgh.First());

            var ams = amr.Select(xau => _members.TryGetValue(xau.Id, out var member) ? member : new DiscordMember { Discord = Discord, Id = xau.Id, _guild_id = Id });
            var amd = ams.ToDictionary(xm => xm.Id, xm => xm);

            Dictionary<ulong, DiscordWebhook> ahd = null;
            if (ahr.Any())
            {
                var whr = await GetWebhooksAsync().ConfigureAwait(false);
                var whs = whr.ToDictionary(xh => xh.Id, xh => xh);

                var amh = ahr.Select(xah => whs.TryGetValue(xah.Id, out var webhook) ? webhook : new DiscordWebhook { Discord = Discord, Name = xah.Name, Id = xah.Id, AvatarHash = xah.AvatarHash, ChannelId = xah.ChannelId, GuildId = xah.GuildId, Token = xah.Token });
                ahd = amh.ToDictionary(xh => xh.Id, xh => xh);
            }

            var acs = alrs.SelectMany(xa => xa.Entries).OrderByDescending(xa => xa.Id);
            var entries = new List<DiscordAuditLogEntry>();
            foreach (var xac in acs)
            {
                DiscordAuditLogEntry entry = null;
                ulong t1, t2;
                int t3, t4;
                bool p1, p2;
                switch (xac.ActionType)
                {
                    case AuditLogActionType.GuildUpdate:
                        entry = new DiscordAuditLogGuildEntry
                        {
                            Target = this
                        };

                        var entrygld = entry as DiscordAuditLogGuildEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "name":
                                    entrygld.NameChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                case "owner_id":
                                    entrygld.OwnerChange = new PropertyChange<DiscordMember>
                                    {
                                        Before = _members.TryGetValue(xc.OldValueUlong, out var oldMember) ? oldMember : await GetMemberAsync(xc.OldValueUlong).ConfigureAwait(false),
                                        After = _members.TryGetValue(xc.NewValueUlong, out var newMember) ? newMember : await GetMemberAsync(xc.NewValueUlong).ConfigureAwait(false)
                                    };
                                    break;

                                case "icon_hash":
                                    entrygld.IconChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString != null ? $"https://cdn.discordapp.com/icons/{Id}/{xc.OldValueString}.webp" : null,
                                        After = xc.OldValueString != null ? $"https://cdn.discordapp.com/icons/{Id}/{xc.NewValueString}.webp" : null
                                    };
                                    break;

                                case "verification_level":
                                    entrygld.VerificationLevelChange = new PropertyChange<VerificationLevel>
                                    {
                                        Before = (VerificationLevel)(long)xc.OldValue,
                                        After = (VerificationLevel)(long)xc.NewValue
                                    };
                                    break;

                                case "afk_channel_id":
                                    ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entrygld.AfkChannelChange = new PropertyChange<DiscordChannel>
                                    {
                                        Before = GetChannel(t1),
                                        After = GetChannel(t2)
                                    };
                                    break;

                                case "widget_channel_id":
                                    ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entrygld.EmbedChannelChange = new PropertyChange<DiscordChannel>
                                    {
                                        Before = GetChannel(t1),
                                        After = GetChannel(t2)
                                    };
                                    break;

                                case "splash_hash":
                                    entrygld.SplashChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString != null ? $"https://cdn.discordapp.com/splashes/{Id}/{xc.OldValueString}.webp?size=2048" : null,
                                        After = xc.NewValueString != null ? $"https://cdn.discordapp.com/splashes/{Id}/{xc.NewValueString}.webp?size=2048" : null
                                    };
                                    break;

                                case "default_message_notifications":
                                    entrygld.NotificationSettingsChange = new PropertyChange<DefaultMessageNotifications>
                                    {
                                        Before = (DefaultMessageNotifications)(long)xc.OldValue,
                                        After = (DefaultMessageNotifications)(long)xc.NewValue
                                    };
                                    break;

                                case "system_channel_id":
                                    ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entrygld.SystemChannelChange = new PropertyChange<DiscordChannel>
                                    {
                                        Before = GetChannel(t1),
                                        After = GetChannel(t2)
                                    };
                                    break;

                                case "explicit_content_filter":
                                    entrygld.ExplicitContentFilterChange = new PropertyChange<ExplicitContentFilter>
                                    {
                                        Before = (ExplicitContentFilter)(long)xc.OldValue,
                                        After = (ExplicitContentFilter)(long)xc.NewValue
                                    };
                                    break;

                                case "mfa_level":
                                    entrygld.MfaLevelChange = new PropertyChange<MfaLevel>
                                    {
                                        Before = (MfaLevel)(long)xc.OldValue,
                                        After = (MfaLevel)(long)xc.NewValue
                                    };
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in guild update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }
                        break;

                    case AuditLogActionType.ChannelCreate:
                    case AuditLogActionType.ChannelDelete:
                    case AuditLogActionType.ChannelUpdate:
                        entry = new DiscordAuditLogChannelEntry
                        {
                            Target = GetChannel(xac.TargetId.Value) ?? new DiscordChannel { Id = xac.TargetId.Value }
                        };

                        var entrychn = entry as DiscordAuditLogChannelEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "name":
                                    entrychn.NameChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValue != null ? xc.OldValueString : null,
                                        After = xc.NewValue != null ? xc.NewValueString : null
                                    };
                                    break;

                                case "type":
                                    p1 = ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    p2 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entrychn.TypeChange = new PropertyChange<ChannelType?>
                                    {
                                        Before = p1 ? (ChannelType?)t1 : null,
                                        After = p2 ? (ChannelType?)t2 : null
                                    };
                                    break;

                                case "permission_overwrites":
                                    var olds = xc.OldValues?.OfType<JObject>()
                                        ?.Select(xjo => xjo.ToObject<DiscordOverwrite>())
                                        ?.Select(xo => { xo.Discord = Discord; return xo; });

                                    var news = xc.NewValues?.OfType<JObject>()
                                        ?.Select(xjo => xjo.ToObject<DiscordOverwrite>())
                                        ?.Select(xo => { xo.Discord = Discord; return xo; });

                                    entrychn.OverwriteChange = new PropertyChange<IReadOnlyList<DiscordOverwrite>>
                                    {
                                        Before = olds != null ? new ReadOnlyCollection<DiscordOverwrite>(new List<DiscordOverwrite>(olds)) : null,
                                        After = news != null ? new ReadOnlyCollection<DiscordOverwrite>(new List<DiscordOverwrite>(news)) : null
                                    };
                                    break;

                                case "topic":
                                    entrychn.TopicChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                case "nsfw":
                                    entrychn.NsfwChange = new PropertyChange<bool?>
                                    {
                                        Before = (bool?)xc.OldValue,
                                        After = (bool?)xc.NewValue
                                    };
                                    break;

                                case "bitrate":
                                    entrychn.BitrateChange = new PropertyChange<int?>
                                    {
                                        Before = (int?)(long?)xc.OldValue,
                                        After = (int?)(long?)xc.NewValue
                                    };
                                    break;

                                case "rate_limit_per_user":
                                    entrychn.PerUserRateLimitChange = new PropertyChange<int?>
                                    {
                                        Before = (int?)(long?)xc.OldValue,
                                        After = (int?)(long?)xc.NewValue
                                    };
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in channel update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }
                        break;

                    case AuditLogActionType.OverwriteCreate:
                    case AuditLogActionType.OverwriteDelete:
                    case AuditLogActionType.OverwriteUpdate:
                        entry = new DiscordAuditLogOverwriteEntry
                        {
                            Target = GetChannel(xac.TargetId.Value)?.PermissionOverwrites.FirstOrDefault(xo => xo.Id == xac.Options.Id),
                            Channel = GetChannel(xac.TargetId.Value)
                        };

                        var entryovr = entry as DiscordAuditLogOverwriteEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "deny":
                                    p1 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    p2 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entryovr.DenyChange = new PropertyChange<Permissions?>
                                    {
                                        Before = p1 ? (Permissions?)t1 : null,
                                        After = p2 ? (Permissions?)t2 : null
                                    };
                                    break;

                                case "allow":
                                    p1 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    p2 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entryovr.AllowChange = new PropertyChange<Permissions?>
                                    {
                                        Before = p1 ? (Permissions?)t1 : null,
                                        After = p2 ? (Permissions?)t2 : null
                                    };
                                    break;

                                case "type":
                                    entryovr.TypeChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                case "id":
                                    p1 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    p2 = ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entryovr.TargetIdChange = new PropertyChange<ulong?>
                                    {
                                        Before = p1 ? (ulong?)t1 : null,
                                        After = p2 ? (ulong?)t2 : null
                                    };
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in overwrite update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }
                        break;

                    case AuditLogActionType.Kick:
                        entry = new DiscordAuditLogKickEntry
                        {
                            Target = amd.TryGetValue(xac.TargetId.Value, out var kickMember) ? kickMember : new DiscordMember { Id = xac.TargetId.Value }
                        };
                        break;

                    case AuditLogActionType.Prune:
                        entry = new DiscordAuditLogPruneEntry
                        {
                            Days = xac.Options.DeleteMemberDays,
                            Toll = xac.Options.MembersRemoved
                        };
                        break;

                    case AuditLogActionType.Ban:
                    case AuditLogActionType.Unban:
                        entry = new DiscordAuditLogBanEntry
                        {
                            Target = amd.TryGetValue(xac.TargetId.Value, out var unbanMember) ? unbanMember : new DiscordMember { Id = xac.TargetId.Value }
                        };
                        break;

                    case AuditLogActionType.MemberUpdate:
                    case AuditLogActionType.MemberRoleUpdate:
                        entry = new DiscordAuditLogMemberUpdateEntry
                        {
                            Target = amd.TryGetValue(xac.TargetId.Value, out var roleUpdMember) ? roleUpdMember : new DiscordMember { Id = xac.TargetId.Value }
                        };

                        var entrymbu = entry as DiscordAuditLogMemberUpdateEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "nick":
                                    entrymbu.NicknameChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                case "deaf":
                                    entrymbu.DeafenChange = new PropertyChange<bool?>
                                    {
                                        Before = (bool?)xc.OldValue,
                                        After = (bool?)xc.NewValue
                                    };
                                    break;

                                case "mute":
                                    entrymbu.MuteChange = new PropertyChange<bool?>
                                    {
                                        Before = (bool?)xc.OldValue,
                                        After = (bool?)xc.NewValue
                                    };
                                    break;

                                case "$add":
                                    entrymbu.AddedRoles = new ReadOnlyCollection<DiscordRole>(xc.NewValues.Select(xo => (ulong)xo["id"]).Select(GetRole).ToList());
                                    break;

                                case "$remove":
                                    entrymbu.RemovedRoles = new ReadOnlyCollection<DiscordRole>(xc.NewValues.Select(xo => (ulong)xo["id"]).Select(GetRole).ToList());
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in member update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }
                        break;

                    case AuditLogActionType.RoleCreate:
                    case AuditLogActionType.RoleDelete:
                    case AuditLogActionType.RoleUpdate:
                        entry = new DiscordAuditLogRoleUpdateEntry
                        {
                            Target = GetRole(xac.TargetId.Value) ?? new DiscordRole { Id = xac.TargetId.Value }
                        };

                        var entryrol = entry as DiscordAuditLogRoleUpdateEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "name":
                                    entryrol.NameChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                case "color":
                                    p1 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t3);
                                    p2 = int.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t4);

                                    entryrol.ColorChange = new PropertyChange<int?>
                                    {
                                        Before = p1 ? (int?)t3 : null,
                                        After = p2 ? (int?)t4 : null
                                    };
                                    break;

                                case "permissions":
                                    entryrol.PermissionChange = new PropertyChange<Permissions?>
                                    {
                                        Before = xc.OldValue != null ? (Permissions?)(long)xc.OldValue : null,
                                        After = xc.NewValue != null ? (Permissions?)(long)xc.NewValue : null
                                    };
                                    break;

                                case "position":
                                    entryrol.PositionChange = new PropertyChange<int?>
                                    {
                                        Before = xc.OldValue != null ? (int?)(long)xc.OldValue : null,
                                        After = xc.NewValue != null ? (int?)(long)xc.NewValue : null,
                                    };
                                    break;

                                case "mentionable":
                                    entryrol.MentionableChange = new PropertyChange<bool?>
                                    {
                                        Before = xc.OldValue != null ? (bool?)xc.OldValue : null,
                                        After = xc.NewValue != null ? (bool?)xc.NewValue : null
                                    };
                                    break;

                                case "hoist":
                                    entryrol.HoistChange = new PropertyChange<bool?>
                                    {
                                        Before = (bool?)xc.OldValue,
                                        After = (bool?)xc.NewValue
                                    };
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in role update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }
                        break;

                    case AuditLogActionType.InviteCreate:
                    case AuditLogActionType.InviteDelete:
                    case AuditLogActionType.InviteUpdate:
                        entry = new DiscordAuditLogInviteEntry();

                        var inv = new DiscordInvite
                        {
                            Discord = Discord,
                            Guild = new DiscordInviteGuild
                            {
                                Discord = Discord,
                                Id = Id,
                                Name = Name,
                                SplashHash = SplashHash
                            }
                        };

                        var entryinv = entry as DiscordAuditLogInviteEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "max_age":
                                    p1 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t3);
                                    p2 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t4);

                                    entryinv.MaxAgeChange = new PropertyChange<int?>
                                    {
                                        Before = p1 ? (int?)t3 : null,
                                        After = p2 ? (int?)t4 : null
                                    };
                                    break;

                                case "code":
                                    inv.Code = xc.OldValueString ?? xc.NewValueString;

                                    entryinv.CodeChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                case "temporary":
                                    entryinv.TemporaryChange = new PropertyChange<bool?>
                                    {
                                        Before = xc.OldValue != null ? (bool?)xc.OldValue : null,
                                        After = xc.NewValue != null ? (bool?)xc.NewValue : null
                                    };
                                    break;

                                case "inviter_id":
                                    p1 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    p2 = ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entryinv.InviterChange = new PropertyChange<DiscordMember>
                                    {
                                        Before = amd.TryGetValue(t1, out var propBeforeMember) ? propBeforeMember : null,
                                        After = amd.TryGetValue(t2, out var propAfterMember) ? propAfterMember : null,
                                    };
                                    break;

                                case "channel_id":
                                    p1 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    p2 = ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entryinv.ChannelChange = new PropertyChange<DiscordChannel>
                                    {
                                        Before = p1 ? GetChannel(t1) : null,
                                        After = p2 ? GetChannel(t2) : null
                                    };

                                    var ch = entryinv.ChannelChange.Before ?? entryinv.ChannelChange.After;
                                    var cht = ch?.Type;
                                    inv.Channel = new DiscordInviteChannel
                                    {
                                        Discord = Discord,
                                        Id = p1 ? t1 : t2,
                                        Name = ch?.Name,
                                        Type = cht != null ? cht.Value : ChannelType.Unknown
                                    };
                                    break;

                                case "uses":
                                    p1 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t3);
                                    p2 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t4);

                                    entryinv.UsesChange = new PropertyChange<int?>
                                    {
                                        Before = p1 ? (int?)t3 : null,
                                        After = p2 ? (int?)t4 : null
                                    };
                                    break;

                                case "max_uses":
                                    p1 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t3);
                                    p2 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t4);

                                    entryinv.MaxUsesChange = new PropertyChange<int?>
                                    {
                                        Before = p1 ? (int?)t3 : null,
                                        After = p2 ? (int?)t4 : null
                                    };
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in invite update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }

                        entryinv.Target = inv;
                        break;

                    case AuditLogActionType.WebhookCreate:
                    case AuditLogActionType.WebhookDelete:
                    case AuditLogActionType.WebhookUpdate:
                        entry = new DiscordAuditLogWebhookEntry
                        {
                            Target = ahd.TryGetValue(xac.TargetId.Value, out var webhook) ? webhook : new DiscordWebhook { Id = xac.TargetId.Value }
                        };

                        var entrywhk = entry as DiscordAuditLogWebhookEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "name":
                                    entrywhk.NameChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                case "channel_id":
                                    p1 = ulong.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t1);
                                    p2 = ulong.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t2);

                                    entrywhk.ChannelChange = new PropertyChange<DiscordChannel>
                                    {
                                        Before = p1 ? GetChannel(t1) : null,
                                        After = p2 ? GetChannel(t2) : null
                                    };
                                    break;

                                case "type": // ???
                                    p1 = int.TryParse(xc.OldValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t3);
                                    p2 = int.TryParse(xc.NewValue as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out t4);

                                    entrywhk.TypeChange = new PropertyChange<int?>
                                    {
                                        Before = p1 ? (int?)t3 : null,
                                        After = p2 ? (int?)t4 : null
                                    };
                                    break;

                                case "avatar_hash":
                                    entrywhk.AvatarHashChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in webhook update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }
                        break;

                    case AuditLogActionType.EmojiCreate:
                    case AuditLogActionType.EmojiDelete:
                    case AuditLogActionType.EmojiUpdate:
                        entry = new DiscordAuditLogEmojiEntry
                        {
                            Target = _emojis.TryGetValue(xac.TargetId.Value, out var target) ? target : new DiscordEmoji { Id = xac.TargetId.Value }
                        };

                        var entryemo = entry as DiscordAuditLogEmojiEntry;
                        foreach (var xc in xac.Changes)
                        {
                            switch (xc.Key.ToLowerInvariant())
                            {
                                case "name":
                                    entryemo.NameChange = new PropertyChange<string>
                                    {
                                        Before = xc.OldValueString,
                                        After = xc.NewValueString
                                    };
                                    break;

                                default:
                                    Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown key in emoji update: {xc.Key}; this should be reported to devs", DateTime.Now);
                                    break;
                            }
                        }
                        break;

                    case AuditLogActionType.MessageDelete:
                        entry = new DiscordAuditLogMessageEntry
                        {
                            Channel = xac.Options != null ? GetChannel(xac.Options.ChannelId) : null,
                            MessageCount = xac.Options?.MessageCount
                        };

                        var entrymsg = entry as DiscordAuditLogMessageEntry;

                        if (entrymsg.Channel != null)
                        {
                            DiscordMessage msg = null;
                            if (Discord is DiscordClient dc && dc.MessageCache?.TryGet(xm => xm.Id == xac.TargetId.Value && xm.ChannelId == entrymsg.Channel.Id, out msg) == true)
                                entrymsg.Target = msg;
                            else
                                entrymsg.Target = new DiscordMessage { Discord = Discord, Id = xac.TargetId.Value };
                        }
                        break;

                    default:
                        Discord.DebugLogger.LogMessage(LogLevel.Warning, "DSharpPlus", $"Unknown audit log action type: {((int)xac.ActionType).ToString(CultureInfo.InvariantCulture)}; this should be reported to devs", DateTime.Now);
                        break;
                }

                if (entry == null)
                    continue;

                switch (xac.ActionType)
                {
                    case AuditLogActionType.ChannelCreate:
                    case AuditLogActionType.EmojiCreate:
                    case AuditLogActionType.InviteCreate:
                    case AuditLogActionType.OverwriteCreate:
                    case AuditLogActionType.RoleCreate:
                    case AuditLogActionType.WebhookCreate:
                        entry.ActionCategory = AuditLogActionCategory.Create;
                        break;

                    case AuditLogActionType.ChannelDelete:
                    case AuditLogActionType.EmojiDelete:
                    case AuditLogActionType.InviteDelete:
                    case AuditLogActionType.MessageDelete:
                    case AuditLogActionType.OverwriteDelete:
                    case AuditLogActionType.RoleDelete:
                    case AuditLogActionType.WebhookDelete:
                        entry.ActionCategory = AuditLogActionCategory.Delete;
                        break;

                    case AuditLogActionType.ChannelUpdate:
                    case AuditLogActionType.EmojiUpdate:
                    case AuditLogActionType.InviteUpdate:
                    case AuditLogActionType.MemberRoleUpdate:
                    case AuditLogActionType.MemberUpdate:
                    case AuditLogActionType.OverwriteUpdate:
                    case AuditLogActionType.RoleUpdate:
                    case AuditLogActionType.WebhookUpdate:
                        entry.ActionCategory = AuditLogActionCategory.Update;
                        break;

                    default:
                        entry.ActionCategory = AuditLogActionCategory.Other;
                        break;
                }

                entry.Discord = Discord;
                entry.ActionType = xac.ActionType;
                entry.Id = xac.Id;
                entry.Reason = xac.Reason;
                entry.UserResponsible = amd[xac.UserId];
                entries.Add(entry);
            }

            return new ReadOnlyCollection<DiscordAuditLogEntry>(entries);

        }

        /// <summary>
        /// Sends a guild sync request for this guild. This fills the guild's member and presence information, and starts dispatching additional events.
        /// 
        /// This can only be done for user tokens.
        /// </summary>
        /// <returns></returns>
        public Task SyncAsync()
            => Discord is DiscordClient dc ? dc.SyncGuildsAsync(this) : Task.Delay(0);

        /// <summary>
        /// Acknowledges all the messages in this guild. This is available to user tokens only.
        /// </summary>
        /// <returns></returns>
        public Task AcknowledgeAsync()
        {
            if (Discord.Configuration.TokenType == TokenType.User)
            {
                return Discord.ApiClient.AcknowledgeGuildAsync(Id);
            }

            throw new InvalidOperationException("ACK can only be used when logged in as regular user.");
        }

        /// <summary>
        /// Gets all of this guild's custom emojis.
        /// </summary>
        /// <returns>All of this guild's custom emojis.</returns>
        public Task<IReadOnlyList<DiscordGuildEmoji>> GetEmojisAsync()
            => Discord.ApiClient.GetGuildEmojisAsync(Id);

        /// <summary>
        /// Gets this guild's specified custom emoji.
        /// </summary>
        /// <param name="id">ID of the emoji to get.</param>
        /// <returns>The requested custom emoji.</returns>
        public Task<DiscordGuildEmoji> GetEmojiAsync(ulong id)
            => Discord.ApiClient.GetGuildEmojiAsync(Id, id);

        /// <summary>
        /// Creates a new custom emoji for this guild.
        /// </summary>
        /// <param name="name">Name of the new emoji.</param>
        /// <param name="image">Image to use as the emoji.</param>
        /// <param name="roles">Roles for which the emoji will be available. This works only if your application is whitelisted as integration.</param>
        /// <param name="reason">Reason for audit log.</param>
        /// <returns>The newly-created emoji.</returns>
        public Task<DiscordGuildEmoji> CreateEmojiAsync(string name, Stream image, IEnumerable<DiscordRole> roles = null, string reason = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = name.Trim();
            if (name.Length < 2 || name.Length > 50)
            {
                throw new ArgumentException("Emoji name needs to be between 2 and 50 characters long.");
            }

            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            string image64 = null;
            using (var imgtool = new ImageTool(image))
            {
                image64 = imgtool.GetBase64();
            }

            return Discord.ApiClient.CreateGuildEmojiAsync(Id, name, image64, roles?.Select(xr => xr.Id), reason);
        }

        /// <summary>
        /// Modifies a this guild's custom emoji.
        /// </summary>
        /// <param name="emoji">Emoji to modify.</param>
        /// <param name="name">New name for the emoji.</param>
        /// <param name="roles">Roles for which the emoji will be available. This works only if your application is whitelisted as integration.</param>
        /// <param name="reason">Reason for audit log.</param>
        /// <returns>The modified emoji.</returns>
        public Task<DiscordGuildEmoji> ModifyEmojiAsync(DiscordGuildEmoji emoji, string name, IEnumerable<DiscordRole> roles = null, string reason = null)
        {
            if (emoji == null)
            {
                throw new ArgumentNullException(nameof(emoji));
            }

            if (emoji.Guild.Id != Id)
            {
                throw new ArgumentException("This emoji does not belong to this guild.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = name.Trim();
            if (name.Length < 2 || name.Length > 50)
            {
                throw new ArgumentException("Emoji name needs to be between 2 and 50 characters long.");
            }

            return Discord.ApiClient.ModifyGuildEmojiAsync(Id, emoji.Id, name, roles?.Select(xr => xr.Id), reason);
        }

        /// <summary>
        /// Deletes this guild's custom emoji.
        /// </summary>
        /// <param name="emoji">Emoji to delete.</param>
        /// <param name="reason">Reason for audit log.</param>
        /// <returns></returns>
        public Task DeleteEmojiAsync(DiscordGuildEmoji emoji, string reason = null)
        {
            if (emoji == null)
            {
                throw new ArgumentNullException(nameof(emoji));
            }

            if (emoji.Guild.Id != Id)
            {
                throw new ArgumentException("This emoji does not belong to this guild.");
            }

            return Discord.ApiClient.DeleteGuildEmojiAsync(Id, emoji.Id, reason);
        }

        /// <summary>
        /// <para>Gets the default channel for this guild.</para>
        /// <para>Default channel is the first channel current member can see.</para>
        /// </summary>
        /// <returns>This member's default guild.</returns>
        // public DiscordChannel GetDefaultChannel() => _channels.Where(xc => xc.Type == ChannelType.Text)
        //        .OrderBy(xc => xc.Position)
        //        .FirstOrDefault(xc => (xc.PermissionsFor(CurrentMember) & Permissions.AccessChannels) == Permissions.AccessChannels);
        #endregion

        /// <summary>
        /// Returns a string representation of this guild.
        /// </summary>
        /// <returns>String representation of this guild.</returns>
        public override string ToString() => $"Guild {Id}; {Name}; {Members.Count}/{MemberCount} Members";

        /// <summary>
        /// Checks whether this <see cref="DiscordGuild"/> is equal to another object.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>Whether the object is equal to this <see cref="DiscordGuild"/>.</returns>
        public override bool Equals(object obj) => Equals(obj as DiscordGuild);

        /// <summary>
        /// Checks whether this <see cref="DiscordGuild"/> is equal to another <see cref="DiscordGuild"/>.
        /// </summary>
        /// <param name="e"><see cref="DiscordGuild"/> to compare to.</param>
        /// <returns>Whether the <see cref="DiscordGuild"/> is equal to this <see cref="DiscordGuild"/>.</returns>
        public bool Equals(DiscordGuild e)
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
        /// Gets the hash code for this <see cref="DiscordGuild"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="DiscordGuild"/>.</returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Gets whether the two <see cref="DiscordGuild"/> objects are equal.
        /// </summary>
        /// <param name="e1">First member to compare.</param>
        /// <param name="e2">Second member to compare.</param>
        /// <returns>Whether the two members are equal.</returns>
        public static bool operator ==(DiscordGuild e1, DiscordGuild e2)
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
        /// Gets whether the two <see cref="DiscordGuild"/> objects are not equal.
        /// </summary>
        /// <param name="e1">First member to compare.</param>
        /// <param name="e2">Second member to compare.</param>
        /// <returns>Whether the two members are not equal.</returns>
        public static bool operator !=(DiscordGuild e1, DiscordGuild e2)
            => !(e1 == e2);
    }

    /// <summary>
    /// Represents guild verification level.
    /// </summary>
    public enum VerificationLevel : int
    {
        /// <summary>
        /// No verification. Anyone can join and chat right away.
        /// </summary>
        None = 0,

        /// <summary>
        /// Low verification level. Users are required to have a verified email attached to their account in order to be able to chat.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium verification level. Users are required to have a verified email attached to their account, and account age need to be at least 5 minutes in order to be able to chat.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// (╯°□°）╯︵ ┻━┻ verification level. Users are required to have a verified email attached to their account, account age need to be at least 5 minutes, and they need to be in the server for at least 10 minutes in order to be able to chat.
        /// </summary>
        High = 3,

        /// <summary>
        /// ┻━┻ ﾐヽ(ಠ益ಠ)ノ彡┻━┻ verification level. Users are required to have a verified phone number attached to their account.
        /// </summary>
        Highest = 4
    }

    /// <summary>
    /// Represents default notification level for a guild.
    /// </summary>
    public enum DefaultMessageNotifications : int
    {
        /// <summary>
        /// All messages will trigger push notifications.
        /// </summary>
        AllMessages = 0,

        /// <summary>
        /// Only messages that mention the user (or a role he's in) will trigger push notifications.
        /// </summary>
        MentionsOnly = 1
    }

    /// <summary>
    /// Represents multi-factor authentication level required by a guild to use administrator functionality.
    /// </summary>
    public enum MfaLevel : int
    {
        /// <summary>
        /// Multi-factor authentication is not required to use administrator functionality.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Multi-factor authentication is required to use administrator functionality.
        /// </summary>
        Enabled = 1
    }

    /// <summary>
    /// Represents the value of explicit content filter in a guild.
    /// </summary>
    public enum ExplicitContentFilter : int
    {
        /// <summary>
        /// Explicit content filter is disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Only messages from members without any roles are scanned.
        /// </summary>
        MembersWithoutRoles = 1,

        /// <summary>
        /// Messages from all members are scanned.
        /// </summary>
        AllMembers = 2
    }
}
