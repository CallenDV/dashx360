using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XboxMetroLauncher.Models;

namespace XboxMetroLauncher.Services;

public sealed class SocialIntegrationManager
{
	private readonly IFriendsService _friendsService;

	private readonly LocalSocialIntegrationService _localService;

	private readonly ISteamCommunityService _steamCommunityService;

	public bool IsDiscordFriendAccessAvailable => false;

	public SocialIntegrationManager(IFriendsService friendsService, LocalSocialIntegrationService localService, ISteamCommunityService steamCommunityService)
	{
		_friendsService = friendsService;
		_localService = localService;
		_steamCommunityService = steamCommunityService;
	}

	public async Task<SocialFriendsLoadResult> LoadFriendsAsync(SocialIntegrationMode mode, DiscordConnectionState discordConnectionState, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<SocialFriend> friends = (await _localService.LoadFriendsAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ToList();
		string popupMessage = string.Empty;
		bool flag = _steamCommunityService.IsConfigured;
		if (!flag)
		{
			bool flag2 = (uint)(mode - 2) <= 1u;
			flag = flag2;
		}
		if (flag)
		{
			friends.AddRange(await _steamCommunityService.LoadFriendsAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			popupMessage = _steamCommunityService.LastStatusMessage;
		}
		return new SocialFriendsLoadResult
		{
			Friends = friends,
			PopupMessage = popupMessage
		};
	}

	public SocialFriend CreateLocalFriend(string gamertag)
	{
		Random random = new Random(gamertag.ToLowerInvariant().Aggregate(17, (int current, char character) => current * 31 + character));
		string[] array = new string[4]
		{
			"Offline",
			"Away",
			$"Last online {random.Next(8, 46)} minutes ago",
			$"Last online {random.Next(1, 12)} hours ago"
		};
		string[] array2 = new string[4] { "Recreation", "Family", "Pro", "Underground" };
		string reputationText = BuildReputation(random.Next(3, 6));
		return new SocialFriend
		{
			Id = "local:" + gamertag,
			DisplayName = gamertag,
			Source = SocialFriendSource.Local,
			AvatarPathOrUrl = ProfileImagePool.GetRandomPoolAvatarPath(),
			IsOnline = false,
			StatusText = array[random.Next(array.Length)],
			ActivityText = string.Empty,
			GamerscoreText = $"{random.Next(2500, 95000):N0} G",
			ReputationText = reputationText,
			ZoneText = array2[random.Next(array2.Length)],
			IdentityDetailText = "Local"
		};
	}

	public Task<SocialConnectionResult> ConnectDiscordAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.FromResult(new SocialConnectionResult
		{
			State = DiscordConnectionState.NotImplemented,
			PopupMessage = "Discord is not available in the public build."
		});
	}

	public async Task AddLocalFriendAsync(SocialFriend friend, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<FriendProfile> friends = (await _friendsService.LoadAsync().ConfigureAwait(continueOnCapturedContext: false)).Where((FriendProfile item) => !string.Equals(item.Gamertag, friend.DisplayName, StringComparison.OrdinalIgnoreCase)).Append(LocalSocialIntegrationService.MapToLocalFriend(friend)).ToList();
		await _friendsService.SaveAsync(friends).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task RemoveLocalFriendAsync(SocialFriend friend, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<FriendProfile> friends = (await _friendsService.LoadAsync().ConfigureAwait(continueOnCapturedContext: false)).Where((FriendProfile item) => !string.Equals(item.Gamertag, friend.DisplayName, StringComparison.OrdinalIgnoreCase)).ToList();
		await _friendsService.SaveAsync(friends).ConfigureAwait(continueOnCapturedContext: false);
	}

	public Task<SocialPartyInviteResult> InviteToPartyAsync(SocialFriend friend, DiscordConnectionState discordConnectionState, CancellationToken cancellationToken = default(CancellationToken))
	{
		return friend.Source switch
		{
			SocialFriendSource.Local => _localService.InviteToPartyAsync(friend, cancellationToken), 
			SocialFriendSource.Steam => Task.FromResult(new SocialPartyInviteResult
			{
				AddToPartyList = false,
				PopupMessage = "Steam friends are read-only in this launcher."
			}), 
			_ => Task.FromResult(new SocialPartyInviteResult
			{
				AddToPartyList = false,
				PopupMessage = GetSourceLabel(friend) + " integration is not available in the public build."
			}), 
		};
	}

	public static string GetSourceLabel(SocialFriend friend)
	{
		return friend.Source switch
		{
			SocialFriendSource.Discord => "Discord", 
			SocialFriendSource.Steam => "Steam", 
			_ => "Local", 
		};
	}

	public async Task<SocialConnectionResult> RestoreDiscordSessionAsync(string tokenTypeName, string accessToken, string grantedScopes, CancellationToken cancellationToken = default(CancellationToken))
	{
		return await Task.FromResult(new SocialConnectionResult
		{
			State = DiscordConnectionState.NotImplemented,
			PopupMessage = "Discord is not available in the public build."
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	public static string GetFriendStatusLabel(SocialFriend friend)
	{
		if (friend.Source == SocialFriendSource.Steam)
		{
			if (!friend.IsOnline)
			{
				return "Offline";
			}
			return "Online";
		}
		if (!string.IsNullOrWhiteSpace(friend.StatusText))
		{
			return friend.StatusText;
		}
		if (!friend.IsOnline)
		{
			return "Offline";
		}
		return "Online";
	}

	public static string GetFriendActivityLabel(SocialFriend friend)
	{
		if (friend.Source == SocialFriendSource.Steam)
		{
			if (!string.IsNullOrWhiteSpace(friend.ActivityText))
			{
				return friend.ActivityText;
			}
			return GetFriendStatusLabel(friend);
		}
		if (!string.IsNullOrWhiteSpace(friend.ActivityText))
		{
			return friend.ActivityText;
		}
		return GetFriendStatusLabel(friend);
	}

	public static SocialFriend BuildPartyHost(Profile profile)
	{
		return new SocialFriend
		{
			Id = "local-host:" + profile.Gamertag,
			DisplayName = profile.Gamertag,
			Source = SocialFriendSource.Local,
			AvatarPathOrUrl = profile.GamerPicturePath,
			IsOnline = true,
			StatusText = profile.OnlineStatus,
			ActivityText = "Xbox 360 Dashboard",
			GamerscoreText = $"{profile.Gamerscore:N0} G",
			ReputationText = "★★★★★",
			ZoneText = "Party",
			IdentityDetailText = profile.Gamertag + "'s Profile",
			IsPartyHost = true
		};
	}

	private static string BuildReputation(int filledStars)
	{
		return new string('★', Math.Clamp(filledStars, 0, 5)) + new string('☆', Math.Clamp(5 - filledStars, 0, 5));
	}
}
