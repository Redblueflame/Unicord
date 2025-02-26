﻿using System;
using System.Collections.Generic;

namespace DSharpPlus.Net
{
    /// <summary>
    /// Represents a non-multipart HTTP request.
    /// </summary>
    public sealed class RestRequest : BaseRestRequest
    {
        /// <summary>
        /// Gets the payload sent with this request.
        /// </summary>
        public string Payload { get; }

        internal RestRequest(BaseDiscordClient client, RateLimitBucket bucket, Uri url, RestRequestMethod method, IDictionary<string, string> headers = null, string payload = null, double? ratelimit_wait_override = null)
            : base(client, bucket, url, method, headers, ratelimit_wait_override)
        {
            Payload = payload;
        }
    }
}
