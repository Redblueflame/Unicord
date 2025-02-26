﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using DSharpPlus;
using DSharpPlus.Entities;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unicord.Universal.Controls.Flyouts
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserListContextFlyout : MenuFlyout
    {
        public UserListContextFlyout()
        {
            InitializeComponent();
        }

        public bool CanKick =>
            (Target.DataContext is DiscordMember member) ? member.Guild.CurrentMember.PermissionsIn(null).HasPermission(Permissions.KickMembers) : false;

        public bool CanBan =>
            (Target.DataContext is DiscordMember member) ? member.Guild.CurrentMember.PermissionsIn(null).HasPermission(Permissions.BanMembers) : false;

        public bool CanChangeNickname
        {
            get
            {
                if (Target.DataContext is DiscordMember member)
                {
                    var perms = member.PermissionsIn(null);
                    if (member.IsCurrent)
                    {
                        return perms.HasPermission(Permissions.ChangeNickname);
                    }
                    else
                    {
                        return perms.HasPermission(Permissions.ManageNicknames);
                    }
                }

                return false;
            }
        }

        public bool ShowManagementSeparator =>
            CanKick || CanBan || CanChangeNickname;
    }
}
