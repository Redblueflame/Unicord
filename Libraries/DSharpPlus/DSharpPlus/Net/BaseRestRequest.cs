﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DSharpPlus.Net
{
    /// <summary>
    /// Represents a request sent over HTTP.
    /// </summary>
    public abstract class BaseRestRequest
    {
        protected internal BaseDiscordClient Discord { get; }
        protected internal TaskCompletionSource<RestResponse> RequestTaskSource { get; }

        /// <summary>
        /// Gets the url to which this request is going to be made.
        /// </summary>
        public Uri Url { get; }

        /// <summary>
        /// Gets the HTTP method used for this request.
        /// </summary>
        public RestRequestMethod Method { get; }

        /// <summary>
        /// Gets the headers sent with this request.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets the override for the rate limit bucket wait time.
        /// </summary>
        public double? RateLimitWaitOverride { get; }

        /// <summary>
        /// Gets the rate limit bucket this request is in.
        /// </summary>
        internal RateLimitBucket RateLimitBucket { get; }

        /// <summary>
        /// Creates a new <see cref="BaseRestRequest"/> with specified parameters.
        /// </summary>
        /// <param name="client"><see cref="DiscordClient"/> from which this request originated.</param>
        /// <param name="bucket">Rate limit bucket to place this request in.</param>
        /// <param name="url">Uri to which this request is going to be sent to.</param>
        /// <param name="method">Method to use for this request,</param>
        /// <param name="headers">Additional headers for this request.</param>
        /// <param name="ratelimit_wait_override">Override for ratelimit bucket wait time.</param>
        internal BaseRestRequest(BaseDiscordClient client, RateLimitBucket bucket, Uri url, RestRequestMethod method, IDictionary<string, string> headers = null, double? ratelimit_wait_override = null)
        {
            Discord = client;
            RateLimitBucket = bucket;
            RequestTaskSource = new TaskCompletionSource<RestResponse>();
            Url = url;
            Method = method;
            RateLimitWaitOverride = ratelimit_wait_override;

            if (headers != null)
            {
                headers = headers.Select(x => new KeyValuePair<string, string>(x.Key, Uri.EscapeDataString(x.Value)))
                    .ToDictionary(x => x.Key, x => x.Value);
                Headers = new ReadOnlyDictionary<string, string>(headers);
            }
        }

        /// <summary>
        /// Asynchronously waits for this request to complete.
        /// </summary>
        /// <returns>HTTP response to this request.</returns>
        public Task<RestResponse> WaitForCompletionAsync()
            => RequestTaskSource.Task;

        protected internal void SetCompleted(RestResponse response)
            => RequestTaskSource.SetResult(response);

        protected internal void SetFaulted(Exception ex)
            => RequestTaskSource.SetException(ex);

        protected internal bool TrySetFaulted(Exception ex)
            => RequestTaskSource.TrySetException(ex);
    }
}
