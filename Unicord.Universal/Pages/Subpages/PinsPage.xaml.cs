﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Unicord.Universal.Controls;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unicord.Universal.Pages.Subpages
{
    public sealed partial class PinsPage : Page
    {
        private DiscordChannel _channel;

        private static bool _hasHandler;
        private static ConcurrentDictionary<ulong, ObservableCollection<DiscordMessage>> _pinsCache
            = new ConcurrentDictionary<ulong, ObservableCollection<DiscordMessage>>();

        public PinsPage()
        {
            InitializeComponent();
            if (!_hasHandler)
            {
                _hasHandler = true;
                App.Discord.MessageUpdated += Discord_MessageUpdated;
            }
        }

        private async Task Discord_MessageUpdated(MessageUpdateEventArgs e)
        {
            if (_pinsCache.TryGetValue(e.Channel.Id, out var pins))
            {
                if (e.Message.Pinned)
                {
                    if (!pins.Contains(e.Message))
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => pins.Add(e.Message));
                    }
                }
                else
                {
                    if (pins.Contains(e.Message))
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => pins.Remove(e.Message));
                    }
                }

                if (e.Channel.Id == _channel.Id)
                {
                    noMessages.Visibility = pins.Any() ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            messages.ItemsSource = Enumerable.Empty<DiscordMessage>();

            if (e.Parameter is DiscordChannel channel)
            {
                _channel = channel;

                try
                {
                    noMessages.Visibility = Visibility.Collapsed;
                    ratelimited.Visibility = Visibility.Collapsed;
                    progress.IsActive = true;

                    if (!_pinsCache.TryGetValue(channel.Id, out var pins))
                    {
                        var p = await channel.GetPinnedMessagesAsync();
                        pins = new ObservableCollection<DiscordMessage>();
                        foreach (var m in p)
                        {
                            pins.Add(m);
                        }

                        _pinsCache[channel.Id] = pins;
                    }

                    messages.ItemsSource = pins;

                    if (!pins.Any())
                    {
                        noMessages.Visibility = Visibility.Visible;
                    }
                }
                catch (RateLimitException)
                {
                    ratelimited.Visibility = Visibility.Visible;
                }
                catch (Exception)
                {

                }
            }

            progress.IsActive = false;
        }
    }
}
