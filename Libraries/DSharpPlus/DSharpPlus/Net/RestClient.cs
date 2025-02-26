﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Exceptions;

namespace DSharpPlus.Net
{
    /// <summary>
    /// Represents a client used to make REST requests.
    /// </summary>
    internal sealed class RestClient
    {
        private static UTF8Encoding UTF8 { get; } = new UTF8Encoding(false);
        private static Regex RouteArgumentRegex { get; } = new Regex(@":([a-z_]+)");

        private BaseDiscordClient Discord { get; }
        private HttpClient HttpClient { get; }
        private ConcurrentDictionary<string, RateLimitBucket> Buckets { get; }
        private AsyncManualResetEvent GlobalRateLimitEvent { get; }

        internal RestClient(BaseDiscordClient client)
            : this(client.Configuration.Proxy, client.Configuration.HttpTimeout)
        {
            Discord = client;
            HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Utilities.GetFormattedToken(client));
        }

        internal RestClient(IWebProxy proxy, TimeSpan timeout) // This is for meta-clients, such as the webhook client
        {
            var httphandler = new HttpClientHandler
            {
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                UseProxy = proxy != null
            };
            if (httphandler.UseProxy) // because mono doesn't implement this properly
            {
                httphandler.Proxy = proxy;
            }

            HttpClient = new HttpClient(httphandler)
            {
                BaseAddress = new Uri(Utilities.GetApiBaseUri()),
                Timeout = timeout
            };
            HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Utilities.GetUserAgent());

