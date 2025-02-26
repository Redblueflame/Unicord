﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Net.Abstractions;
using DSharpPlus.Net.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSharpPlus.Net
{
    public sealed class DiscordApiClient
    {
        private const string REASON_HEADER_NAME = "X-Audit-Log-Reason";

        internal BaseDiscordClient Discord { get; }
        internal RestClient Rest { get; }

        private string LastAckToken { get; set; } = null;
        private SemaphoreSlim TokenSemaphore { get; } = new SemaphoreSlim(1, 1);

        internal DiscordApiClient(BaseDiscordClient client)
        {
            Discord = client;
            Rest = new RestClient(client);
        }

        internal DiscordApiClient(IWebProxy proxy, TimeSpan timeout) // This is for meta-clients, such as the webhook client
        {
            Rest = new RestClient(proxy, timeout);
        }

        private static string BuildQueryString(IDictionary<string, string> values, bool post = false)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            var vals_collection = values.Select(xkvp =>
                $"{WebUtility.UrlEncode(xkvp.Key)}={WebUtility.UrlEncode(xkvp.Value)}");
            var vals = string.Join("&", vals_collection);

            return !post ? $"?{vals}" : vals;
        }

        private DiscordMessage PrepareMessage(JToken msg_raw)
        {
            var author = msg_raw["author"].ToObject<TransportUser>();
            var ret = msg_raw.ToDiscordObject<DiscordMessage>();

            if (Discord is DiscordClient dc && dc.MessageCache.TryGet(m => m.Id == ret.Id && m.ChannelId == ret.ChannelId, out var fasttrack))
            {
                return fasttrack;
            }

            ret.Discord = Discord;

            var guild = ret.Channel?.Guild;

            if (!Discord.UserCache.TryGetValue(author.Id, out var usr))
            {
                Discord.UserCache[author.Id] = (usr = new DiscordUser(author) { Discord = Discord });
            }

            if (guild != null)
            {
                var mbr = guild.Members.Values.FirstOrDefault(xm => xm.Id == author.Id);
                if (mbr == null)
                {
                    mbr = new DiscordMember(usr) { Discord = Discord, _guild_id = guild.Id };
                    guild._members[mbr.Id] = (mbr);
                }
                ret.Author = mbr;
            }
            else
            {
                ret.Author = usr;
            }

            if (ret._reactions == null)
            {
                ret._reactions = new List<DiscordReaction>();
            }

            foreach (var xr in ret._reactions)
            {
                xr.Emoji.Discord = Discord;
            }

            if (Discord is DiscordClient dc1)
            {
                dc1.MessageCache.Add(ret);
            }

            return ret;
        }

        private Task<RestResponse> DoRequestAsync(BaseDiscordClient client, RateLimitBucket bucket, Uri url, RestRequestMethod method, IDictionary<string, string> headers = null, string payload = null, double? ratelimit_wait_override = null)
        {
            var req = new RestRequest(client, bucket, url, method, headers, payload, ratelimit_wait_override);
            Discord.DebugLogger.LogTaskFault(Rest.ExecuteRequestAsync(req), LogLevel.Error, "REST", "Error while executing request: ");
            return req.WaitForCompletionAsync();
        }

        private Task<RestResponse> DoMultipartAsync(BaseDiscordClient client, RateLimitBucket bucket, Uri url, RestRequestMethod method, IDictionary<string, string> headers = null, IDictionary<string, string> values = null,
            IDictionary<string, Stream> files = null, double? ratelimit_wait_override = null)
        {
            var req = new MultipartWebRequest(client, bucket, url, method, headers, values, files, ratelimit_wait_override);
            Discord.DebugLogger.LogTaskFault(Rest.ExecuteRequestAsync(req), LogLevel.Error, "REST", "Error while executing request: ");
            return req.WaitForCompletionAsync();
        }

        #region Guild
        internal async Task<DiscordGuild> CreateGuildAsync(string name, string region_id, Optional<string> iconb64, VerificationLevel? verification_level,
            DefaultMessageNotifications? default_message_notifications)
        {
            var pld = new RestGuildCreatePayload
            {
                Name = name,
                RegionId = region_id,
                DefaultMessageNotifications = default_message_notifications,
                VerificationLevel = verification_level,
                IconBase64 = iconb64
            };

            var route = $"{Endpoints.GUILDS}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var json = JObject.Parse(res.Response);
            var raw_members = (JArray)json["members"];
            var guild = JsonConvert.DeserializeObject<DiscordGuild>(res.Response);

            if (Discord is DiscordClient dc)
            {
                await dc.OnGuildCreateEventAsync(guild, raw_members, null).ConfigureAwait(false);
            }

            return guild;
        }

        internal async Task DeleteGuildAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE).ConfigureAwait(false);

            if (Discord is DiscordClient dc)
            {
                var gld = dc._guilds[guild_id];
                await dc.OnGuildDeleteEventAsync(gld, null).ConfigureAwait(false);
            }
        }

        internal async Task<DiscordGuild> ModifyGuildAsync(ulong guild_id, Optional<string> name,
            Optional<string> region, Optional<VerificationLevel> verification_level,
            Optional<DefaultMessageNotifications> default_message_notifications, Optional<MfaLevel> mfa_level,
            Optional<ExplicitContentFilter> explicit_content_filter, Optional<ulong?> afk_channel_id,
            Optional<int> afk_timeout, Optional<string> iconb64, Optional<ulong> owner_id, Optional<string> splashb64,
            Optional<ulong?> systemChannelId, string reason)
        {
            var pld = new RestGuildModifyPayload
            {
                Name = name,
                RegionId = region,
                VerificationLevel = verification_level,
                DefaultMessageNotifications = default_message_notifications,
                MfaLevel = mfa_level,
                ExplicitContentFilter = explicit_content_filter,
                AfkChannelId = afk_channel_id,
                AfkTimeout = afk_timeout,
                IconBase64 = iconb64,
                SplashBase64 = splashb64,
                OwnerId = owner_id,
                SystemChannelId = systemChannelId
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var json = JObject.Parse(res.Response);
            var raw_members = (JArray)json["members"];
            var guild = JsonConvert.DeserializeObject<DiscordGuild>(res.Response);
            foreach (var r in guild._roles.Values)
            {
                r._guild_id = guild.Id;
            }

            if (Discord is DiscordClient dc)
            {
                await dc.OnGuildUpdateEventAsync(guild, raw_members).ConfigureAwait(false);
            }

            return guild;
        }

        internal async Task<IReadOnlyList<DiscordBan>> GetGuildBansAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.BANS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var bans_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordBan>>(res.Response).Select(xb =>
            {
                var usr = Discord.InternalGetCachedUser(xb.RawUser.Id);
                if (usr == null)
                {
                    usr = new DiscordUser(xb.RawUser) { Discord = Discord };
                    usr = Discord.UserCache.AddOrUpdate(usr.Id, usr, (id, old) =>
                    {
                        old.Username = usr.Username;
                        old.Discriminator = usr.Discriminator;
                        old.AvatarHash = usr.AvatarHash;
                        return old;
                    });
                }

                xb.User = usr;
                return xb;
            });
            var bans = new ReadOnlyCollection<DiscordBan>(new List<DiscordBan>(bans_raw));

            return bans;
        }

        internal Task CreateGuildBanAsync(ulong guild_id, ulong user_id, int delete_message_days, string reason)
        {
            if (delete_message_days < 0 || delete_message_days > 7)
            {
                throw new ArgumentException("Delete message days must be a number between 0 and 7.", nameof(delete_message_days));
            }

            var urlparams = new Dictionary<string, string>
            {
                ["delete-message-days"] = delete_message_days.ToString(CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(reason))
            {
                urlparams["reason"] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.BANS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PUT, route, new { guild_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path, BuildQueryString(urlparams));
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PUT);
        }

        internal Task RemoveGuildBanAsync(ulong guild_id, ulong user_id, string reason)
        {
            var urlparams = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                urlparams["reason"] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.BANS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path, BuildQueryString(urlparams));
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE);
        }

        internal Task LeaveGuildAsync(ulong guild_id)
        {
            var route = $"{Endpoints.USERS}{Endpoints.ME}{Endpoints.GUILDS}/:guild_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE);
        }

        internal async Task<DiscordMember> AddGuildMemberAsync(ulong guild_id, ulong user_id, string access_token, string nick, IEnumerable<DiscordRole> roles, bool muted, bool deafened)
        {
            var pld = new RestGuildMemberAddPayload
            {
                AccessToken = access_token,
                Nickname = nick ?? "",
                Roles = roles ?? new List<DiscordRole>(),
                Deaf = deafened,
                Mute = muted
            };

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PUT, route, new { guild_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PUT, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var tm = JsonConvert.DeserializeObject<TransportMember>(res.Response);

            return new DiscordMember(tm) { Discord = Discord, _guild_id = guild_id };
        }

        internal async Task<IReadOnlyList<TransportMember>> ListGuildMembersAsync(ulong guild_id, int? limit, ulong? after)
        {
            var urlparams = new Dictionary<string, string>();
            if (limit != null && limit > 0)
            {
                urlparams["limit"] = limit.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (after != null)
            {
                urlparams["after"] = after.Value.ToString(CultureInfo.InvariantCulture);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path, urlparams.Any() ? BuildQueryString(urlparams) : "");
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var members_raw = JsonConvert.DeserializeObject<List<TransportMember>>(res.Response);
            return new ReadOnlyCollection<TransportMember>(members_raw);
        }

        internal Task AddGuildMemberRoleAsync(ulong guild_id, ulong user_id, ulong role_id, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}/:user_id{Endpoints.ROLES}/:role_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PUT, route, new { guild_id, user_id, role_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PUT, headers);
        }

        internal Task RemoveGuildMemberRoleAsync(ulong guild_id, ulong user_id, ulong role_id, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}/:user_id{Endpoints.ROLES}/:role_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id, user_id, role_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }

        internal Task ModifyGuildChannelPosition(ulong guild_id, IEnumerable<RestGuildChannelReorderPayload> pld, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.CHANNELS}";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld));
        }

        internal Task ModifyGuildRolePosition(ulong guild_id, IEnumerable<RestGuildRoleReorderPayload> pld, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.ROLES}";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld));
        }

        internal async Task<AuditLog> GetAuditLogsAsync(ulong guild_id, int limit, ulong? after, ulong? before, ulong? responsible, int? action_type)
        {
            var urlparams = new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
            };
            if (after != null)
            {
                urlparams["after"] = after?.ToString(CultureInfo.InvariantCulture);
            }

            if (before != null)
            {
                urlparams["before"] = before?.ToString(CultureInfo.InvariantCulture);
            }

            if (responsible != null)
            {
                urlparams["user_id"] = responsible?.ToString(CultureInfo.InvariantCulture);
            }

            if (action_type != null)
            {
                urlparams["action_type"] = action_type?.ToString(CultureInfo.InvariantCulture);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.AUDIT_LOGS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path, urlparams.Any() ? BuildQueryString(urlparams) : "");
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var audit_log_data_raw = JsonConvert.DeserializeObject<AuditLog>(res.Response);

            return audit_log_data_raw;
        }
        #endregion

        #region Channel
        internal async Task<DiscordChannel> CreateGuildChannelAsync(ulong guild_id, string name, ChannelType type, ulong? parent, int? bitrate, int? user_limit, IEnumerable<DiscordOverwriteBuilder> overwrites, bool? nsfw, Optional<int?> perUserRateLimit, string reason)
        {
            var restoverwrites = new List<DiscordRestOverwrite>();
            if (overwrites != null)
            {
                foreach (var ow in overwrites)
                {
                    restoverwrites.Add(ow.Build());
                }
            }

            var pld = new RestChannelCreatePayload
            {
                Name = name,
                Type = type,
                Parent = parent,
                Bitrate = bitrate,
                UserLimit = user_limit,
                PermissionOverwrites = restoverwrites,
                Nsfw = nsfw,
                PerUserRateLimit = perUserRateLimit
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.CHANNELS}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordChannel>(res.Response);
            ret.Discord = Discord;
            foreach (var xo in ret._permission_overwrites)
            {
                xo.Discord = Discord;
                xo._channel_id = ret.Id;
            }

            return ret;
        }

        internal Task ModifyChannelAsync(ulong channel_id, string name, int? position, string topic, bool? nsfw, Optional<ulong?> parent, int? bitrate, int? user_limit, Optional<int?> perUserRateLimit, string reason)
        {
            var pld = new RestChannelModifyPayload
            {
                Name = name,
                Position = position,
                Topic = topic,
                Nsfw = nsfw,
                Parent = parent,
                Bitrate = bitrate,
                UserLimit = user_limit,
                PerUserRateLimit = perUserRateLimit
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld));
        }

        internal async Task<DiscordChannel> GetChannelAsync(ulong channel_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordChannel>(res.Response);
            ret.Discord = Discord;
            foreach (var xo in ret._permission_overwrites)
            {
                xo.Discord = Discord;
                xo._channel_id = ret.Id;
            }

            return ret;
        }

        internal Task DeleteChannelAsync(ulong channel_id, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }

        internal async Task<DiscordMessage> GetMessageAsync(ulong channel_id, ulong message_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id, message_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var ret = PrepareMessage(JObject.Parse(res.Response));

            return ret;
        }

        internal async Task<DiscordMessage> CreateMessageAsync(ulong channel_id, string content, bool? tts, DiscordEmbed embed)
        {
            if (content != null && content.Length >= 2000)
            {
                throw new ArgumentException("Max message length is 2000");
            }

            if (string.IsNullOrEmpty(content) && embed == null)
            {
                throw new ArgumentException("Cannot send empty message");
            }

            if (content == null && embed == null)
            {
                throw new ArgumentException("Message must have text or embed");
            }

            if (embed?.Timestamp != null)
            {
                embed.Timestamp = embed.Timestamp.Value.ToUniversalTime();
            }

            var pld = new RestChannelMessageCreatePayload
            {
                HasContent = content != null,
                Content = content,
                IsTTS = tts,
                HasEmbed = embed != null,
                Embed = embed
            };

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = PrepareMessage(JObject.Parse(res.Response));

            return ret;
        }

        internal async Task<DiscordMessage> UploadFileAsync(ulong channel_id, Stream file_data, string file_name, string content, bool? tts, DiscordEmbed embed)
        {
            var file = new Dictionary<string, Stream> { { file_name, file_data } };

            if (content != null && content.Length >= 2000)
            {
                throw new ArgumentException("Max message length is 2000");
            }

            if (embed?.Timestamp != null)
            {
                embed.Timestamp = embed.Timestamp.Value.ToUniversalTime();
            }

            var values = new Dictionary<string, string>();
            var pld = new RestChannelMessageCreateMultipartPayload
            {
                Embed = embed,
                Content = content,
                IsTTS = tts
            };

            if (!string.IsNullOrEmpty(content) || embed != null || tts == true)
            {
                values["payload_json"] = DiscordJson.SerializeObject(pld);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoMultipartAsync(Discord, bucket, url, RestRequestMethod.POST, values: values, files: file).ConfigureAwait(false);

            var ret = PrepareMessage(JObject.Parse(res.Response));

            return ret;
        }

        internal async Task<DiscordMessage> UploadFilesAsync(ulong channel_id, Dictionary<string, Stream> files, string content, bool? tts, DiscordEmbed embed)
        {
            if (embed?.Timestamp != null)
            {
                embed.Timestamp = embed.Timestamp.Value.ToUniversalTime();
            }

            if (content != null && content.Length >= 2000)
            {
                throw new ArgumentException("Message content length cannot exceed 2000 characters.");
            }

            if (files.Count == 0 && string.IsNullOrEmpty(content) && embed == null)
            {
                throw new ArgumentException("You must specify content, an embed, or at least one file.");
            }

            var values = new Dictionary<string, string>();
            var pld = new RestChannelMessageCreateMultipartPayload
            {
                Embed = embed,
                Content = content,
                IsTTS = tts
            };
            if (!string.IsNullOrWhiteSpace(content) || embed != null || tts == true)
            {
                values["payload_json"] = DiscordJson.SerializeObject(pld);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoMultipartAsync(Discord, bucket, url, RestRequestMethod.POST, values: values, files: files).ConfigureAwait(false);

            var ret = PrepareMessage(JObject.Parse(res.Response));

            return ret;
        }

        internal async Task<IReadOnlyList<DiscordChannel>> GetGuildChannelsAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.CHANNELS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var channels_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordChannel>>(res.Response).Select(xc => { xc.Discord = Discord; return xc; });

            foreach (var ret in channels_raw)
            {
                foreach (var xo in ret._permission_overwrites)
                {
                    xo.Discord = Discord;
                    xo._channel_id = ret.Id;
                }
            }

            return new ReadOnlyCollection<DiscordChannel>(new List<DiscordChannel>(channels_raw));
        }

        internal async Task<IReadOnlyList<DiscordMessage>> GetChannelMessagesAsync(ulong channel_id, int limit, ulong? before, ulong? after, ulong? around)
        {
            var urlparams = new Dictionary<string, string>();
            if (around != null)
            {
                urlparams["around"] = around?.ToString(CultureInfo.InvariantCulture);
            }

            if (before != null)
            {
                urlparams["before"] = before?.ToString(CultureInfo.InvariantCulture);
            }

            if (after != null)
            {
                urlparams["after"] = after?.ToString(CultureInfo.InvariantCulture);
            }

            if (limit > 0)
            {
                urlparams["limit"] = limit.ToString(CultureInfo.InvariantCulture);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path, urlparams.Any() ? BuildQueryString(urlparams) : "");
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var msgs_raw = JArray.Parse(res.Response);
            var msgs = new List<DiscordMessage>();
            foreach (var xj in msgs_raw)
            {
                msgs.Add(PrepareMessage(xj));
            }

            return new ReadOnlyCollection<DiscordMessage>(new List<DiscordMessage>(msgs));
        }

        internal async Task<DiscordMessage> GetChannelMessageAsync(ulong channel_id, ulong message_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id, message_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var ret = PrepareMessage(JObject.Parse(res.Response));

            return ret;
        }

        internal async Task<DiscordMessage> EditMessageAsync(ulong channel_id, ulong message_id, Optional<string> content, Optional<DiscordEmbed> embed)
        {
            if (embed.HasValue && embed.Value != null && embed.Value.Timestamp != null)
            {
                embed.Value.Timestamp = embed.Value.Timestamp.Value.ToUniversalTime();
            }

            var pld = new RestChannelMessageEditPayload
            {
                HasContent = content.HasValue,
                Content = content.HasValue ? (string)content : null,
                HasEmbed = embed.HasValue,
                Embed = embed.HasValue ? (DiscordEmbed)embed : null
            };

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { channel_id, message_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = PrepareMessage(JObject.Parse(res.Response));

            return ret;
        }

        internal Task DeleteMessageAsync(ulong channel_id, ulong message_id, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id, message_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }

        internal Task DeleteMessagesAsync(ulong channel_id, IEnumerable<ulong> message_ids, string reason)
        {
            var pld = new RestChannelMessageBulkDeletePayload
            {
                Messages = message_ids
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}{Endpoints.BULK_DELETE}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, headers, DiscordJson.SerializeObject(pld));
        }

        internal async Task<IReadOnlyList<DiscordInvite>> GetChannelInvitesAsync(ulong channel_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.INVITES}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var invites_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordInvite>>(res.Response).Select(xi => { xi.Discord = Discord; return xi; });

            return new ReadOnlyCollection<DiscordInvite>(new List<DiscordInvite>(invites_raw));
        }

        internal async Task<DiscordInvite> CreateChannelInviteAsync(ulong channel_id, int max_age, int max_uses, bool temporary, bool unique, string reason)
        {
            var pld = new RestChannelInviteCreatePayload
            {
                MaxAge = max_age,
                MaxUses = max_uses,
                Temporary = temporary,
                Unique = unique
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.INVITES}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordInvite>(res.Response);
            ret.Discord = Discord;

            return ret;
        }

        internal Task DeleteChannelPermissionAsync(ulong channel_id, ulong overwrite_id, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.PERMISSIONS}/:overwrite_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id, overwrite_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }

        internal Task EditChannelPermissionsAsync(ulong channel_id, ulong overwrite_id, Permissions allow, Permissions deny, string type, string reason)
        {
            var pld = new RestChannelPermissionEditPayload
            {
                Type = type,
                Allow = allow & PermissionMethods.FULL_PERMS,
                Deny = deny & PermissionMethods.FULL_PERMS
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers.Add(REASON_HEADER_NAME, reason);
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.PERMISSIONS}/:overwrite_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PUT, route, new { channel_id, overwrite_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PUT, headers, DiscordJson.SerializeObject(pld));
        }

        internal Task TriggerTypingAsync(ulong channel_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.TYPING}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST);
        }

        internal async Task<IReadOnlyList<DiscordMessage>> GetPinnedMessagesAsync(ulong channel_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.PINS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var msgs_raw = JArray.Parse(res.Response);
            var msgs = new List<DiscordMessage>();
            foreach (var xj in msgs_raw)
            {
                msgs.Add(PrepareMessage(xj));
            }

            return new ReadOnlyCollection<DiscordMessage>(new List<DiscordMessage>(msgs));
        }

        internal Task PinMessageAsync(ulong channel_id, ulong message_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.PINS}/:message_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PUT, route, new { channel_id, message_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PUT);
        }

        internal Task UnpinMessageAsync(ulong channel_id, ulong message_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.PINS}/:message_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id, message_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE);
        }

        internal Task GroupDmAddRecipientAsync(ulong channel_id, ulong user_id, string access_token, string nickname)
        {
            var pld = new RestChannelGroupDmRecipientAddPayload
            {
                AccessToken = access_token,
                Nickname = nickname
            };

            var route = $"{Endpoints.USERS}{Endpoints.ME}{Endpoints.CHANNELS}/:channel_id{Endpoints.RECIPIENTS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PUT, route, new { channel_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PUT, payload: DiscordJson.SerializeObject(pld));
        }

        internal Task GroupDmRemoveRecipientAsync(ulong channel_id, ulong user_id)
        {
            var route = $"{Endpoints.USERS}{Endpoints.ME}{Endpoints.CHANNELS}/:channel_id{Endpoints.RECIPIENTS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE);
        }

        internal async Task<DiscordDmChannel> CreateGroupDmAsync(IEnumerable<string> access_tokens, IDictionary<ulong, string> nicks)
        {
            var pld = new RestUserGroupDmCreatePayload
            {
                AccessTokens = access_tokens,
                Nicknames = nicks
            };

            var route = $"{Endpoints.USERS}{Endpoints.ME}{Endpoints.CHANNELS}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordDmChannel>(res.Response);
            ret.Discord = Discord;

            return ret;
        }

        internal async Task<DiscordDmChannel> CreateDmAsync(ulong recipient_id)
        {
            var pld = new RestUserDmCreatePayload
            {
                Recipient = recipient_id
            };

            var route = $"{Endpoints.USERS}{Endpoints.ME}{Endpoints.CHANNELS}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordDmChannel>(res.Response);
            ret.Discord = Discord;
            ret._recipients = new ConcurrentDictionary<ulong, DiscordUser>() { [recipient_id] = Discord.InternalGetCachedUser(recipient_id) };

            return ret;
        }
        #endregion

        #region Member
        internal Task<DiscordUser> GetCurrentUserAsync()
            => GetUserAsync("@me");

        internal Task<DiscordUser> GetUserAsync(ulong user)
            => GetUserAsync(user.ToString(CultureInfo.InvariantCulture));

        internal async Task<DiscordUser> GetUserAsync(string user_id)
        {
            var route = $"{Endpoints.USERS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { user_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var user_raw = JsonConvert.DeserializeObject<TransportUser>(res.Response);
            var duser = new DiscordUser(user_raw) { Discord = Discord };

            return duser;
        }

        internal async Task<DiscordMember> GetGuildMemberAsync(ulong guild_id, ulong user_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var tm = JsonConvert.DeserializeObject<TransportMember>(res.Response);

            var usr = new DiscordUser(tm.User) { Discord = Discord };
            usr = Discord.UserCache.AddOrUpdate(tm.User.Id, usr, (id, old) =>
            {
                old.Username = usr.Username;
                old.Discriminator = usr.Discriminator;
                old.AvatarHash = usr.AvatarHash;
                return old;
            });

            return new DiscordMember(tm)
            {
                Discord = Discord,
                _guild_id = guild_id
            };
        }

        internal Task RemoveGuildMemberAsync(ulong guild_id, ulong user_id, string reason)
        {
            var urlparams = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(reason))
            {
                urlparams["reason"] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path, BuildQueryString(urlparams));
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE);
        }

        internal async Task<TransportUser> ModifyCurrentUserAsync(string username, Optional<string> base64_avatar)
        {
            var pld = new RestUserUpdateCurrentPayload
            {
                Username = username,
                AvatarBase64 = base64_avatar.HasValue ? base64_avatar.Value : null,
                AvatarSet = base64_avatar.HasValue
            };

            var route = $"{Endpoints.USERS}{Endpoints.ME}";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var user_raw = JsonConvert.DeserializeObject<TransportUser>(res.Response);

            return user_raw;
        }

        internal async Task<IReadOnlyList<DiscordGuild>> GetCurrentUserGuildsAsync(int limit, ulong? before, ulong? after)
        {
            var route = $"{Endpoints.USERS}{Endpoints.ME}{Endpoints.GUILDS}?limit={limit}";

            if (before != null)
            {
                route += "&before=" + before;
            }

            if (after != null)
            {
                route += "&after=" + after;
            }

            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            if (Discord is DiscordClient)
            {
                var guilds_raw = JsonConvert.DeserializeObject<IEnumerable<RestUserGuild>>(res.Response);
                var glds = guilds_raw.Select(xug => (Discord as DiscordClient)?._guilds[xug.Id]);
                return new ReadOnlyCollection<DiscordGuild>(new List<DiscordGuild>(glds));
            }
            else
            {
                var guilds_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordGuild>>(res.Response);

                var glds = new List<DiscordGuild>();
                foreach (var gld in guilds_raw)
                {
                    glds.Add(await GetGuildAsync(gld.Id));
                }

                return new ReadOnlyCollection<DiscordGuild>(new List<DiscordGuild>(glds));
            }
        }

        internal Task ModifyGuildMemberAsync(ulong guild_id, ulong user_id, Optional<string> nick,
            Optional<IEnumerable<ulong>> role_ids, Optional<bool> mute, Optional<bool> deaf,
            Optional<ulong?> voice_channel_id, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var pld = new RestGuildMemberModifyPayload
            {
                Nickname = nick,
                RoleIds = role_ids,
                Deafen = deaf,
                Mute = mute,
                VoiceChannelId = voice_channel_id
            };

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, payload: DiscordJson.SerializeObject(pld));
        }

        internal Task ModifyCurrentMemberNicknameAsync(ulong guild_id, string nick, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var pld = new RestGuildMemberModifyPayload
            {
                Nickname = nick
            };

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.MEMBERS}{Endpoints.ME}{Endpoints.NICK}";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, payload: DiscordJson.SerializeObject(pld));
        }
        #endregion

        #region Roles
        internal async Task<IReadOnlyList<DiscordRole>> GetGuildRolesAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.ROLES}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var roles_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordRole>>(res.Response).Select(xr => { xr.Discord = Discord; xr._guild_id = guild_id; return xr; });

            return new ReadOnlyCollection<DiscordRole>(new List<DiscordRole>(roles_raw));
        }

        internal async Task<DiscordGuild> GetGuildAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var json = JObject.Parse(res.Response);
            var raw_members = (JArray)json["members"];
            var guild_rest = JsonConvert.DeserializeObject<DiscordGuild>(res.Response);
            foreach (var r in guild_rest._roles.Values)
            {
                r._guild_id = guild_rest.Id;
            }

            if (Discord is DiscordClient dc)
            {
                await dc.OnGuildUpdateEventAsync(guild_rest, raw_members).ConfigureAwait(false);
                return dc._guilds[guild_rest.Id];
            }
            else
            {
                guild_rest.Discord = Discord;
                return guild_rest;
            }
        }

        internal async Task<DiscordRole> ModifyGuildRoleAsync(ulong guild_id, ulong role_id, string name, Permissions? permissions, int? color, bool? hoist, bool? mentionable, string reason)
        {
            var pld = new RestGuildRolePayload
            {
                Name = name,
                Permissions = permissions & PermissionMethods.FULL_PERMS,
                Color = color,
                Hoist = hoist,
                Mentionable = mentionable
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.ROLES}/:role_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id, role_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordRole>(res.Response);
            ret.Discord = Discord;
            ret._guild_id = guild_id;

            return ret;
        }

        internal Task DeleteRoleAsync(ulong guild_id, ulong role_id, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.ROLES}/:role_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id, role_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }

        internal async Task<DiscordRole> CreateGuildRole(ulong guild_id, string name, Permissions? permissions, int? color, bool? hoist, bool? mentionable, string reason)
        {
            var pld = new RestGuildRolePayload
            {
                Name = name,
                Permissions = permissions & PermissionMethods.FULL_PERMS,
                Color = color,
                Hoist = hoist,
                Mentionable = mentionable
            };

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.ROLES}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordRole>(res.Response);
            ret.Discord = Discord;
            ret._guild_id = guild_id;

            return ret;
        }
        #endregion

        #region Prune
        internal async Task<int> GetGuildPruneCountAsync(ulong guild_id, int days)
        {
            if (days < 0 || days > 30)
            {
                throw new ArgumentException("Prune inactivity days must be a number between 0 and 7.", nameof(days));
            }

            var urlparams = new Dictionary<string, string>
            {
                ["days"] = days.ToString(CultureInfo.InvariantCulture)
            };

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.PRUNE}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path, BuildQueryString(urlparams));
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var pruned = JsonConvert.DeserializeObject<RestGuildPruneResultPayload>(res.Response);

            return pruned.Pruned;
        }

        internal async Task<int> BeginGuildPruneAsync(ulong guild_id, int days, string reason)
        {
            var urlparams = new Dictionary<string, string>
            {
                ["days"] = days.ToString(CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(reason))
            {
                urlparams["reason"] = reason;
            }

            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.PRUNE}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path, BuildQueryString(urlparams));
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST).ConfigureAwait(false);

            var pruned = JsonConvert.DeserializeObject<RestGuildPruneResultPayload>(res.Response);

            return pruned.Pruned;
        }
        #endregion

        #region GuildVarious
        internal async Task<IReadOnlyList<DiscordIntegration>> GetGuildIntegrationsAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.INTEGRATIONS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var integrations_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordIntegration>>(res.Response).Select(xi => { xi.Discord = Discord; return xi; });

            return new ReadOnlyCollection<DiscordIntegration>(new List<DiscordIntegration>(integrations_raw));
        }

        internal async Task<DiscordIntegration> CreateGuildIntegrationAsync(ulong guild_id, string type, ulong id)
        {
            var pld = new RestGuildIntegrationAttachPayload
            {
                Type = type,
                Id = id
            };

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.INTEGRATIONS}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordIntegration>(res.Response);
            ret.Discord = Discord;

            return ret;
        }

        internal async Task<DiscordIntegration> ModifyGuildIntegrationAsync(ulong guild_id, ulong integration_id, int expire_behaviour, int expire_grace_period, bool enable_emoticons)
        {
            var pld = new RestGuildIntegrationModifyPayload
            {
                ExpireBehavior = expire_behaviour,
                ExpireGracePeriod = expire_grace_period,
                EnableEmoticons = enable_emoticons
            };

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.INTEGRATIONS}/:integration_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id, integration_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordIntegration>(res.Response);
            ret.Discord = Discord;

            return ret;
        }

        internal Task DeleteGuildIntegrationAsync(ulong guild_id, DiscordIntegration integration)
        {
            var pld = integration;

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.INTEGRATIONS}/:integration_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id, integration_id = integration.Id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, payload: DiscordJson.SerializeObject(integration));
        }

        internal Task SyncGuildIntegrationAsync(ulong guild_id, ulong integration_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.INTEGRATIONS}/:integration_id{Endpoints.SYNC}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { guild_id, integration_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST);
        }

        internal async Task<DiscordGuildEmbed> GetGuildEmbedAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.EMBED}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var embed = JsonConvert.DeserializeObject<DiscordGuildEmbed>(res.Response);

            return embed;
        }

        internal async Task<DiscordGuildEmbed> ModifyGuildEmbedAsync(ulong guild_id, DiscordGuildEmbed embed)
        {
            var pld = embed;

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.EMBED}";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, payload: DiscordJson.SerializeObject(embed)).ConfigureAwait(false);

            var embed_rest = JsonConvert.DeserializeObject<DiscordGuildEmbed>(res.Response);

            return embed_rest;
        }

        internal async Task<IReadOnlyList<DiscordVoiceRegion>> GetGuildVoiceRegionsAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.REGIONS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var regions_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordVoiceRegion>>(res.Response);

            return new ReadOnlyCollection<DiscordVoiceRegion>(new List<DiscordVoiceRegion>(regions_raw));
        }

        internal async Task<IReadOnlyList<DiscordInvite>> GetGuildInvitesAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.INVITES}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var invites_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordInvite>>(res.Response).Select(xi => { xi.Discord = Discord; return xi; });

            return new ReadOnlyCollection<DiscordInvite>(new List<DiscordInvite>(invites_raw));
        }
        #endregion

        #region Invite
        internal async Task<DiscordInvite> GetInviteAsync(string invite_code)
        {
            var route = $"{Endpoints.INVITES}/:invite_code";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { invite_code }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordInvite>(res.Response);
            ret.Discord = Discord;

            return ret;
        }

        internal async Task<DiscordInvite> DeleteInviteAsync(string invite_code, string reason)
        {
            var headers = Utilities.GetBaseHeaders();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.INVITES}/:invite_code";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { invite_code }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordInvite>(res.Response);
            ret.Discord = Discord;

            return ret;
        }

        /* 
         * Disabled due to API restrictions
         * 
         * internal async Task<DiscordInvite> InternalAcceptInvite(string invite_code)
         * {
         *     this.Discord.DebugLogger.LogMessage(LogLevel.Warning, "REST API", "Invite accept endpoint was used; this account is now likely unverified", DateTime.Now);
         *     
         *     var url = new Uri($"{Utils.GetApiBaseUri(), Endpoints.INVITES}/{invite_code));
         *     var bucket = this.Rest.GetBucket(0, MajorParameterType.Unbucketed, url, HttpRequestMethod.POST);
         *     var res = await this.DoRequestAsync(this.Discord, bucket, url, HttpRequestMethod.POST).ConfigureAwait(false);
         *     
         *     var ret = JsonConvert.DeserializeObject<DiscordInvite>(res.Response);
         *     ret.Discord = this.Discord;
         * 
         *     return ret;
         * }
         */
        #endregion

        #region Connections
        internal async Task<IReadOnlyList<DiscordConnection>> GetUsersConnectionsAsync()
        {
            var route = $"{Endpoints.USERS}{Endpoints.ME}{Endpoints.CONNECTIONS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var connections_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordConnection>>(res.Response).Select(xc => { xc.Discord = Discord; return xc; });

            return new ReadOnlyCollection<DiscordConnection>(new List<DiscordConnection>(connections_raw));
        }
        #endregion

        #region Voice
        internal async Task<IReadOnlyList<DiscordVoiceRegion>> ListVoiceRegionsAsync()
        {
            var route = $"{Endpoints.VOICE}{Endpoints.REGIONS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var regions = JsonConvert.DeserializeObject<IEnumerable<DiscordVoiceRegion>>(res.Response);

            return new ReadOnlyCollection<DiscordVoiceRegion>(new List<DiscordVoiceRegion>(regions));
        }
        #endregion

        #region Webhooks
        internal async Task<DiscordWebhook> CreateWebhookAsync(ulong channel_id, string name, Optional<string> base64_avatar, string reason)
        {
            var pld = new RestWebhookPayload
            {
                Name = name,
                AvatarBase64 = base64_avatar.HasValue ? base64_avatar.Value : null,
                AvatarSet = base64_avatar.HasValue
            };

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.WEBHOOKS}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordWebhook>(res.Response);
            ret.Discord = Discord;
            ret.ApiClient = this;

            return ret;
        }

        internal async Task<IReadOnlyList<DiscordWebhook>> GetChannelWebhooksAsync(ulong channel_id)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.WEBHOOKS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var webhooks_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordWebhook>>(res.Response).Select(xw => { xw.Discord = Discord; xw.ApiClient = this; return xw; });

            return new ReadOnlyCollection<DiscordWebhook>(new List<DiscordWebhook>(webhooks_raw));
        }

        internal async Task<IReadOnlyList<DiscordWebhook>> GetGuildWebhooksAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.WEBHOOKS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var webhooks_raw = JsonConvert.DeserializeObject<IEnumerable<DiscordWebhook>>(res.Response).Select(xw => { xw.Discord = Discord; xw.ApiClient = this; return xw; });

            return new ReadOnlyCollection<DiscordWebhook>(new List<DiscordWebhook>(webhooks_raw));
        }

        internal async Task<DiscordWebhook> GetWebhookAsync(ulong webhook_id)
        {
            var route = $"{Endpoints.WEBHOOKS}/:webhook_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { webhook_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordWebhook>(res.Response);
            ret.Discord = Discord;
            ret.ApiClient = this;

            return ret;
        }

        // Auth header not required
        internal async Task<DiscordWebhook> GetWebhookWithTokenAsync(ulong webhook_id, string webhook_token)
        {
            var route = $"{Endpoints.WEBHOOKS}/:webhook_id/:webhook_token";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { webhook_id, webhook_token }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordWebhook>(res.Response);
            ret.Token = webhook_token;
            ret.Id = webhook_id;
            ret.Discord = Discord;
            ret.ApiClient = this;

            return ret;
        }

        internal async Task<DiscordWebhook> ModifyWebhookAsync(ulong webhook_id, string name, Optional<string> base64_avatar, string reason)
        {
            var pld = new RestWebhookPayload
            {
                Name = name,
                AvatarBase64 = base64_avatar.HasValue ? base64_avatar.Value : null,
                AvatarSet = base64_avatar.HasValue
            };

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.WEBHOOKS}/:webhook_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { webhook_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordWebhook>(res.Response);
            ret.Discord = Discord;
            ret.ApiClient = this;

            return ret;
        }

        internal async Task<DiscordWebhook> ModifyWebhookAsync(ulong webhook_id, string name, string base64_avatar, string webhook_token, string reason)
        {
            var pld = new RestWebhookPayload
            {
                Name = name,
                AvatarBase64 = base64_avatar
            };

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.WEBHOOKS}/:webhook_id/:webhook_token";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { webhook_id, webhook_token }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<DiscordWebhook>(res.Response);
            ret.Discord = Discord;
            ret.ApiClient = this;

            return ret;
        }

        internal Task DeleteWebhookAsync(ulong webhook_id, string reason)
        {
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.WEBHOOKS}/:webhook_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { webhook_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }

        internal Task DeleteWebhookAsync(ulong webhook_id, string webhook_token, string reason)
        {
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.WEBHOOKS}/:webhook_id/:webhook_token";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { webhook_id, webhook_token }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }

        internal Task ExecuteWebhookAsync(ulong webhook_id, string webhook_token, string content, string username, string avatar_url, bool? tts, IEnumerable<DiscordEmbed> embeds)
        {
            if (embeds != null)
            {
                foreach (var embed in embeds)
                {
                    if (embed.Timestamp != null)
                    {
                        embed.Timestamp = embed.Timestamp.Value.ToUniversalTime();
                    }
                }
            }

            var pld = new RestWebhookExecutePayload
            {
                Content = content,
                Username = username,
                AvatarUrl = avatar_url,
                IsTTS = tts,
                Embeds = embeds
            };

            var route = $"{Endpoints.WEBHOOKS}/:webhook_id/:webhook_token";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { webhook_id, webhook_token }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld));
        }

        internal Task ExecuteWebhookSlackAsync(ulong webhook_id, string webhook_token, string json_payload)
        {
            var route = $"{Endpoints.WEBHOOKS}/:webhook_id/:webhook_token{Endpoints.SLACK}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { webhook_id, webhook_token }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: json_payload);
        }

        internal Task ExecuteWebhookGithubAsync(ulong webhook_id, string webhook_token, string json_payload)
        {
            var route = $"{Endpoints.WEBHOOKS}/:webhook_id/:webhook_token{Endpoints.GITHUB}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { webhook_id, webhook_token }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: json_payload);
        }
        #endregion

        #region Reactions
        internal Task CreateReactionAsync(ulong channel_id, ulong message_id, string emoji)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id{Endpoints.REACTIONS}/:emoji{Endpoints.ME}";
            var bucket = Rest.GetBucket(RestRequestMethod.PUT, route, new { channel_id, message_id, emoji }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.PUT, ratelimit_wait_override: 0.26);
        }

        internal Task DeleteOwnReactionAsync(ulong channel_id, ulong message_id, string emoji)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id{Endpoints.REACTIONS}/:emoji{Endpoints.ME}";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id, message_id, emoji }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, ratelimit_wait_override: 0.26);
        }

        internal Task DeleteUserReactionAsync(ulong channel_id, ulong message_id, ulong user_id, string emoji, string reason)
        {
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id{Endpoints.REACTIONS}/:emoji/:user_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id, message_id, emoji, user_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers, ratelimit_wait_override: 0.26);
        }

        internal async Task<IReadOnlyList<DiscordUser>> GetReactionsAsync(ulong channel_id, ulong message_id, string emoji)
        {
            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id{Endpoints.REACTIONS}/:emoji";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { channel_id, message_id, emoji }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var reacters_raw = JsonConvert.DeserializeObject<IEnumerable<TransportUser>>(res.Response);
            var reacters = new List<DiscordUser>();
            foreach (var xr in reacters_raw)
            {
                var usr = new DiscordUser(xr) { Discord = Discord };
                usr = Discord.UserCache.AddOrUpdate(xr.Id, usr, (id, old) =>
                {
                    old.Username = usr.Username;
                    old.Discriminator = usr.Discriminator;
                    old.AvatarHash = usr.AvatarHash;
                    return old;
                });

                reacters.Add(usr);
            }

            return new ReadOnlyCollection<DiscordUser>(new List<DiscordUser>(reacters));
        }

        internal Task DeleteAllReactionsAsync(ulong channel_id, ulong message_id, string reason)
        {
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/:message_id{Endpoints.REACTIONS}";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { channel_id, message_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers, ratelimit_wait_override: 0.26);
        }
        #endregion

        #region Emoji
        internal async Task<IReadOnlyList<DiscordGuildEmoji>> GetGuildEmojisAsync(ulong guild_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.EMOJIS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var emojis_raw = JsonConvert.DeserializeObject<IEnumerable<JObject>>(res.Response);

            Discord.Guilds.TryGetValue(guild_id, out var gld);
            var users = new Dictionary<ulong, DiscordUser>();
            var emojis = new List<DiscordGuildEmoji>();
            foreach (var xj in emojis_raw)
            {
                var xge = xj.ToObject<DiscordGuildEmoji>();
                xge.Guild = gld;

                var xtu = xj["user"]?.ToObject<TransportUser>();
                if (xtu != null)
                {
                    if (!users.ContainsKey(xtu.Id))
                    {
                        var xu = gld != null && gld.Members.TryGetValue(xtu.Id, out var member) ? member : new DiscordUser(xtu);
                        users[xu.Id] = xu;
                    }

                    xge.User = users[xtu.Id];
                }

                emojis.Add(xge);
            }

            return new ReadOnlyCollection<DiscordGuildEmoji>(emojis);
        }

        internal async Task<DiscordGuildEmoji> GetGuildEmojiAsync(ulong guild_id, ulong emoji_id)
        {
            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.EMOJIS}/:emoji_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { guild_id, emoji_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            Discord.Guilds.TryGetValue(guild_id, out var gld);

            var emoji_raw = JObject.Parse(res.Response);
            var emoji = emoji_raw.ToObject<DiscordGuildEmoji>();
            emoji.Guild = gld;

            var xtu = emoji_raw["user"]?.ToObject<TransportUser>();
            if (xtu != null)
            {
                emoji.User = gld != null && gld.Members.TryGetValue(xtu.Id, out var member) ? member : new DiscordUser(xtu);
            }

            return emoji;
        }

        internal async Task<DiscordGuildEmoji> CreateGuildEmojiAsync(ulong guild_id, string name, string imageb64, IEnumerable<ulong> roles, string reason)
        {
            var pld = new RestGuildEmojiCreatePayload
            {
                Name = name,
                ImageB64 = imageb64,
                Roles = roles?.ToArray()
            };

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.EMOJIS}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            Discord.Guilds.TryGetValue(guild_id, out var gld);

            var emoji_raw = JObject.Parse(res.Response);
            var emoji = emoji_raw.ToObject<DiscordGuildEmoji>();
            emoji.Guild = gld;

            var xtu = emoji_raw["user"]?.ToObject<TransportUser>();
            if (xtu != null)
            {
                emoji.User = gld != null && gld.Members.TryGetValue(xtu.Id, out var member) ? member : new DiscordUser(xtu);
            }
            else
            {
                emoji.User = Discord.CurrentUser;
            }

            return emoji;
        }

        internal async Task<DiscordGuildEmoji> ModifyGuildEmojiAsync(ulong guild_id, ulong emoji_id, string name, IEnumerable<ulong> roles, string reason)
        {
            var pld = new RestGuildEmojiModifyPayload
            {
                Name = name,
                Roles = roles?.ToArray()
            };

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.EMOJIS}/:emoji_id";
            var bucket = Rest.GetBucket(RestRequestMethod.PATCH, route, new { guild_id, emoji_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.PATCH, headers, DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            Discord.Guilds.TryGetValue(guild_id, out var gld);

            var emoji_raw = JObject.Parse(res.Response);
            var emoji = emoji_raw.ToObject<DiscordGuildEmoji>();
            emoji.Guild = gld;

            var xtu = emoji_raw["user"]?.ToObject<TransportUser>();
            if (xtu != null)
            {
                emoji.User = gld != null && gld.Members.TryGetValue(xtu.Id, out var member) ? member : new DiscordUser(xtu);
            }

            return emoji;
        }

        internal Task DeleteGuildEmojiAsync(ulong guild_id, ulong emoji_id, string reason)
        {
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(reason))
            {
                headers[REASON_HEADER_NAME] = reason;
            }

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.EMOJIS}/:emoji_id";
            var bucket = Rest.GetBucket(RestRequestMethod.DELETE, route, new { guild_id, emoji_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            return DoRequestAsync(Discord, bucket, url, RestRequestMethod.DELETE, headers);
        }
        #endregion

        #region Misc
        internal Task<DiscordApplication> GetCurrentApplicationInfoAsync()
            => GetApplicationInfoAsync("@me");

        internal Task<DiscordApplication> GetApplicationInfoAsync(ulong id)
            => GetApplicationInfoAsync(id.ToString(CultureInfo.InvariantCulture));

        private async Task<DiscordApplication> GetApplicationInfoAsync(string app_id)
        {
            var route = $"{Endpoints.OAUTH2}{Endpoints.APPLICATIONS}/:app_id";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { app_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var app = JsonConvert.DeserializeObject<DiscordApplication>(res.Response);
            app.Discord = Discord;

            return app;
        }

        internal async Task<IReadOnlyList<DiscordApplicationAsset>> GetApplicationAssetsAsync(DiscordApplication app)
        {
            var route = $"{Endpoints.OAUTH2}{Endpoints.APPLICATIONS}/:app_id{Endpoints.ASSETS}";
            var bucket = Rest.GetBucket(RestRequestMethod.GET, route, new { app_id = app.Id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.GET).ConfigureAwait(false);

            var assets = JsonConvert.DeserializeObject<IEnumerable<DiscordApplicationAsset>>(res.Response);
            foreach (var asset in assets)
            {
                asset.Discord = app.Discord;
                asset.Application = app;
            }

            return new ReadOnlyCollection<DiscordApplicationAsset>(new List<DiscordApplicationAsset>(assets));
        }

        internal async Task AcknowledgeMessageAsync(ulong msg_id, ulong channel_id)
        {
            await TokenSemaphore.WaitAsync().ConfigureAwait(false);

            var pld = new AcknowledgePayload
            {
                Token = LastAckToken
            };

            var route = $"{Endpoints.CHANNELS}/:channel_id{Endpoints.MESSAGES}/{msg_id}{Endpoints.ACK}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { channel_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            var ret = JsonConvert.DeserializeObject<AcknowledgePayload>(res.Response);
            LastAckToken = ret.Token;

            TokenSemaphore.Release();
        }

        internal async Task AcknowledgeGuildAsync(ulong guild_id)
        {
            await TokenSemaphore.WaitAsync().ConfigureAwait(false);

            var pld = new AcknowledgePayload
            {
                Token = LastAckToken
            };

            var route = $"{Endpoints.GUILDS}/:guild_id{Endpoints.ACK}";
            var bucket = Rest.GetBucket(RestRequestMethod.POST, route, new { guild_id }, out var path);

            var url = Utilities.GetApiUriFor(path);
            var res = await DoRequestAsync(Discord, bucket, url, RestRequestMethod.POST, payload: DiscordJson.SerializeObject(pld)).ConfigureAwait(false);

            if (res.ResponseCode != 204)
            {
                var ret = JsonConvert.DeserializeObject<AcknowledgePayload>(res.Response);
                LastAckToken = ret.Token;
            }
            else
            {
                LastAckToken = null;
            }

            TokenSemaphore.Release();
        }
        #endregion
    }
}
