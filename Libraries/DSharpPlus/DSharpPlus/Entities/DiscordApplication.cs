﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DSharpPlus.Entities
{
    /// <summary>
    /// Represents an OAuth2 application.
    /// </summary>
    public sealed class DiscordApplication : SnowflakeObject, IEquatable<DiscordApplication>
    {
        /// <summary>
        /// Gets the application's description.
        /// </summary>
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the application's icon.
        /// </summary>
        public string Icon
            => !string.IsNullOrWhiteSpace(IconHash) ? $"https://cdn.discordapp.com/app-icons/{Id.ToString(CultureInfo.InvariantCulture)}/{IconHash}.png?size=1024" : null;

        [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
        internal string IconHash { get; set; }

        /// <summary>
        /// Gets the application's name.
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the application's allowed RPC origins.
        /// </summary>
        [JsonProperty("rpc_origins", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string> RpcOrigins { get; internal set; }

        /// <summary>
        /// Gets the application's flags.
        /// </summary>
        [JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)]
        public int Flags { get; internal set; }

        /// <summary>
        /// Gets the application's owner.
        /// </summary>
        [JsonProperty("owner", NullValueHandling = NullValueHandling.Ignore)]
        public DiscordUser Owner { get; internal set; }

        /// <summary>
        /// Gets whether this application's bot user requires code grant.
        /// </summary>
        [JsonProperty("bot_require_code_grant", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RequiresCodeGrant { get; internal set; }

        /// <summary>
        /// Gets whether this bot application is public.
        /// </summary>
        [JsonProperty("bot_public", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPublic { get; internal set; }

        /// <summary>
        /// Gets the hash of the application's cover image.
        /// </summary>
        [JsonProperty("cover_image")]
        internal string CoverImageHash { get; set; }

        /// <summary>
        /// Gets this application's cover image URL.
        /// </summary>
        [JsonIgnore]
        public string CoverImageUrl
            => $"https://cdn.discordapp.com/app-icons/{Id.ToString(CultureInfo.InvariantCulture)}/{CoverImageHash}.png?size=1024";

        [JsonIgnore]
        private IReadOnlyList<DiscordApplicationAsset> Assets { get; set; }

        internal DiscordApplication() { }

        /// <summary>
        /// Gets the application's cover image URL, in requested format and size.
        /// </summary>
        /// <param name="fmt">Format of the image to get.</param>
        /// <param name="size">Maximum size of the cover image. Must be a power of two, minimum 16, maximum 2048.</param>
        /// <returns>URL of the application's cover image.</returns>
        public string GetAvatarUrl(ImageFormat fmt, ushort size = 1024)
        {
            if (fmt == ImageFormat.Unknown)
            {
                throw new ArgumentException("You must specify valid image format.", nameof(fmt));
            }

            if (size < 16 || size > 2048)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            var log = Math.Log(size, 2);
            if (log < 4 || log > 11 || log % 1 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            var sfmt = "";
            switch (fmt)
            {
                case ImageFormat.Gif:
                    sfmt = "gif";
                    break;

                case ImageFormat.Jpeg:
                    sfmt = "jpg";
                    break;

                case ImageFormat.Png:
                    sfmt = "png";
                    break;

                case ImageFormat.WebP:
                    sfmt = "webp";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(fmt));
            }

            var ssize = size.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(CoverImageHash))
            {
                var id = Id.ToString(CultureInfo.InvariantCulture);
                return $"https://cdn.discordapp.com/avatars/{id}/{CoverImageHash}.{sfmt}?size={ssize}";
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves this application's assets.
        /// </summary>
        /// <returns>This application's assets.</returns>
        public async Task<IReadOnlyList<DiscordApplicationAsset>> GetAssetsAsync()
        {
            if (Assets == null)
            {
                Assets = await Discord.ApiClient.GetApplicationAssetsAsync(this).ConfigureAwait(false);
            }

            return Assets;
        }

        public string GenerateBotOAuth(Permissions permissions = Permissions.None)
        {
            permissions &= PermissionMethods.FULL_PERMS;
            // Split it up so it isn't annoying and blue
            // 
            // :blobthonkang: -emzi
            return "https://" + $"discordapp.com/oauth2/authorize?client_id={Id.ToString(CultureInfo.InvariantCulture)}&scope=bot&permissions={((long)permissions).ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Checks whether this <see cref="DiscordApplication"/> is equal to another object.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>Whether the object is equal to this <see cref="DiscordApplication"/>.</returns>
        public override bool Equals(object obj) => Equals(obj as DiscordApplication);

        /// <summary>
        /// Checks whether this <see cref="DiscordApplication"/> is equal to another <see cref="DiscordApplication"/>.
        /// </summary>
        /// <param name="e"><see cref="DiscordApplication"/> to compare to.</param>
        /// <returns>Whether the <see cref="DiscordApplication"/> is equal to this <see cref="DiscordApplication"/>.</returns>
        public bool Equals(DiscordApplication e)
        {
            if (e is null)
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
        /// Gets the hash code for this <see cref="DiscordApplication"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="DiscordApplication"/>.</returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Gets whether the two <see cref="DiscordApplication"/> objects are equal.
        /// </summary>
        /// <param name="e1">First application to compare.</param>
        /// <param name="e2">Second application to compare.</param>
        /// <returns>Whether the two applications are equal.</returns>
        public static bool operator ==(DiscordApplication e1, DiscordApplication e2)
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
        /// Gets whether the two <see cref="DiscordApplication"/> objects are not equal.
        /// </summary>
        /// <param name="e1">First application to compare.</param>
        /// <param name="e2">Second application to compare.</param>
        /// <returns>Whether the two applications are not equal.</returns>
        public static bool operator !=(DiscordApplication e1, DiscordApplication e2)
            => !(e1 == e2);
    }

    public abstract class DiscordAsset
    {
        /// <summary>
        /// Gets the ID of this asset.
        /// </summary>
        public virtual string Id { get; set; }

        /// <summary>
        /// Gets the URL of this asset.
        /// </summary>
        public abstract Uri Url { get; }
    }

    /// <summary>
    /// Represents an asset for an OAuth2 application.
    /// </summary>
    public sealed class DiscordApplicationAsset : DiscordAsset, IEquatable<DiscordApplicationAsset>
    {
        /// <summary>
        /// Gets the Discord client instance for this asset.
        /// </summary>
        internal BaseDiscordClient Discord { get; set; }

        /// <summary>
        /// Gets the asset's name.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the asset's type.
        /// </summary>
        [JsonProperty("type")]
        public ApplicationAssetType Type { get; internal set; }

        /// <summary>
        /// Gets the application this asset belongs to.
        /// </summary>
        public DiscordApplication Application { get; internal set; }

        /// <summary>
        /// Gets the Url of this asset.
        /// </summary>
        public override Uri Url
            => new Uri($"https://cdn.discordapp.com/app-assets/{Application.Id.ToString(CultureInfo.InvariantCulture)}/{Id}.png");

        internal DiscordApplicationAsset() { }

        internal DiscordApplicationAsset(DiscordApplication app)
        {
            Discord = app.Discord;
        }

        /// <summary>
        /// Checks whether this <see cref="DiscordApplicationAsset"/> is equal to another object.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>Whether the object is equal to this <see cref="DiscordApplicationAsset"/>.</returns>
        public override bool Equals(object obj) => Equals(obj as DiscordApplicationAsset);

        /// <summary>
        /// Checks whether this <see cref="DiscordApplicationAsset"/> is equal to another <see cref="DiscordApplicationAsset"/>.
        /// </summary>
        /// <param name="e"><see cref="DiscordApplicationAsset"/> to compare to.</param>
        /// <returns>Whether the <see cref="DiscordApplicationAsset"/> is equal to this <see cref="DiscordApplicationAsset"/>.</returns>
        public bool Equals(DiscordApplicationAsset e)
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
        /// Gets the hash code for this <see cref="DiscordApplication"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="DiscordApplication"/>.</returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Gets whether the two <see cref="DiscordApplicationAsset"/> objects are equal.
        /// </summary>
        /// <param name="e1">First application asset to compare.</param>
        /// <param name="e2">Second application asset to compare.</param>
        /// <returns>Whether the two application assets not equal.</returns>
        public static bool operator ==(DiscordApplicationAsset e1, DiscordApplicationAsset e2)
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
        /// Gets whether the two <see cref="DiscordApplicationAsset"/> objects are not equal.
        /// </summary>
        /// <param name="e1">First application asset to compare.</param>
        /// <param name="e2">Second application asset to compare.</param>
        /// <returns>Whether the two application assets are not equal.</returns>
        public static bool operator !=(DiscordApplicationAsset e1, DiscordApplicationAsset e2)
            => !(e1 == e2);
    }

    public sealed class DiscordSpotifyAsset : DiscordAsset
    {
        /// <summary>
        /// Gets the URL of this asset.
        /// </summary>
        public override Uri Url
            => _url.Value;

        private Lazy<Uri> _url;

        public DiscordSpotifyAsset()
        {
            _url = new Lazy<Uri>(() =>
            {
                var ids = Id.Split(':');
                var id = ids[1];
                return new Uri($"https://i.scdn.co/image/{id}");
            });
        }
    }

    /// <summary>
    /// Determines the type of the asset attached to the application.
    /// </summary>
    public enum ApplicationAssetType : int
    {
        /// <summary>
        /// Unknown type. This indicates something went terribly wrong.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// This asset can be used as small image for rich presences.
        /// </summary>
        SmallImage = 1,

        /// <summary>
        /// This asset can be used as large image for rich presences.
        /// </summary>
        LargeImage = 2
    }
}
