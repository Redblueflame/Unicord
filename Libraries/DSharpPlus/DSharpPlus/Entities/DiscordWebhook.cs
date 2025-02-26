﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus.Net;
using Newtonsoft.Json;

namespace DSharpPlus.Entities
{
    /// <summary>
    /// Represents information about a Discord webhook.
    /// </summary>
    public class DiscordWebhook : SnowflakeObject, IEquatable<DiscordWebhook>
    {
        internal DiscordApiClient ApiClient { get; set; }

        /// <summary>
        /// Gets the ID of the guild this webhook belongs to.
        /// </summary>
        [JsonProperty("guild_id", NullValueHandling = NullValueHandling.Ignore)]
        public ulong GuildId { get; internal set; }

        /// <summary>
        /// Gets the ID of the channel this webhook belongs to.
        /// </summary>
        [JsonProperty("channel_id", NullValueHandling = NullValueHandling.Ignore)]
        public ulong ChannelId { get; internal set; }

        /// <summary>
        /// Gets the user this webhook was created by.
        /// </summary>
        [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
        public DiscordUser User { get; internal set; }

        /// <summary>
        /// Gets the default name of this webhook.
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; internal set; }

        /// <summary>
        /// Gets hash of the default avatar for this webhook.
        /// </summary>
        [JsonProperty("avatar", NullValueHandling = NullValueHandling.Ignore)]
        internal string AvatarHash { get; set; }

        /// <summary>
        /// Gets the default avatar url for this webhook.
        /// </summary>
        public string AvatarUrl
            => $"https://cdn.discordapp.com/avatars/{Id}/{AvatarHash}.png?size=1024";

        /// <summary>
        /// Gets the secure token of this webhook.
        /// </summary>
        [JsonProperty("token", NullValueHandling = NullValueHandling.Ignore)]
        public string Token { get; internal set; }

        internal DiscordWebhook() { }

        /// <summary>
        /// Modifies this webhook.
        /// </summary>
        /// <param name="name">New default name for this webhook.</param>
        /// <param name="avatar">New avatar for this webhook.</param>
        /// <returns>The modified webhook.</returns>
        public Task<DiscordWebhook> ModifyAsync(string name = null, Optional<Stream> avatar = default)
        {
            var avatarb64 = Optional.FromNoValue<string>();
            if (avatar.HasValue && avatar.Value != null)
            {
                using (var imgtool = new ImageTool(avatar.Value))
                {
                    avatarb64 = imgtool.GetBase64();
                }
            }
            else if (avatar.HasValue)
            {
                avatarb64 = null;
            }

            return Discord.ApiClient.ModifyWebhookAsync(Id, name, avatarb64, Token);
        }

        /// <summary>
        /// Permanently deletes this webhook.
        /// </summary>
        /// <returns></returns>
        public Task DeleteAsync()
            => Discord.ApiClient.DeleteWebhookAsync(Id, Token);

        /// <summary>
        /// Executes this webhook.
        /// </summary>
        /// <param name="content">The contents of the message to send.</param>
        /// <param name="username">Username to use for this message.</param>
        /// <param name="avatar_url">Url of the avatar to use for this message.</param>
        /// <param name="tts">Whether the message is to be spoken aloud.</param>
        /// <param name="embeds">Embeds to attach to the message being sent.</param>
        /// <returns></returns>
        public Task ExecuteAsync(string content = null, string username = null, string avatar_url = null, bool tts = false, IEnumerable<DiscordEmbed> embeds = null)
            => (Discord?.ApiClient ?? ApiClient).ExecuteWebhookAsync(Id, Token, content, username, avatar_url, tts, embeds);

        /// <summary>
        /// Executes this webhook in Slack compatibility mode.
        /// </summary>
        /// <param name="json">JSON containing Slack-compatible payload for this webhook.</param>
        /// <returns></returns>
        public Task ExecuteSlackAsync(string json)
            => (Discord?.ApiClient ?? ApiClient).ExecuteWebhookSlackAsync(Id, Token, json);

        /// <summary>
        /// Executes this webhook in GitHub compatibility mode.
        /// </summary>
        /// <param name="json">JSON containing GitHub-compatible payload for this webhook.</param>
        /// <returns></returns>
        public Task ExecuteGithubAsync(string json)
            => (Discord?.ApiClient ?? ApiClient).ExecuteWebhookGithubAsync(Id, Token, json);

        /// <summary>
        /// Checks whether this <see cref="DiscordWebhook"/> is equal to another object.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>Whether the object is equal to this <see cref="DiscordWebhook"/>.</returns>
        public override bool Equals(object obj) => Equals(obj as DiscordWebhook);

        /// <summary>
        /// Checks whether this <see cref="DiscordWebhook"/> is equal to another <see cref="DiscordWebhook"/>.
        /// </summary>
        /// <param name="e"><see cref="DiscordWebhook"/> to compare to.</param>
        /// <returns>Whether the <see cref="DiscordWebhook"/> is equal to this <see cref="DiscordWebhook"/>.</returns>
        public bool Equals(DiscordWebhook e)
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
        /// Gets the hash code for this <see cref="DiscordWebhook"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="DiscordWebhook"/>.</returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Gets whether the two <see cref="DiscordWebhook"/> objects are equal.
        /// </summary>
        /// <param name="e1">First webhook to compare.</param>
        /// <param name="e2">Second webhook to compare.</param>
        /// <returns>Whether the two webhooks are equal.</returns>
        public static bool operator ==(DiscordWebhook e1, DiscordWebhook e2)
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
        /// Gets whether the two <see cref="DiscordWebhook"/> objects are not equal.
        /// </summary>
        /// <param name="e1">First webhook to compare.</param>
        /// <param name="e2">Second webhook to compare.</param>
        /// <returns>Whether the two webhooks are not equal.</returns>
        public static bool operator !=(DiscordWebhook e1, DiscordWebhook e2)
            => !(e1 == e2);
    }
}