            Buckets = new ConcurrentDictionary<string, RateLimitBucket>();
            GlobalRateLimitEvent = new AsyncManualResetEvent(true);
        }

        public RateLimitBucket GetBucket(RestRequestMethod method, string route, object route_params, out string url)
        {
            var rparams_props = route_params.GetType()
                .GetTypeInfo()
                .DeclaredProperties;
            var rparams = new Dictionary<string, string>();
            foreach (var xp in rparams_props)
            {
                var val = xp.GetValue(route_params);
                if (val is string xs)
                {
                    rparams[xp.Name] = xs;
                }
                else if (val is DateTime dt)
                {
                    rparams[xp.Name] = dt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
                }
                else if (val is DateTimeOffset dto)
                {
                    rparams[xp.Name] = dto.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
                }
                else if (val is IFormattable xf)
                {
                    rparams[xp.Name] = xf.ToString(null, CultureInfo.InvariantCulture);
                }
                else
                {
                    rparams[xp.Name] = val.ToString();
                }
            }

            var guild_id = rparams.ContainsKey("guild_id") ? rparams["guild_id"] : "";
            var channel_id = rparams.ContainsKey("channel_id") ? rparams["channel_id"] : "";
            var webhook_id = rparams.ContainsKey("webhook_id") ? rparams["webhook_id"] : "";

            var id = RateLimitBucket.GenerateId(method, route, guild_id, channel_id, webhook_id);

            // using the GetOrAdd version with the factory has no advantages as it will allocate the delegate, closure object and bucket (if needed) instead of just the bucket 
            RateLimitBucket bucket = Buckets.GetOrAdd(id, new RateLimitBucket(method, route, guild_id, channel_id, webhook_id));

            url = RouteArgumentRegex.Replace(route, xm => rparams[xm.Groups[1].Value]);
            return bucket;
        }

        public Task ExecuteRequestAsync(BaseRestRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return ExecuteRequestAsync(request, null, null);
        }

        // to allow proper rescheduling of the first request from a bucket
        private async Task ExecuteRequestAsync(BaseRestRequest request, RateLimitBucket bucket, TaskCompletionSource<bool> ratelimitTcs)
        {
            try
            {
                await GlobalRateLimitEvent.WaitAsync();

                if (bucket == null)
                {
                    bucket = request.RateLimitBucket;
                }

                if (ratelimitTcs == null)
                {
                    ratelimitTcs = await WaitForInitialRateLimit(bucket);
                }

                if (ratelimitTcs == null) // ckeck rate limit only if we are not the probe request
                {
                    var now = DateTimeOffset.UtcNow;

                    await bucket.TryResetLimit(now);

                    // Decrement the remaining number of requests as there can be other concurrent requests before this one finishes and has a chance to update the bucket
#pragma warning disable 420 // interlocked access is always volatile
                    if (Interlocked.Decrement(ref bucket._remaining) < 0)
#pragma warning restore 420
                    {
                        request.Discord.DebugLogger.LogMessage(LogLevel.Debug, "REST", $"Request for bucket {bucket}. Blocking.", DateTime.Now);
                        var delay = bucket.Reset - now;
                        if (delay < new TimeSpan(-TimeSpan.TicksPerMinute))
                        {
                            request.Discord.DebugLogger.LogMessage(LogLevel.Error, "REST", "Failed to retrieve ratelimits. Giving up and allowing next request for bucket.", DateTime.Now);
                            bucket._remaining = 1;
                        }
                        if (delay < TimeSpan.Zero)
                        {
                            delay = TimeSpan.FromMilliseconds(100);
                        }

                        request.Discord.DebugLogger.LogMessage(LogLevel.Warning, "REST", $"Pre-emptive ratelimit triggered, waiting until {bucket.Reset:yyyy-MM-dd HH:mm:ss zzz} ({delay:c})", DateTime.Now);
                        request.Discord.DebugLogger.LogTaskFault(Task.Delay(delay).ContinueWith(t => ExecuteRequestAsync(request, null, null)), LogLevel.Error, "RESET", "Error while executing request: ");
                        return;
                    }
                    request.Discord.DebugLogger.LogMessage(LogLevel.Debug, "REST", $"Request for bucket {bucket}. Allowing.", DateTime.Now);
                }
                else
                {
                    request.Discord.DebugLogger.LogMessage(LogLevel.Debug, "REST", $"Initial Request for bucket {bucket}. Allowing.", DateTime.Now);
                }

                var req = BuildRequest(request);
                var response = new RestResponse();
                try
                {
                    var res = await HttpClient.SendAsync(req, CancellationToken.None).ConfigureAwait(false);

                    var bts = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var txt = UTF8.GetString(bts, 0, bts.Length);

                    response.Headers = res.Headers.ToDictionary(xh => xh.Key, xh => string.Join("\n", xh.Value));
                    response.Response = txt;
                    response.ResponseCode = (int)res.StatusCode;
                }
                catch (HttpRequestException httpex)
                {
                    request.Discord.DebugLogger.LogMessage(LogLevel.Error, "REST", $"Request to {request.Url} triggered an HttpException: {httpex.Message}", DateTime.Now);
                    request.SetFaulted(httpex);
                    FailInitialRateLimitTest(bucket, ratelimitTcs);
                    return;
                }

                UpdateBucket(request, response, ratelimitTcs);

                Exception ex = null;
                switch (response.ResponseCode)
                {
                    case 400:
                    case 405:
                        ex = new BadRequestException(request, response);
                        break;

                    case 401:
                    case 403:
                        ex = new UnauthorizedException(request, response);
                        break;

                    case 404:
                        ex = new NotFoundException(request, response);
                        break;

                    case 429:
                        ex = new RateLimitException(request, response);

                        // check the limit info and requeue
                        Handle429(response, out var wait, out var global);
                        if (wait != null)
                        {
                            if (global)
                            {
                                request.Discord.DebugLogger.LogMessage(LogLevel.Error, "REST", "Global ratelimit hit, cooling down", DateTime.Now);
                                try
                                {
                                    GlobalRateLimitEvent.Reset();
                                    await wait.ConfigureAwait(false);
                                }
                                finally
                                {
                                    // we don't want to wait here until all the blocked requests have been run, additionally Set can never throw an exception that could be suppressed here
                                    _ = GlobalRateLimitEvent.Set();
                                }
                                request.Discord.DebugLogger.LogTaskFault(ExecuteRequestAsync(request, bucket, ratelimitTcs), LogLevel.Error, "REST", "Error while retrying request: ");
                            }
                            else
                            {
                                request.Discord.DebugLogger.LogMessage(LogLevel.Error, "REST", $"Ratelimit hit, requeueing request to {request.Url}", DateTime.Now);
                                await wait.ConfigureAwait(false);
                                request.Discord.DebugLogger.LogTaskFault(ExecuteRequestAsync(request, bucket, ratelimitTcs), LogLevel.Error, "REST", "Error while retrying request: ");
                            }

                            return;
                        }
                        break;
                }

                if (ex != null)
                {
                    request.SetFaulted(ex);
                }
                else
                {
                    request.SetCompleted(response);
                }
            }
            catch (Exception ex)
            {
                request.Discord.DebugLogger.LogMessage(LogLevel.Error, "REST", $"Request to {request.Url} triggered an {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", DateTime.Now);

                // if something went wrong and we couldn't get rate limits for the first request here, allow the next request to run
                if (bucket != null && ratelimitTcs != null && bucket._limitTesting != 0)
                {
                    FailInitialRateLimitTest(bucket, ratelimitTcs);
                }

                if (!request.TrySetFaulted(ex))
                {
                    throw;
                }
            }
        }

        private void FailInitialRateLimitTest(RateLimitBucket bucket, TaskCompletionSource<bool> ratelimitTcs)
        {
            if (ratelimitTcs == null)
            {
                return;
            }

            bucket._limitValid = false;
            bucket._limitTestFinished = null;
            bucket._limitTesting = 0;

            // no need to wait on all the potentially waiting tasks
            Task.Run(() => ratelimitTcs.TrySetResult(false));
        }

        private async Task<TaskCompletionSource<bool>> WaitForInitialRateLimit(RateLimitBucket bucket)
        {
            while (!bucket._limitValid)
            {
                if (bucket._limitTesting == 0)
                {
#pragma warning disable 420 // interlocked access is always volatile
                    if (Interlocked.CompareExchange(ref bucket._limitTesting, 1, 0) == 0)
#pragma warning restore 420
                    {
                        // if we got here when the first request was just finishing, we must not create the waiter task as it would signel ExecureRequestAsync to bypass rate limiting
                        if (bucket._limitValid)
                        {
                            return null;
                        }

                        // allow exactly one request to go through without having rate limits available
                        var ratelimitsTcs = new TaskCompletionSource<bool>();
                        bucket._limitTestFinished = ratelimitsTcs.Task;
                        return ratelimitsTcs;
                    }
                }
                // it can take a couple of cycles for the task to be allocated, so wait until it happens or we are no longer probing for the limits
                Task waitTask = null;
                while (bucket._limitTesting != 0 && (waitTask = bucket._limitTestFinished) == null)
                {
                    await Task.Yield();
                }

                if (waitTask != null)
                {
                    await waitTask;
                }

                // if the request failed and the response did not have rate limit headers we have allow the next request and wait again, thus this is a loop here
            }
            return null;
        }

        private HttpRequestMessage BuildRequest(BaseRestRequest request)
        {
            var req = new HttpRequestMessage(new HttpMethod(request.Method.ToString()), request.Url);
            if (request.Headers != null && request.Headers.Any())
            {
                foreach (var kvp in request.Headers)
                {
                    req.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            if (request is RestRequest nmprequest && !string.IsNullOrWhiteSpace(nmprequest.Payload))
            {
                req.Content = new StringContent(nmprequest.Payload);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            if (request is MultipartWebRequest mprequest)
            {
                string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");

                req.Headers.Add("Connection", "keep-alive");
                req.Headers.Add("Keep-Alive", "600");

                var content = new MultipartFormDataContent(boundary);
                if (mprequest.Values != null && mprequest.Values.Any())
                {
                    foreach (var kvp in mprequest.Values)
                    {
                        content.Add(new StringContent(kvp.Value), kvp.Key);
                    }
                }

                if (mprequest.Files != null && mprequest.Files.Any())
                {
                    var i = 1;
                    foreach (var f in mprequest.Files)
                    {
                        content.Add(new StreamContent(f.Value), $"file{(i++).ToString(CultureInfo.InvariantCulture)}", f.Key);
                    }
                }

                req.Content = content;
            }

            return req;
        }

        private void Handle429(RestResponse response, out Task wait_task, out bool global)
        {
            wait_task = null;
            global = false;

            if (response.Headers == null)
            {
                return;
            }

            var hs = response.Headers;

            // handle the wait
            if (hs.TryGetValue("Retry-After", out var retry_after_raw))
            {
                var retry_after = int.Parse(retry_after_raw, CultureInfo.InvariantCulture);
                wait_task = Task.Delay(retry_after);
            }

            // check if global b1nzy
            if (hs.TryGetValue("X-RateLimit-Global", out var isglobal) && isglobal.ToLowerInvariant() == "true")
            {
                // global
                global = true;
            }
        }

        private void UpdateBucket(BaseRestRequest request, RestResponse response, TaskCompletionSource<bool> ratelimitTcs)
        {
            var bucket = request.RateLimitBucket;

            if (response.Headers == null)
            {
                if (response.ResponseCode != 429) // do not fail when ratelimit was or the next request will be scheduled hitting the rate limit again
                {
                    FailInitialRateLimitTest(bucket, ratelimitTcs);
                }

                return;
            }

            var hs = response.Headers;

            if (hs.TryGetValue("X-RateLimit-Global", out var isglobal) && isglobal.ToLowerInvariant() == "true")
            {
                if (response.ResponseCode != 429)
                {
                    FailInitialRateLimitTest(bucket, ratelimitTcs);
                }

                return;
            }

            var r1 = hs.TryGetValue("X-RateLimit-Limit", out var usesmax);
            var r2 = hs.TryGetValue("X-RateLimit-Remaining", out var usesleft);
            var r3 = hs.TryGetValue("X-RateLimit-Reset", out var reset);

            if (!r1 || !r2 || !r3)
            {
                if (response.ResponseCode != 429)
                {
                    FailInitialRateLimitTest(bucket, ratelimitTcs);
                }

                return;
            }

            var clienttime = DateTimeOffset.UtcNow;
            var resettime = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(long.Parse(reset, CultureInfo.InvariantCulture));
            var servertime = clienttime;
            if (hs.TryGetValue("Date", out var raw_date))
            {
                servertime = DateTimeOffset.Parse(raw_date, CultureInfo.InvariantCulture).ToUniversalTime();
            }

            var resetdelta = resettime - servertime;
            //var difference = clienttime - servertime;
            //if (Math.Abs(difference.TotalSeconds) >= 1)
            //    request.Discord.DebugLogger.LogMessage(LogLevel.Debug, "REST", $"Difference between machine and server time: {difference.TotalMilliseconds.ToString("#,##0.00", CultureInfo.InvariantCulture)}ms", DateTime.Now);
            //else
            //    difference = TimeSpan.Zero;

            if (request.RateLimitWaitOverride != null)
            {
                resetdelta = TimeSpan.FromSeconds(request.RateLimitWaitOverride.Value);
            }

            var newReset = clienttime + resetdelta;


            if (ratelimitTcs != null)
            {
                // initial population of the ratelimit data
                bucket.Maximum = int.Parse(usesmax, CultureInfo.InvariantCulture);
                bucket._remaining = int.Parse(usesleft, CultureInfo.InvariantCulture);
                bucket.Reset = newReset;
                bucket._nextReset = newReset.UtcTicks;

                bucket._limitValid = true;
                bucket._limitTestFinished = null;
                bucket._limitTesting = 0;
                Task.Run(() => ratelimitTcs.TrySetResult(true));
            }
            else
            {
                // only update the bucket values if this request was for a newer interval than the one
                // currently in the bucket, to avoid issues with concurrent requests in one bucket

                bucket.Maximum = int.Parse(usesmax, CultureInfo.InvariantCulture);
                bucket.Reset = newReset;
                // remaining is reset by TryResetLimit and not the response, just allow that to happen when it is time
                if (bucket._nextReset == 0)
                {
                    bucket._nextReset = newReset.UtcTicks;
                }
            }
        }
    }
}


//       More useless comments, sorry..
//  Was listening to this, felt like sharing.
// https://www.youtube.com/watch?v=ePX5qgDe9s4
//         ♫♪.ılılıll|̲̅̅●̲̅̅|̲̅̅=̲̅̅|̲̅̅●̲̅̅|llılılı.♫♪
