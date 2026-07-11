using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XboxMetroLauncher.Input;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Services;
using XboxMetroLauncher.Utilities;
using XboxMetroLauncher.ViewModels.Tabs;

namespace XboxMetroLauncher.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
	private static readonly string SteamScanDebugLogPath = Path.Combine(AppPaths.LogsFolder, "steam-scan-debug.log");

	private const int ShowWindowRestore = 9;

	private readonly IGameLibraryService _libraryService;

	private readonly IGameLaunchService _launchService;

	private readonly ISearchService _searchService;

	private readonly ISettingsService _settingsService;

	private readonly IProfileService _profileService;

	private readonly IFilePickerService _filePickerService;

	private readonly IImportExportService _importExportService;

	private readonly ISteamLibraryScannerService _steamLibraryScannerService;

	private readonly ISteamCommunityService _steamCommunityService;

	private readonly IThemeService _themeService;

	private readonly IStartupRegistrationService _startupRegistrationService;

	private readonly IAudioService _audioService;

	private readonly SocialIntegrationManager _socialIntegrationManager;

	private readonly IRunningGameService _runningGameService;

	private readonly AudioAnalysisService _audioAnalysisService = new AudioAnalysisService();

	private readonly MediaPlayer _musicPlayer = new MediaPlayer();

	private readonly DispatcherTimer _musicTimer;

	private readonly List<System.Windows.Media.Brush> _accentBrushes;

	private GameLibrary _library = new GameLibrary();

	private DashboardTabViewModel? _currentTab;

	private GameCardViewModel? _selectedGame;

	private GameCardViewModel? _featuredGame;

	private Profile _profile = new Profile();

	private AppSettings _settings = new AppSettings();

	private string _searchQuery = string.Empty;

	private string _statusMessage = "Ready";

	private bool _isSearchOverlayOpen;

	private bool _isDetailsOpen;

	private bool _isQuickMenuOpen;

	private bool _isMyGamesOpen;

	private bool _isLibraryShowingPins;

	private bool _isLibraryShowingApps;

	private bool _isLauncherSettingsOpen;

	private bool _isProfileEditorOpen;

	private bool _isThemeMenuOpen;

	private bool _isThemeCreatorOpen;

	private bool _isDashboardCustomizerOpen;

	private bool _isSteamSetupOpen;

	private bool _isMusicPlayerOpen;

	private bool _isMusicPlayerTransparent;

	private bool _isMusicVisualizerFullscreen;

	private bool _isMusicPlaying;

	private bool _isShuffleEnabled;

	private bool _isBooting = true;

	private string _clockText = string.Empty;

	private string _musicPositionText = "0:00";

	private string _musicDurationText = "0:00";

	private double _musicProgress;

	private double _musicVolume = 0.7;

	private double _visualizerBass;

	private double _visualizerMid;

	private double _visualizerTreble;

	private double _visualizerLoudness;

	private double _visualizerPeak;

	private string? _pendingTabSound;

	private GameCardViewModel? _trayGame;

	private MusicTrackViewModel? _currentMusicTrack;

	private int _musicIndex = -1;

	private readonly Random _random = new Random();

	private DashboardTheme? _selectedTheme;

	private string _themeNameInput = string.Empty;

	private string _themeHomePreviewPath = string.Empty;

	private string _themeGamesPreviewPath = string.Empty;

	private string _themeSettingsPreviewPath = string.Empty;

	private string _themeAppsPreviewPath = string.Empty;

	private int _customizerTabIndex;

	private DashboardTileCustomizationViewModel? _selectedDashboardTile;

	private byte _customTileRed = 96;

	private byte _customTileGreen = 182;

	private byte _customTileBlue = 22;

	private string _steamSetupApiKey = string.Empty;

	private string _steamSetupSteamId64 = string.Empty;

	private string _steamSetupStatus = "Steam is not connected.";

	private int _selectedGameDetailsTabIndex;

	private bool _isGameDetailsSeeAllOpen;

	private IReadOnlyList<SteamGameDlc> _selectedGameDlc = Array.Empty<SteamGameDlc>();

	private int _libraryMenuStartIndex;

	private const int LibraryVisibleWindowSize = 6;

	private const double LibraryMenuLeftPeekOffset = 176.0;

	public ObservableCollection<DashboardTabViewModel> Tabs { get; }

	public ObservableCollection<GameCardViewModel> Games { get; } = new ObservableCollection<GameCardViewModel>();

	public ObservableCollection<MusicTrackViewModel> MusicTracks { get; } = new ObservableCollection<MusicTrackViewModel>();

	public ObservableCollection<DashboardTheme> AvailableThemes { get; } = new ObservableCollection<DashboardTheme>();

	public ObservableCollection<GameCardViewModel> VisibleLibraryMenuGames { get; } = new ObservableCollection<GameCardViewModel>();

	public ObservableCollection<DashboardTabCustomizationViewModel> DashboardCustomizationTabs { get; } = new ObservableCollection<DashboardTabCustomizationViewModel>();

	public ObservableCollection<string> AudioOutputDeviceOptions { get; } = new ObservableCollection<string> { "Default" };

	public IEnumerable<GameCardViewModel> RecentGames => Games.OrderByDescending((GameCardViewModel game) => game.Game.LastPlayed ?? DateTimeOffset.MinValue).Take(8);

	public IEnumerable<GameCardViewModel> PinnedGames => Games.Where((GameCardViewModel game) => game.Game.IsFavorite).Take(8);

	public IEnumerable<GameCardViewModel> ImportedGames => Games.Where((GameCardViewModel game) => string.Equals(game.Game.Genre, "Imported", StringComparison.OrdinalIgnoreCase));

	public IEnumerable<string> LibraryPaths => _library.LibraryPaths;

	public double LibraryMenuScrollOffset
	{
		get
		{
			if (_libraryMenuStartIndex <= 0)
			{
				return 0.0;
			}
			return 176.0;
		}
	}

	public IReadOnlyList<string> ResolutionOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "720p", "1080p", "1440p", "4K" });

	public IReadOnlyList<string> GameCoverFitOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "Auto", "Cover", "Fill", "Fit" });

	public IReadOnlyList<string> AddDestinationOptions { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "My Games", "My Apps" });

	public IReadOnlyList<string> SocialIntegrationOptions { get; } = new _003C_003Ez__ReadOnlySingleElementList<string>("Local");

	public ObservableCollection<GameDetailsTabViewModel> GameDetailsTabs { get; }

	public ObservableCollection<GameDetailsExtraViewModel> GameDetailsExtras { get; } = new ObservableCollection<GameDetailsExtraViewModel>();

	public ObservableCollection<GameDetailsExtraViewModel> GameDetailsPreviewExtras { get; } = new ObservableCollection<GameDetailsExtraViewModel>();

	public ObservableCollection<string> GameDetailsGalleryImages { get; } = new ObservableCollection<string>();

	public bool IsGameDetailsOverviewTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "overview")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsDetailsTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "details")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsExtrasTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "extras")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsGalleryTab
	{
		get
		{
			if (SelectedGameDetailsTabKey == "gallery")
			{
				return !IsGameDetailsSeeAllOpen;
			}
			return false;
		}
	}

	public bool IsGameDetailsSeeAllOpen
	{
		get
		{
			return _isGameDetailsSeeAllOpen;
		}
		set
		{
			if (SetProperty(ref _isGameDetailsSeeAllOpen, value, "IsGameDetailsSeeAllOpen"))
			{
				NotifyGameDetailsTabVisibility();
				OnPropertyChanged("GameDetailsSeeAllCountText");
			}
		}
	}

	public string SelectedGameDetailsTabKey
	{
		get
		{
			if (GameDetailsTabs.Count != 0)
			{
				return GameDetailsTabs[_selectedGameDetailsTabIndex].Key;
			}
			return "overview";
		}
	}

	public string GameDetailsDeveloperText
	{
		get
		{
			GameCardViewModel? selectedGame = SelectedGame;
			if (selectedGame == null || !selectedGame.IsSteamGame)
			{
				return "User added";
			}
			return "Steam";
		}
	}

	public string GameDetailsPublisherText
	{
		get
		{
			GameCardViewModel? selectedGame = SelectedGame;
			if (selectedGame == null || !selectedGame.IsSteamGame)
			{
				return "Manual";
			}
			return "Steam";
		}
	}

	public string GameDetailsGenreText => SelectedGame?.DetailsGenreText ?? "Game";

	public string GameDetailsLocalCapabilitiesText => string.Join(Environment.NewLine, BuildLocalCapabilities());

	public string GameDetailsOnlineCapabilitiesText => string.Join(Environment.NewLine, BuildOnlineCapabilities());

	public string GameDetailsNoteText => BuildGameDetailsNote();

	public string GameDetailsSeeAllCountText
	{
		get
		{
			if (GameDetailsExtras.Count != 0)
			{
				return $"{Math.Min(1, GameDetailsExtras.Count)} of {GameDetailsExtras.Count}";
			}
			return "0 of 0";
		}
	}

	public string GameDetailsGalleryImagePath => BuildGameDetailsGalleryImages().FirstOrDefault() ?? string.Empty;

	public string GameDetailsGalleryCountText
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(GameDetailsGalleryImagePath))
			{
				return "1 of 1";
			}
			return "0 of 0";
		}
	}

	public string GameDetailsAddOnIconPath => AppPaths.ResolvePath(Path.Combine("Assets", "Misc", "add on icon.png"));

	public string GameDetailsSeeAllIconPath => AppPaths.ResolvePath(Path.Combine("Assets", "Misc", "see all icon.png"));

	public DashboardTabViewModel? CurrentTab
	{
		get
		{
			return _currentTab;
		}
		set
		{
			if (!SetProperty(ref _currentTab, value, "CurrentTab") || value == null)
			{
				return;
			}
			foreach (DashboardTabViewModel tab in Tabs)
			{
				tab.IsSelected = tab == value;
			}
			_audioService.Play(_pendingTabSound ?? "tab");
			_pendingTabSound = null;
			OnPropertyChanged("CurrentTabName");
			OnPropertyChanged("PreviousTab");
			OnPropertyChanged("NextTab");
			OnPropertyChanged("LeftPreviewContentLeft");
			OnPropertyChanged("RightPreviewContentLeft");
			OnPropertyChanged("CurrentReferenceImagePath");
			OnPropertyChanged("CurrentReferenceImageOpacity");
			OnPropertyChanged("UseLightDashboardChrome");
			OnPropertyChanged("CurrentThemeBackgroundPath");
		}
	}

	public string CurrentTabName => CurrentTab?.Name ?? string.Empty;

	public double LeftPreviewContentLeft => (CurrentTab?.Key == "settings") ? (-938) : (-910);

	public double RightPreviewContentLeft
	{
		get
		{
			string text = CurrentTab?.Key;
			bool flag = ((text == "bing" || text == "home") ? true : false);
			return flag ? (-198) : (-240);
		}
	}

	public DashboardTabViewModel? PreviousTab
	{
		get
		{
			if (CurrentTab == null)
			{
				return null;
			}
			int num = Tabs.IndexOf(CurrentTab);
			if (num <= 0)
			{
				return null;
			}
			return Tabs[num - 1];
		}
	}

	public DashboardTabViewModel? NextTab
	{
		get
		{
			if (CurrentTab == null)
			{
				return null;
			}
			int num = Tabs.IndexOf(CurrentTab);
			if (num < 0 || num >= Tabs.Count - 1)
			{
				return null;
			}
			return Tabs[num + 1];
		}
	}

	public string CurrentReferenceImagePath => string.Empty;

	public double CurrentReferenceImageOpacity => 0.0;

	public bool UseLightDashboardChrome => false;

	public GameCardViewModel? SelectedGame
	{
		get
		{
			return _selectedGame;
		}
		set
		{
			if (SetProperty(ref _selectedGame, value, "SelectedGame"))
			{
				if (value != null)
				{
					FeaturedGame = value;
					StatusMessage = value.Title;
				}
				RefreshVisibleLibraryMenuGames();
				OnPropertyChanged("SpotlightTitle");
				OnPropertyChanged("SpotlightSubtitle");
				OnPropertyChanged("MyGamesCountText");
				OnPropertyChanged("LibraryMenuCountText");
				OnPropertyChanged("SelectedCoverZoom");
				OnPropertyChanged("SelectedCoverOffsetX");
				OnPropertyChanged("SelectedCoverOffsetY");
				RefreshGameDetailsPanels();
			}
		}
	}

	public GameCardViewModel? FeaturedGame
	{
		get
		{
			return _featuredGame;
		}
		set
		{
			if (SetProperty(ref _featuredGame, value, "FeaturedGame"))
			{
				OnPropertyChanged("SpotlightTitle");
				OnPropertyChanged("SpotlightSubtitle");
			}
		}
	}

	public Profile Profile
	{
		get
		{
			return _profile;
		}
		set
		{
			SetProperty(ref _profile, value, "Profile");
		}
	}

	public AppSettings Settings
	{
		get
		{
			return _settings;
		}
		set
		{
			value.GameCoverFitMode = NormalizeGameCoverFitMode(value.GameCoverFitMode);
			value.DefaultAddDestination = NormalizeAddDestination(value.DefaultAddDestination);
			value.AudioOutputDeviceName = (string.IsNullOrWhiteSpace(value.AudioOutputDeviceName) ? "Default" : value.AudioOutputDeviceName);
			value.SocialIntegrationMode = NormalizeSocialIntegrationMode(value.SocialIntegrationMode);
			value.DashboardTileColor = NormalizeDashboardTileColor(value.DashboardTileColor);
			if (value.DashboardTileCustomizations == null)
			{
				Dictionary<string, DashboardTileCustomization> dictionary = (value.DashboardTileCustomizations = new Dictionary<string, DashboardTileCustomization>());
			}
			if (SetProperty(ref _settings, value, "Settings"))
			{
				ApplyDashboardAccentResources(value.DashboardTileColor);
				SyncDashboardCustomizerFromSettings();
				ApplyDashboardTileColorToSliders(value.DashboardTileColor);
				OnPropertyChanged("OpenTrayTitle");
				OnPropertyChanged("GameCoverFitMode");
				OnPropertyChanged("DefaultAddDestination");
				OnPropertyChanged("AudioOutputDeviceName");
				OnPropertyChanged("AudioOutputDeviceOptions");
				OnPropertyChanged("SocialIntegrationModeDisplay");
				OnPropertyChanged("CurrentThemeBackgroundPath");
				OnPropertyChanged("DashboardTileBrush");
				OnPropertyChanged("DashboardTileColorPreviewBrush");
				RefreshDashboardTileBindings();
			}
		}
	}

	public bool IsDashboardCustomizerOpen
	{
		get
		{
			return _isDashboardCustomizerOpen;
		}
		set
		{
			SetProperty(ref _isDashboardCustomizerOpen, value, "IsDashboardCustomizerOpen");
		}
	}

	public DashboardTabCustomizationViewModel? CurrentDashboardCustomizationTab
	{
		get
		{
			if (DashboardCustomizationTabs.Count != 0)
			{
				return DashboardCustomizationTabs[Math.Clamp(_customizerTabIndex, 0, DashboardCustomizationTabs.Count - 1)];
			}
			return null;
		}
	}

	public string CurrentDashboardCustomizationTabName => CurrentDashboardCustomizationTab?.Name ?? string.Empty;

	public DashboardTileCustomizationViewModel? SelectedDashboardTile
	{
		get
		{
			return _selectedDashboardTile;
		}
		set
		{
			if (_selectedDashboardTile != value)
			{
				if (_selectedDashboardTile != null)
				{
					_selectedDashboardTile.IsSelected = false;
					_selectedDashboardTile.PropertyChanged -= SelectedDashboardTile_OnPropertyChanged;
				}
				_selectedDashboardTile = value;
				if (_selectedDashboardTile != null)
				{
					_selectedDashboardTile.IsSelected = true;
					_selectedDashboardTile.PropertyChanged += SelectedDashboardTile_OnPropertyChanged;
				}
				OnPropertyChanged("SelectedDashboardTile");
				OnPropertyChanged("SelectedDashboardTileTitle");
				OnPropertyChanged("SelectedDashboardTileAllowsImageCustomization");
				OnPropertyChanged("SelectedDashboardTileUsesDashboardColor");
				OnPropertyChanged("SelectedDashboardTileCustomizationHint");
			}
		}
	}

	public string SelectedDashboardTileTitle => SelectedDashboardTile?.Title ?? "Select a tile";

	public bool SelectedDashboardTileAllowsImageCustomization => SelectedDashboardTile?.AllowsImageCustomization ?? false;

	public bool SelectedDashboardTileUsesDashboardColor => SelectedDashboardTile?.UsesDashboardColor ?? false;

	public string SelectedDashboardTileCustomizationHint => SelectedDashboardTile?.CustomizationHint ?? "Select a tile.";

	public System.Windows.Media.Brush DashboardTileBrush => CreateBrush(Settings.DashboardTileColor);

	public System.Windows.Media.Brush DashboardTileDarkBrush => CreateBrush(ToDarkAccentColor(Settings.DashboardTileColor));

	public System.Windows.Media.Brush DashboardTileColorPreviewBrush => CreateBrush(Settings.DashboardTileColor);

	public double CustomTileRed
	{
		get
		{
			return (int)_customTileRed;
		}
		set
		{
			byte b = ToByte(value);
			if (_customTileRed != b)
			{
				_customTileRed = b;
				OnPropertyChanged("CustomTileRed");
				UpdateDashboardTileColorFromSliders();
			}
		}
	}

	public double CustomTileGreen
	{
		get
		{
			return (int)_customTileGreen;
		}
		set
		{
			byte b = ToByte(value);
			if (_customTileGreen != b)
			{
				_customTileGreen = b;
				OnPropertyChanged("CustomTileGreen");
				UpdateDashboardTileColorFromSliders();
			}
		}
	}

	public double CustomTileBlue
	{
		get
		{
			return (int)_customTileBlue;
		}
		set
		{
			byte b = ToByte(value);
			if (_customTileBlue != b)
			{
				_customTileBlue = b;
				OnPropertyChanged("CustomTileBlue");
				UpdateDashboardTileColorFromSliders();
			}
		}
	}

	public string GameCoverFitMode
	{
		get
		{
			return Settings.GameCoverFitMode;
		}
		set
		{
			value = NormalizeGameCoverFitMode(value);
			if (!string.Equals(Settings.GameCoverFitMode, value, StringComparison.Ordinal))
			{
				Settings.GameCoverFitMode = value;
				OnPropertyChanged("GameCoverFitMode");
			}
		}
	}

	public string DefaultAddDestination
	{
		get
		{
			return Settings.DefaultAddDestination;
		}
		set
		{
			value = NormalizeAddDestination(value);
			if (!string.Equals(Settings.DefaultAddDestination, value, StringComparison.Ordinal))
			{
				Settings.DefaultAddDestination = value;
				OnPropertyChanged("DefaultAddDestination");
			}
		}
	}

	public string AudioOutputDeviceName
	{
		get
		{
			return Settings.AudioOutputDeviceName;
		}
		set
		{
			value = NormalizeAudioOutputDeviceName(value);
			if (!string.Equals(Settings.AudioOutputDeviceName, value, StringComparison.Ordinal))
			{
				Settings.AudioOutputDeviceName = value;
				OnPropertyChanged("AudioOutputDeviceName");
			}
		}
	}

	public string SocialIntegrationModeDisplay
	{
		get
		{
			return ToSocialIntegrationDisplay(Settings.SocialIntegrationMode);
		}
		set
		{
			SocialIntegrationMode socialIntegrationMode = ParseSocialIntegrationMode(value);
			if (Settings.SocialIntegrationMode != socialIntegrationMode)
			{
				Settings.SocialIntegrationMode = socialIntegrationMode;
				OnPropertyChanged("SocialIntegrationModeDisplay");
			}
		}
	}

	public double SelectedCoverZoom
	{
		get
		{
			GameCardViewModel? selectedGame = SelectedGame;
			if (selectedGame == null || !(selectedGame.Game.CoverZoom > 0.0))
			{
				return 1.0;
			}
			return SelectedGame.Game.CoverZoom;
		}
		set
		{
			if (SelectedGame != null)
			{
				double num = Math.Clamp(value, 1.0, 1.8);
				if (!(Math.Abs(SelectedGame.Game.CoverZoom - num) < 0.001))
				{
					SelectedGame.Game.CoverZoom = num;
					SelectedGame.Refresh();
					OnPropertyChanged("SelectedCoverZoom");
				}
			}
		}
	}

	public double SelectedCoverOffsetX
	{
		get
		{
			return SelectedGame?.Game.CoverOffsetX ?? 0.0;
		}
		set
		{
			if (SelectedGame != null)
			{
				double num = Math.Clamp(value, -1.0, 1.0);
				if (!(Math.Abs(SelectedGame.Game.CoverOffsetX - num) < 0.001))
				{
					SelectedGame.Game.CoverOffsetX = num;
					SelectedGame.Refresh();
					OnPropertyChanged("SelectedCoverOffsetX");
				}
			}
		}
	}

	public double SelectedCoverOffsetY
	{
		get
		{
			return SelectedGame?.Game.CoverOffsetY ?? 0.0;
		}
		set
		{
			if (SelectedGame != null)
			{
				double num = Math.Clamp(value, -1.0, 1.0);
				if (!(Math.Abs(SelectedGame.Game.CoverOffsetY - num) < 0.001))
				{
					SelectedGame.Game.CoverOffsetY = num;
					SelectedGame.Refresh();
					OnPropertyChanged("SelectedCoverOffsetY");
				}
			}
		}
	}

	public string SearchQuery
	{
		get
		{
			return _searchQuery;
		}
		set
		{
			SetProperty(ref _searchQuery, value, "SearchQuery");
		}
	}

	public string StatusMessage
	{
		get
		{
			return _statusMessage;
		}
		set
		{
			SetProperty(ref _statusMessage, value, "StatusMessage");
		}
	}

	public bool IsSearchOverlayOpen
	{
		get
		{
			return _isSearchOverlayOpen;
		}
		set
		{
			SetProperty(ref _isSearchOverlayOpen, value, "IsSearchOverlayOpen");
		}
	}

	public bool IsDetailsOpen
	{
		get
		{
			return _isDetailsOpen;
		}
		set
		{
			if (SetProperty(ref _isDetailsOpen, value, "IsDetailsOpen") && value)
			{
				RefreshSelectedGameDetailsAsync();
			}
		}
	}

	public bool IsQuickMenuOpen
	{
		get
		{
			return _isQuickMenuOpen;
		}
		set
		{
			SetProperty(ref _isQuickMenuOpen, value, "IsQuickMenuOpen");
		}
	}

	public bool IsMyGamesOpen
	{
		get
		{
			return _isMyGamesOpen;
		}
		set
		{
			if (SetProperty(ref _isMyGamesOpen, value, "IsMyGamesOpen"))
			{
				OnPropertyChanged("IsDashboardContentHidden");
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsLauncherSettingsOpen
	{
		get
		{
			return _isLauncherSettingsOpen;
		}
		set
		{
			if (SetProperty(ref _isLauncherSettingsOpen, value, "IsLauncherSettingsOpen"))
			{
				OnPropertyChanged("IsDashboardContentHidden");
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsProfileEditorOpen
	{
		get
		{
			return _isProfileEditorOpen;
		}
		set
		{
			if (SetProperty(ref _isProfileEditorOpen, value, "IsProfileEditorOpen"))
			{
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsThemeMenuOpen
	{
		get
		{
			return _isThemeMenuOpen;
		}
		set
		{
			if (SetProperty(ref _isThemeMenuOpen, value, "IsThemeMenuOpen"))
			{
				OnPropertyChanged("ThemeMenuVisibilityTitle");
			}
		}
	}

	public bool IsThemeCreatorOpen
	{
		get
		{
			return _isThemeCreatorOpen;
		}
		set
		{
			if (SetProperty(ref _isThemeCreatorOpen, value, "IsThemeCreatorOpen"))
			{
				OnPropertyChanged("IsDashboardContentHidden");
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsDashboardContentHidden
	{
		get
		{
			if (!IsMyGamesOpen && !IsLauncherSettingsOpen)
			{
				return IsThemeCreatorOpen;
			}
			return true;
		}
	}

	public bool IsSteamSetupOpen
	{
		get
		{
			return _isSteamSetupOpen;
		}
		set
		{
			SetProperty(ref _isSteamSetupOpen, value, "IsSteamSetupOpen");
		}
	}

	public bool IsMusicPlayerOpen
	{
		get
		{
			return _isMusicPlayerOpen;
		}
		set
		{
			if (SetProperty(ref _isMusicPlayerOpen, value, "IsMusicPlayerOpen"))
			{
				EnsureAudioAnalysisState();
				OnPropertyChanged("CurrentThemeBackgroundPath");
			}
		}
	}

	public bool IsMusicPlayerTransparent
	{
		get
		{
			return _isMusicPlayerTransparent;
		}
		private set
		{
			SetProperty(ref _isMusicPlayerTransparent, value, "IsMusicPlayerTransparent");
		}
	}

	public bool IsMusicVisualizerFullscreen
	{
		get
		{
			return _isMusicVisualizerFullscreen;
		}
		private set
		{
			SetProperty(ref _isMusicVisualizerFullscreen, value, "IsMusicVisualizerFullscreen");
		}
	}

	public bool IsMusicPlaying
	{
		get
		{
			return _isMusicPlaying;
		}
		set
		{
			if (SetProperty(ref _isMusicPlaying, value, "IsMusicPlaying"))
			{
				EnsureAudioAnalysisState();
				OnPropertyChanged("MusicPlayPauseText");
			}
		}
	}

	public bool IsShuffleEnabled
	{
		get
		{
			return _isShuffleEnabled;
		}
		set
		{
			if (SetProperty(ref _isShuffleEnabled, value, "IsShuffleEnabled"))
			{
				OnPropertyChanged("ShuffleText");
			}
		}
	}

	public bool IsBooting
	{
		get
		{
			return _isBooting;
		}
		set
		{
			SetProperty(ref _isBooting, value, "IsBooting");
		}
	}

	public DashboardTheme? SelectedTheme
	{
		get
		{
			return _selectedTheme;
		}
		set
		{
			SetProperty(ref _selectedTheme, value, "SelectedTheme");
		}
	}

	public string ThemeNameInput
	{
		get
		{
			return _themeNameInput;
		}
		set
		{
			SetProperty(ref _themeNameInput, value, "ThemeNameInput");
		}
	}

	public string ThemeHomePreviewPath
	{
		get
		{
			return _themeHomePreviewPath;
		}
		set
		{
			SetProperty(ref _themeHomePreviewPath, value, "ThemeHomePreviewPath");
		}
	}

	public string ThemeGamesPreviewPath
	{
		get
		{
			return _themeGamesPreviewPath;
		}
		set
		{
			SetProperty(ref _themeGamesPreviewPath, value, "ThemeGamesPreviewPath");
		}
	}

	public string ThemeSettingsPreviewPath
	{
		get
		{
			return _themeSettingsPreviewPath;
		}
		set
		{
			SetProperty(ref _themeSettingsPreviewPath, value, "ThemeSettingsPreviewPath");
		}
	}

	public string ThemeAppsPreviewPath
	{
		get
		{
			return _themeAppsPreviewPath;
		}
		set
		{
			SetProperty(ref _themeAppsPreviewPath, value, "ThemeAppsPreviewPath");
		}
	}

	public string SteamSetupApiKey
	{
		get
		{
			return _steamSetupApiKey;
		}
		set
		{
			SetProperty(ref _steamSetupApiKey, value?.Trim() ?? string.Empty, "SteamSetupApiKey");
		}
	}

	public string SteamSetupSteamId64
	{
		get
		{
			return _steamSetupSteamId64;
		}
		set
		{
			SetProperty(ref _steamSetupSteamId64, ExtractSteamId64(value ?? string.Empty), "SteamSetupSteamId64");
		}
	}

	public string SteamSetupStatus
	{
		get
		{
			return _steamSetupStatus;
		}
		set
		{
			SetProperty(ref _steamSetupStatus, value, "SteamSetupStatus");
		}
	}

	public string ThemeMenuVisibilityTitle => SelectedTheme?.Name ?? "Xbox 360";

	public string CurrentThemeBackgroundPath
	{
		get
		{
			string text = ResolveThemeSectionKey();
			if (SelectedTheme == null || SelectedTheme.IsBuiltIn || string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}
			string backgroundPath = SelectedTheme.GetBackgroundPath(text);
			if (!File.Exists(AppPaths.ResolvePath(backgroundPath)))
			{
				return string.Empty;
			}
			return backgroundPath;
		}
	}

	public string ClockText
	{
		get
		{
			return _clockText;
		}
		set
		{
			SetProperty(ref _clockText, value, "ClockText");
		}
	}

	public MusicTrackViewModel? CurrentMusicTrack
	{
		get
		{
			return _currentMusicTrack;
		}
		set
		{
			if (!SetProperty(ref _currentMusicTrack, value, "CurrentMusicTrack"))
			{
				return;
			}
			foreach (MusicTrackViewModel musicTrack in MusicTracks)
			{
				musicTrack.IsPlaying = musicTrack == value;
			}
			OnPropertyChanged("CurrentMusicTitle");
			OnPropertyChanged("MusicTrackCountText");
		}
	}

	public string CurrentMusicTitle => CurrentMusicTrack?.Title ?? "No music found";

	public string MusicTrackCountText
	{
		get
		{
			if (MusicTracks.Count != 0)
			{
				return $"{Math.Max(1, _musicIndex + 1)} of {MusicTracks.Count}";
			}
			return "0 of 0";
		}
	}

	public string MusicPlayPauseText
	{
		get
		{
			if (!IsMusicPlaying)
			{
				return "Play";
			}
			return "Pause";
		}
	}

	public string ShuffleText
	{
		get
		{
			if (!IsShuffleEnabled)
			{
				return "Shuffle";
			}
			return "Shuffle On";
		}
	}

	public string MusicPositionText
	{
		get
		{
			return _musicPositionText;
		}
		set
		{
			SetProperty(ref _musicPositionText, value, "MusicPositionText");
		}
	}

	public string MusicDurationText
	{
		get
		{
			return _musicDurationText;
		}
		set
		{
			SetProperty(ref _musicDurationText, value, "MusicDurationText");
		}
	}

	public double MusicProgress
	{
		get
		{
			return _musicProgress;
		}
		set
		{
			SetProperty(ref _musicProgress, value, "MusicProgress");
		}
	}

	public double MusicVolume
	{
		get
		{
			return _musicVolume;
		}
		set
		{
			double num = Math.Clamp(value, 0.0, 1.0);
			if (SetProperty(ref _musicVolume, num, "MusicVolume"))
			{
				_musicPlayer.Volume = num;
				OnPropertyChanged("MusicVolumeText");
			}
		}
	}

	public string MusicVolumeText => $"{Math.Round(MusicVolume * 100.0)}%";

	public double VisualizerBass
	{
		get
		{
			return _visualizerBass;
		}
		private set
		{
			SetProperty(ref _visualizerBass, value, "VisualizerBass");
		}
	}

	public double VisualizerMid
	{
		get
		{
			return _visualizerMid;
		}
		private set
		{
			SetProperty(ref _visualizerMid, value, "VisualizerMid");
		}
	}

	public double VisualizerTreble
	{
		get
		{
			return _visualizerTreble;
		}
		private set
		{
			SetProperty(ref _visualizerTreble, value, "VisualizerTreble");
		}
	}

	public double VisualizerLoudness
	{
		get
		{
			return _visualizerLoudness;
		}
		private set
		{
			SetProperty(ref _visualizerLoudness, value, "VisualizerLoudness");
		}
	}

	public double VisualizerPeak
	{
		get
		{
			return _visualizerPeak;
		}
		private set
		{
			SetProperty(ref _visualizerPeak, value, "VisualizerPeak");
		}
	}

	public string SpotlightTitle => FeaturedGame?.Title ?? "Xbox Metro Launcher";

	public string SpotlightSubtitle => FeaturedGame?.Subtitle ?? "Press Y to search or E to move across the dashboard.";

	public GameCardViewModel? TrayGame
	{
		get
		{
			return _trayGame;
		}
		set
		{
			if (SetProperty(ref _trayGame, value, "TrayGame"))
			{
				OnPropertyChanged("OpenTrayTitle");
				OnPropertyChanged("OpenTrayCoverArtPath");
			}
		}
	}

	public string OpenTrayTitle => TrayGame?.Title ?? "Open Tray";

	public string OpenTrayCoverArtPath => TrayGame?.BackgroundArtPath ?? string.Empty;

	public string MyGamesCountText
	{
		get
		{
			List<GameCardViewModel> list = Games.Where((GameCardViewModel game) => !IsAppEntry(game.Game)).ToList();
			int count = list.Count;
			if (count == 0)
			{
				return "0 of 17";
			}
			int value = ((SelectedGame == null) ? 1 : Math.Max(1, list.IndexOf(SelectedGame) + 1));
			return $"{value} of {count}";
		}
	}

	public string LibraryMenuTitle
	{
		get
		{
			if (!_isLibraryShowingPins)
			{
				if (!_isLibraryShowingApps)
				{
					return "My Games";
				}
				return "My Apps";
			}
			return "My Pins";
		}
	}

	public string LibraryMenuFilterText
	{
		get
		{
			if (!_isLibraryShowingPins)
			{
				if (!_isLibraryShowingApps)
				{
					return "all games";
				}
				return "all apps";
			}
			return "pinned games";
		}
	}

	public string LibraryMenuXHintText => " Game Details";

	public string LibraryMenuYHintText => " Pin";

	public IEnumerable<GameCardViewModel> LibraryMenuGames => GetLibraryMenuGames();

	public string LibraryMenuCountText
	{
		get
		{
			List<GameCardViewModel> list = LibraryMenuGames.ToList();
			if (list.Count == 0)
			{
				if (!_isLibraryShowingPins && !_isLibraryShowingApps)
				{
					return "0 of 17";
				}
				return "0 of 0";
			}
			int num = ((SelectedGame == null) ? 1 : (list.IndexOf(SelectedGame) + 1));
			if (num <= 0)
			{
				num = 1;
			}
			return $"{num} of {list.Count}";
		}
	}

	public bool HasRunningLaunchedGame => _runningGameService.HasRunningGame;

	public string RunningLaunchedGameTitle => _runningGameService.RunningGameTitle;

	public GameMetadata? RunningLaunchedGame => _runningGameService.CurrentGame;

	public string RunningGameFooterActionText => _runningGameService.State switch
	{
		RunningGameState.Launching => "Finding Game...", 
		RunningGameState.None => "No Game Running", 
		_ => "Close Game", 
	};

	public bool IsAudioAnalysisRunning => _audioAnalysisService.IsRunning;

	public bool IsMusicProgressTimerActive => _musicTimer.IsEnabled;

	public ICommand SelectGameCommand { get; }

	public ICommand LaunchGameCommand { get; }

	public ICommand SubmitSearchCommand { get; }

	public ICommand UseTrendingSearchCommand { get; }

	public ICommand OpenSearchCommand { get; }

	public ICommand CloseSearchCommand { get; }

	public ICommand ShowDetailsCommand { get; }

	public ICommand CloseDetailsCommand { get; }

	public ICommand SelectGameDetailsTabCommand { get; }

	public ICommand OpenGameDetailsExtrasSeeAllCommand { get; }

	public ICommand OpenGameDetailsExtraCommand { get; }

	public ICommand BackCommand { get; }

	public ICommand AddGameCommand { get; }

	public ICommand EditSelectedGameCommand { get; }

	public ICommand ScanFolderCommand { get; }

	public ICommand ToggleFavoriteCommand { get; }

	public ICommand OpenSelectedGameStoreCommand { get; }

	public ICommand SaveSettingsCommand { get; }

	public ICommand ExportDataCommand { get; }

	public ICommand ImportDataCommand { get; }

	public ICommand ScanSteamGamesCommand { get; }

	public ICommand OpenSteamSetupCommand { get; }

	public ICommand CloseSteamSetupCommand { get; }

	public ICommand SaveSteamSetupCommand { get; }

	public ICommand TestSteamSetupCommand { get; }

	public ICommand PasteSteamApiKeyCommand { get; }

	public ICommand PasteSteamIdCommand { get; }

	public ICommand OpenSteamApiKeyPageCommand { get; }

	public ICommand OpenSteamProfileHelpCommand { get; }

	public ICommand OpenThemeMenuCommand { get; }

	public ICommand CloseThemeMenuCommand { get; }

	public ICommand SelectThemeCommand { get; }

	public ICommand OpenThemeCreatorCommand { get; }

	public ICommand CloseThemeCreatorCommand { get; }

	public ICommand OpenDashboardCustomizerCommand { get; }

	public ICommand CloseDashboardCustomizerCommand { get; }

	public ICommand PreviousDashboardCustomizerTabCommand { get; }

	public ICommand NextDashboardCustomizerTabCommand { get; }

	public ICommand SelectDashboardTileCommand { get; }

	public ICommand ChooseDashboardTileImageCommand { get; }

	public ICommand ResetDashboardTileImageCommand { get; }

	public ICommand ResetDashboardTileTitleCommand { get; }

	public ICommand ResetDashboardTabImagesCommand { get; }

	public ICommand OpenDashboardTileColorPickerCommand { get; }

	public ICommand ResetDashboardTileColorCommand { get; }

	public ICommand ChooseThemeHomeImageCommand { get; }

	public ICommand ChooseThemeGamesImageCommand { get; }

	public ICommand ChooseThemeSettingsImageCommand { get; }

	public ICommand ChooseThemeAppsImageCommand { get; }

	public ICommand SaveThemeCommand { get; }

	public ICommand ToggleQuickMenuCommand { get; }

	public ICommand OpenMyGamesCommand { get; }

	public ICommand OpenMyAppsCommand { get; }

	public ICommand OpenMyPinsCommand { get; }

	public ICommand CloseMyGamesCommand { get; }

	public ICommand OpenLauncherSettingsCommand { get; }

	public ICommand CloseLauncherSettingsCommand { get; }

	public ICommand ChooseSelectedHomeImageCommand { get; }

	public ICommand ChooseSelectedGameMenuImageCommand { get; }

	public ICommand SaveSelectedGameCommand { get; }

	public ICommand SetOpenTrayGameCommand { get; }

	public ICommand RemoveSelectedGameCommand { get; }

	public ICommand OpenProfileEditorCommand { get; }

	public ICommand CloseProfileEditorCommand { get; }

	public ICommand OpenMusicPlayerCommand { get; }

	public ICommand CloseMusicPlayerCommand { get; }

	public ICommand OpenMusicFolderCommand { get; }

	public ICommand OpenMusicVisualizerFullscreenCommand { get; }

	public ICommand PlayPauseMusicCommand { get; }

	public ICommand StopMusicCommand { get; }

	public ICommand NextMusicCommand { get; }

	public ICommand PreviousMusicCommand { get; }

	public ICommand ToggleShuffleMusicCommand { get; }

	public ICommand VolumeDownCommand { get; }

	public ICommand VolumeUpCommand { get; }

	public ICommand PlaySelectedMusicCommand { get; }

	public ICommand ChooseProfilePictureCommand { get; }

	public ICommand SaveProfileCommand { get; }

	public ICommand ShutdownCommand { get; }

	public ICommand OpenYouTubeCommand { get; }

	public ICommand OpenFriendsOverlayCommand { get; }

	public ICommand SwitchTabCommand { get; }

	public event EventHandler? FriendsOverlayRequested;

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	public DashboardViewModel(IGameLibraryService libraryService, IGameLaunchService launchService, ISearchService searchService, ISettingsService settingsService, IProfileService profileService, IFilePickerService filePickerService, IImportExportService importExportService, ISteamLibraryScannerService steamLibraryScannerService, ISteamCommunityService steamCommunityService, IThemeService themeService, IStartupRegistrationService startupRegistrationService, IAudioService audioService, SocialIntegrationManager socialIntegrationManager, IRunningGameService runningGameService)
	{
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Expected O, but got Unknown
		_libraryService = libraryService;
		_launchService = launchService;
		_searchService = searchService;
		_settingsService = settingsService;
		_profileService = profileService;
		_filePickerService = filePickerService;
		_importExportService = importExportService;
		_steamLibraryScannerService = steamLibraryScannerService;
		_steamCommunityService = steamCommunityService;
		_themeService = themeService;
		_startupRegistrationService = startupRegistrationService;
		_audioService = audioService;
		_socialIntegrationManager = socialIntegrationManager;
		_runningGameService = runningGameService;
		_musicPlayer.Volume = _musicVolume;
		_musicPlayer.MediaOpened += delegate
		{
			RefreshMusicProgress();
		};
		_musicPlayer.MediaEnded += delegate
		{
			NextMusicTrack();
		};
		_audioAnalysisService.FrameReady += AudioAnalysis_OnFrameReady;
		_musicTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		_musicTimer.Tick += delegate
		{
			RefreshMusicProgress();
		};
		int num = 6;
		List<System.Windows.Media.Brush> list = new List<System.Windows.Media.Brush>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<System.Windows.Media.Brush> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 156, 74));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(202, 80, 16));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(116, 77, 169));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 161, 156));
		num2++;
		span[num2] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 40, 71));
		num2++;
		_accentBrushes = list;
		Tabs = new ObservableCollection<DashboardTabViewModel>
		{
			new BingTabViewModel(this),
			new HomeTabViewModel(this),
			new SocialTabViewModel(this),
			new MediaTabViewModel(this),
			new GamesTabViewModel(this),
			new MusicTabViewModel(this),
			new AppsTabViewModel(this),
			new SettingsTabViewModel(this)
		};
		foreach (DashboardTabCustomizationViewModel item in BuildDashboardCustomizationTabs())
		{
			DashboardCustomizationTabs.Add(item);
		}
		Games.CollectionChanged += OnGamesChanged;
		_runningGameService.StateChanged += RunningGameService_OnStateChanged;
		GameDetailsTabs = new ObservableCollection<GameDetailsTabViewModel>
		{
			new GameDetailsTabViewModel("overview", "overview"),
			new GameDetailsTabViewModel("details", "details"),
			new GameDetailsTabViewModel("extras", "extras"),
			new GameDetailsTabViewModel("gallery", "gallery")
		};
		GameDetailsTabs[0].IsSelected = true;
		SelectGameCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectGame(parameter as GameCardViewModel);
		});
		LaunchGameCommand = new AsyncRelayCommand((object? parameter) => LaunchGameAsync(parameter as GameCardViewModel));
		SubmitSearchCommand = new AsyncRelayCommand(SubmitSearchAsync);
		UseTrendingSearchCommand = new RelayCommand(delegate(object? parameter)
		{
			SearchQuery = parameter?.ToString() ?? string.Empty;
			SubmitSearchAsync();
		});
		OpenSearchCommand = new RelayCommand(OpenSearch);
		CloseSearchCommand = new RelayCommand((Action)delegate
		{
			IsSearchOverlayOpen = false;
		});
		ShowDetailsCommand = new RelayCommand(OpenGameDetails);
		CloseDetailsCommand = new RelayCommand((Action)delegate
		{
			IsDetailsOpen = false;
		});
		SelectGameDetailsTabCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectGameDetailsTab(parameter?.ToString() ?? string.Empty);
		});
		OpenGameDetailsExtrasSeeAllCommand = new RelayCommand(delegate
		{
			OpenGameDetailsSeeAll();
		}, null);
		OpenGameDetailsExtraCommand = new RelayCommand(OpenGameDetailsExtra);
		BackCommand = new RelayCommand(GoBack);
		AddGameCommand = new AsyncRelayCommand(AddGameAsync);
		EditSelectedGameCommand = new AsyncRelayCommand(EditSelectedGameAsync);
		ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync);
		ToggleFavoriteCommand = new AsyncRelayCommand(ToggleFavoriteAsync, (object? _) => SelectedGame != null);
		OpenSelectedGameStoreCommand = new RelayCommand(OpenSelectedGameStore);
		SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
		ExportDataCommand = new AsyncRelayCommand(ExportDataAsync);
		ImportDataCommand = new AsyncRelayCommand(ImportDataAsync);
		ScanSteamGamesCommand = new AsyncRelayCommand(ScanSteamGamesAsync);
		OpenSteamSetupCommand = new AsyncRelayCommand(OpenSteamSetupAsync);
		CloseSteamSetupCommand = new RelayCommand((Action)delegate
		{
			IsSteamSetupOpen = false;
		});
		SaveSteamSetupCommand = new AsyncRelayCommand(SaveSteamSetupAsync);
		TestSteamSetupCommand = new AsyncRelayCommand(TestSteamSetupAsync);
		PasteSteamApiKeyCommand = new RelayCommand((Action)delegate
		{
			SteamSetupApiKey = GetClipboardText();
		});
		PasteSteamIdCommand = new RelayCommand((Action)delegate
		{
			SteamSetupSteamId64 = ExtractSteamId64(GetClipboardText());
		});
		OpenSteamApiKeyPageCommand = new RelayCommand((Action)delegate
		{
			OpenExternalUrl("https://steamcommunity.com/dev/apikey", "Opening Steam API key page");
		});
		OpenSteamProfileHelpCommand = new RelayCommand((Action)delegate
		{
			OpenExternalUrl("https://steamid.io/lookup", "Opening SteamID lookup");
		});
		OpenThemeMenuCommand = new RelayCommand(OpenThemeMenu);
		CloseThemeMenuCommand = new RelayCommand((Action)delegate
		{
			IsThemeMenuOpen = false;
		});
		SelectThemeCommand = new AsyncRelayCommand(SelectThemeAsync);
		OpenThemeCreatorCommand = new RelayCommand(OpenThemeCreator);
		CloseThemeCreatorCommand = new RelayCommand(CloseThemeCreator);
		OpenDashboardCustomizerCommand = new RelayCommand(OpenDashboardCustomizer);
		CloseDashboardCustomizerCommand = new RelayCommand(CloseDashboardCustomizer);
		PreviousDashboardCustomizerTabCommand = new RelayCommand(delegate
		{
			MoveDashboardCustomizerTab(-1);
		}, null);
		NextDashboardCustomizerTabCommand = new RelayCommand(delegate
		{
			MoveDashboardCustomizerTab(1);
		}, null);
		SelectDashboardTileCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectDashboardTile(parameter as DashboardTileCustomizationViewModel);
		});
		ChooseDashboardTileImageCommand = new AsyncRelayCommand(ChooseDashboardTileImageAsync);
		ResetDashboardTileImageCommand = new AsyncRelayCommand(ResetDashboardTileImageAsync);
		ResetDashboardTileTitleCommand = new AsyncRelayCommand(ResetDashboardTileTitleAsync);
		ResetDashboardTabImagesCommand = new AsyncRelayCommand(ResetDashboardTabImagesAsync);
		OpenDashboardTileColorPickerCommand = new RelayCommand(OpenDashboardTileColorPicker);
		ResetDashboardTileColorCommand = new AsyncRelayCommand(ResetDashboardTileColorAsync);
		ChooseThemeHomeImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("home"));
		ChooseThemeGamesImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("games"));
		ChooseThemeSettingsImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("settings"));
		ChooseThemeAppsImageCommand = new AsyncRelayCommand((object? _) => ChooseThemeSectionImageAsync("apps"));
		SaveThemeCommand = new AsyncRelayCommand(SaveThemeAsync);
		ToggleQuickMenuCommand = new RelayCommand((Action)delegate
		{
			IsQuickMenuOpen = !IsQuickMenuOpen;
		});
		OpenMyGamesCommand = new RelayCommand(OpenMyGames);
		OpenMyAppsCommand = new RelayCommand(OpenMyApps);
		OpenMyPinsCommand = new RelayCommand(OpenMyPins);
		CloseMyGamesCommand = new RelayCommand((Action)delegate
		{
			IsMyGamesOpen = false;
		});
		OpenLauncherSettingsCommand = new RelayCommand(OpenLauncherSettings);
		CloseLauncherSettingsCommand = new RelayCommand((Action)delegate
		{
			IsLauncherSettingsOpen = false;
		});
		ChooseSelectedHomeImageCommand = new AsyncRelayCommand(ChooseSelectedHomeImageAsync);
		ChooseSelectedGameMenuImageCommand = new AsyncRelayCommand(ChooseSelectedGameMenuImageAsync);
		SaveSelectedGameCommand = new AsyncRelayCommand(SaveSelectedGameAsync);
		SetOpenTrayGameCommand = new AsyncRelayCommand(SetOpenTrayGameAsync);
		RemoveSelectedGameCommand = new AsyncRelayCommand(RemoveSelectedGameAsync);
		OpenProfileEditorCommand = new RelayCommand(OpenProfileEditor);
		CloseProfileEditorCommand = new RelayCommand((Action)delegate
		{
			IsProfileEditorOpen = false;
		});
		OpenMusicPlayerCommand = new RelayCommand(delegate(object? parameter)
		{
			bool flag = default(bool);
			int num3;
			if (parameter is bool)
			{
				flag = (bool)parameter;
				num3 = 1;
			}
			else
			{
				num3 = 0;
			}
			OpenMusicPlayer((byte)((uint)num3 & (flag ? 1u : 0u)) != 0);
		});
		CloseMusicPlayerCommand = new RelayCommand(CloseMusicPlayer);
		OpenMusicFolderCommand = new RelayCommand(OpenMusicFolder);
		OpenMusicVisualizerFullscreenCommand = new RelayCommand(OpenMusicVisualizerFullscreen);
		PlayPauseMusicCommand = new RelayCommand(ToggleMusicPlayback);
		StopMusicCommand = new RelayCommand(StopMusic);
		NextMusicCommand = new RelayCommand(NextMusicTrack);
		PreviousMusicCommand = new RelayCommand(PreviousMusicTrack);
		ToggleShuffleMusicCommand = new RelayCommand((Action)delegate
		{
			IsShuffleEnabled = !IsShuffleEnabled;
		});
		VolumeDownCommand = new RelayCommand((Action)delegate
		{
			MusicVolume -= 0.05;
		});
		VolumeUpCommand = new RelayCommand((Action)delegate
		{
			MusicVolume += 0.05;
		});
		PlaySelectedMusicCommand = new RelayCommand(delegate(object? parameter)
		{
			PlayMusicTrack(parameter as MusicTrackViewModel);
		});
		ChooseProfilePictureCommand = new AsyncRelayCommand(ChooseProfilePictureAsync);
		SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync);
		ShutdownCommand = new AsyncRelayCommand(ShutdownAsync);
		OpenYouTubeCommand = new RelayCommand(OpenYouTube);
		OpenFriendsOverlayCommand = new RelayCommand(RequestFriendsOverlay);
		SwitchTabCommand = new RelayCommand(delegate(object? parameter)
		{
			if (parameter is DashboardTabViewModel currentTab)
			{
				CurrentTab = currentTab;
			}
		});
		CurrentTab = Tabs[1];
		UpdateClock();
	}

	public async Task InitializeAsync()
	{
		await ReloadSavedDataAsync();
		await _settingsService.SaveAsync(Settings);
	}

	private async Task ReloadSavedDataAsync()
	{
		Settings = await _settingsService.LoadAsync();
		await LoadThemesAsync();
		Profile = await _profileService.LoadAsync();
		EnsureProfileDefaults();
		Settings.ThemeName = NormalizeThemeName(Settings.ThemeName);
		ApplySelectedTheme(Settings.ThemeName);
		Settings.SocialIntegrationMode = SocialIntegrationMode.LocalOnly;
		Settings.DiscordUserId = string.Empty;
		Settings.DiscordDisplayName = string.Empty;
		Settings.DiscordAvatarPathOrUrl = string.Empty;
		Settings.DiscordAccessTokenEncrypted = string.Empty;
		Settings.DiscordGrantedScopes = string.Empty;
		Settings.DiscordTokenType = string.Empty;
		_library = await _libraryService.LoadAsync();
		foreach (GameMetadata game in _library.Games)
		{
			GameMetadata gameMetadata = game;
			if (gameMetadata.Title == null)
			{
				gameMetadata.Title = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.Platform == null)
			{
				gameMetadata.Platform = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.Genre == null)
			{
				gameMetadata.Genre = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.MultiplayerInfo == null)
			{
				gameMetadata.MultiplayerInfo = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.CoOpInfo == null)
			{
				gameMetadata.CoOpInfo = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.ExecutablePath == null)
			{
				gameMetadata.ExecutablePath = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.Arguments == null)
			{
				gameMetadata.Arguments = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.WorkingDirectory == null)
			{
				gameMetadata.WorkingDirectory = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.CoverArtPath == null)
			{
				gameMetadata.CoverArtPath = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.BackgroundArtPath == null)
			{
				gameMetadata.BackgroundArtPath = string.Empty;
			}
			game.LaunchType = (string.IsNullOrWhiteSpace(game.LaunchType) ? "Exe" : game.LaunchType);
			gameMetadata = game;
			if (gameMetadata.SteamAppId == null)
			{
				gameMetadata.SteamAppId = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.InstallPath == null)
			{
				gameMetadata.InstallPath = string.Empty;
			}
			gameMetadata = game;
			if (gameMetadata.LaunchCommand == null)
			{
				gameMetadata.LaunchCommand = string.Empty;
			}
		}
		_library.Games = _library.Games.OrderBy<GameMetadata, string>((GameMetadata game) => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
		SyncGamesCollectionFromLibrary();
		SelectedGame = Games.FirstOrDefault((GameCardViewModel game) => game.Game.IsFavorite) ?? Games.FirstOrDefault();
		FeaturedGame = SelectedGame;
		TrayGame = Games.FirstOrDefault((GameCardViewModel game) => string.Equals(game.Game.Id, Settings.OpenTrayGameId, StringComparison.OrdinalIgnoreCase));
		RefreshDerivedLists();
		LoadMusicLibrary();
		ResetPendingThemeDraft();
	}

	public void UpdateClock()
	{
		ClockText = DateTime.Now.ToString("h:mm tt  ddd, MMM d");
	}

	public void HandleInput(DashboardInputAction action)
	{
		switch (action)
		{
		case DashboardInputAction.PreviousTab:
			if (IsDetailsOpen)
			{
				MoveGameDetailsTab(-1);
			}
			else
			{
				MoveTab(-1);
			}
			break;
		case DashboardInputAction.NextTab:
			if (IsDetailsOpen)
			{
				MoveGameDetailsTab(1);
			}
			else
			{
				MoveTab(1);
			}
			break;
		case DashboardInputAction.Back:
			GoBack();
			break;
		case DashboardInputAction.Details:
			if (IsMusicPlayerOpen)
			{
				OpenMusicVisualizerFullscreen();
			}
			else if (IsMyGamesOpen)
			{
				OpenGameDetails();
			}
			else
			{
				OpenGameDetails();
			}
			break;
		case DashboardInputAction.Search:
			if (IsDetailsOpen)
			{
				SetOpenTrayGameAsync(null);
			}
			else if (IsMyGamesOpen)
			{
				ToggleFavoriteAsync(null);
			}
			else
			{
				OpenSearch();
			}
			break;
		case DashboardInputAction.Options:
			IsQuickMenuOpen = !IsQuickMenuOpen;
			break;
		default:
			_audioService.Play("focus");
			break;
		case DashboardInputAction.Activate:
			break;
		}
	}

	public void MoveTab(int delta)
	{
		if (CurrentTab == null)
		{
			CurrentTab = Tabs[1];
			return;
		}
		int num = Tabs.IndexOf(CurrentTab);
		int num2 = Math.Clamp(num + delta, 0, Tabs.Count - 1);
		if (num2 != num)
		{
			_pendingTabSound = ((delta < 0) ? "page-left" : "page-right");
			CurrentTab = Tabs[num2];
		}
	}

	public void MoveGameDetailsTab(int delta)
	{
		if (IsDetailsOpen && GameDetailsTabs.Count != 0)
		{
			if (IsGameDetailsSeeAllOpen)
			{
				IsGameDetailsSeeAllOpen = false;
			}
			int num = Math.Clamp(_selectedGameDetailsTabIndex + delta, 0, GameDetailsTabs.Count - 1);
			if (num != _selectedGameDetailsTabIndex)
			{
				SetGameDetailsTab(num);
				_audioService.Play((delta < 0) ? "page-left" : "page-right");
			}
		}
	}

	private void OpenGameDetails()
	{
		if (SelectedGame != null)
		{
			SetGameDetailsTab(0);
			IsGameDetailsSeeAllOpen = false;
			RefreshGameDetailsPanels();
			IsDetailsOpen = true;
			_audioService.Play("select");
		}
	}

	private void SelectGameDetailsTab(string key)
	{
		int num = GameDetailsTabs.Select((GameDetailsTabViewModel tab, int tabIndex) => new { tab, tabIndex }).FirstOrDefault(item => string.Equals(item.tab.Key, key, StringComparison.OrdinalIgnoreCase))?.tabIndex ?? (-1);
		if (num >= 0)
		{
			IsGameDetailsSeeAllOpen = false;
			if (num != _selectedGameDetailsTabIndex)
			{
				int num2 = ((num >= _selectedGameDetailsTabIndex) ? 1 : (-1));
				SetGameDetailsTab(num);
				_audioService.Play((num2 < 0) ? "page-left" : "page-right");
			}
		}
	}

	private void SetGameDetailsTab(int index)
	{
		_selectedGameDetailsTabIndex = Math.Clamp(index, 0, GameDetailsTabs.Count - 1);
		for (int i = 0; i < GameDetailsTabs.Count; i++)
		{
			GameDetailsTabs[i].IsSelected = i == _selectedGameDetailsTabIndex;
		}
		OnPropertyChanged("SelectedGameDetailsTabKey");
		NotifyGameDetailsTabVisibility();
	}

	private void OpenGameDetailsSeeAll()
	{
		SelectGameDetailsTab("extras");
		IsGameDetailsSeeAllOpen = true;
		_audioService.Play("select");
	}

	private void NotifyGameDetailsTabVisibility()
	{
		OnPropertyChanged("IsGameDetailsOverviewTab");
		OnPropertyChanged("IsGameDetailsDetailsTab");
		OnPropertyChanged("IsGameDetailsExtrasTab");
		OnPropertyChanged("IsGameDetailsGalleryTab");
	}

	public void SelectGame(GameCardViewModel? game)
	{
		if (game != null)
		{
			SelectedGame = game;
			_audioService.Play("focus");
		}
	}

	private void OpenMyGames()
	{
		OpenLibraryMenu(showPins: false, showApps: false);
	}

	private void OpenMyApps()
	{
		OpenLibraryMenu(showPins: false, showApps: true);
	}

	private void OpenMyPins()
	{
		OpenLibraryMenu(showPins: true, showApps: false);
	}

	private void OpenLibraryMenu(bool showPins, bool showApps)
	{
		_isLibraryShowingPins = showPins;
		_isLibraryShowingApps = showApps;
		List<GameCardViewModel> list = LibraryMenuGames.ToList();
		if (list.Count > 0 && (SelectedGame == null || !list.Contains(SelectedGame)))
		{
			SelectedGame = list.FirstOrDefault();
		}
		IsMyGamesOpen = true;
		IsLauncherSettingsOpen = false;
		IsProfileEditorOpen = false;
		IsThemeMenuOpen = false;
		IsThemeCreatorOpen = false;
		IsSteamSetupOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("LibraryMenuTitle");
		OnPropertyChanged("LibraryMenuFilterText");
		OnPropertyChanged("LibraryMenuGames");
		RefreshVisibleLibraryMenuGames();
		OnPropertyChanged("LibraryMenuCountText");
		OnPropertyChanged("LibraryMenuXHintText");
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("select");
	}

	private void OpenLauncherSettings()
	{
		IsLauncherSettingsOpen = true;
		IsMyGamesOpen = false;
		IsProfileEditorOpen = false;
		IsThemeMenuOpen = false;
		IsSteamSetupOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("select");
	}

	private void OpenProfileEditor()
	{
		IsProfileEditorOpen = true;
		IsMyGamesOpen = false;
		IsLauncherSettingsOpen = false;
		IsThemeMenuOpen = false;
		IsThemeCreatorOpen = false;
		IsSteamSetupOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("select");
	}

	private void OpenThemeMenu()
	{
		IsThemeMenuOpen = true;
		IsThemeCreatorOpen = false;
		IsDashboardCustomizerOpen = false;
		IsSteamSetupOpen = false;
		IsMyGamesOpen = false;
		IsLauncherSettingsOpen = false;
		IsProfileEditorOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		_audioService.Play("select");
	}

	private void OpenThemeCreator()
	{
		IsThemeCreatorOpen = true;
		IsDashboardCustomizerOpen = false;
		IsThemeMenuOpen = false;
		IsSteamSetupOpen = false;
		IsMyGamesOpen = false;
		IsProfileEditorOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		if (!IsLauncherSettingsOpen)
		{
			IsLauncherSettingsOpen = true;
		}
		ResetPendingThemeDraft();
		_audioService.Play("select");
	}

	private void CloseThemeCreator()
	{
		IsThemeCreatorOpen = false;
		_audioService.Play("back");
	}

	private void OpenDashboardCustomizer()
	{
		IsDashboardCustomizerOpen = true;
		IsThemeCreatorOpen = false;
		IsThemeMenuOpen = false;
		IsSteamSetupOpen = false;
		IsMyGamesOpen = false;
		IsProfileEditorOpen = false;
		IsMusicPlayerOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		if (!IsLauncherSettingsOpen)
		{
			IsLauncherSettingsOpen = true;
		}
		_customizerTabIndex = Math.Clamp(_customizerTabIndex, 0, Math.Max(0, DashboardCustomizationTabs.Count - 1));
		SelectDefaultDashboardCustomizerTile();
		OnPropertyChanged("CurrentDashboardCustomizationTab");
		OnPropertyChanged("CurrentDashboardCustomizationTabName");
		_audioService.Play("select");
	}

	private void CloseDashboardCustomizer()
	{
		IsDashboardCustomizerOpen = false;
		_audioService.Play("back");
	}

	private void OpenMusicPlayer(bool transparent = false)
	{
		IsMusicPlayerTransparent = transparent;
		IsMusicVisualizerFullscreen = false;
		LoadMusicLibrary();
		IsMusicPlayerOpen = true;
		IsMyGamesOpen = false;
		IsLauncherSettingsOpen = false;
		IsProfileEditorOpen = false;
		IsThemeMenuOpen = false;
		IsThemeCreatorOpen = false;
		IsSteamSetupOpen = false;
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("select");
	}

	private void CloseMusicPlayer()
	{
		IsMusicVisualizerFullscreen = false;
		IsMusicPlayerOpen = false;
		IsMusicPlayerTransparent = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		_audioService.Play("back");
	}

	private void OpenMusicFolder()
	{
		string musicFolder = GetMusicFolder();
		Process.Start(new ProcessStartInfo
		{
			FileName = "explorer.exe",
			Arguments = "\"" + musicFolder + "\"",
			UseShellExecute = true
		});
	}

	private void OpenMusicVisualizerFullscreen()
	{
		if (IsMusicPlayerOpen && !IsMusicVisualizerFullscreen)
		{
			IsMusicVisualizerFullscreen = true;
			_audioService.Play("select");
		}
	}

	private async Task LaunchGameAsync(GameCardViewModel? card)
	{
		if (card == null)
		{
			card = SelectedGame;
		}
		if (card == null)
		{
			StatusMessage = "No game selected";
			return;
		}
		try
		{
			SelectedGame = card;
			_runningGameService.BeginLaunch(card.Game, DateTimeOffset.UtcNow);
			if (Settings.MinimizeOnGameLaunch)
			{
				Window window = System.Windows.Application.Current?.MainWindow;
				if (window != null)
				{
					window.WindowState = WindowState.Minimized;
				}
			}
			GameLaunchResult gameLaunchResult = await _launchService.LaunchAsync(card.Game);
			_runningGameService.Track(card.Game, gameLaunchResult.TrackedProcess);
			await BringLaunchedGameToForegroundAsync(gameLaunchResult.TrackedProcess);
			card.Game.LastPlayed = DateTimeOffset.Now;
			if (card.Game.Playtime < TimeSpan.Zero)
			{
				card.Game.Playtime = TimeSpan.Zero;
			}
			await PersistLibraryAsync();
			StatusMessage = "Launching " + card.Title;
			_audioService.Play("select");
		}
		catch (Exception ex)
		{
			_runningGameService.Clear();
			StatusMessage = ex.Message;
		}
	}

	private static async Task BringLaunchedGameToForegroundAsync(Process? process)
	{
		if (process == null)
		{
			return;
		}
		for (int attempt = 0; attempt < 10; attempt++)
		{
			try
			{
				if (process.HasExited)
				{
					break;
				}
				process.Refresh();
				nint mainWindowHandle = process.MainWindowHandle;
				if (mainWindowHandle != IntPtr.Zero)
				{
					ShowWindow(mainWindowHandle, 9);
					SetForegroundWindow(mainWindowHandle);
					break;
				}
			}
			catch
			{
				break;
			}
			await Task.Delay(250);
		}
	}

	private async Task SubmitSearchAsync()
	{
		if (string.IsNullOrWhiteSpace(SearchQuery))
		{
			StatusMessage = "Type a Bing search first";
			return;
		}
		await _searchService.SearchWebAsync(SearchQuery, Settings.BingSearchBaseUrl);
		StatusMessage = "Searching Bing for " + SearchQuery;
		IsSearchOverlayOpen = false;
		_audioService.Play("select");
	}

	private void OpenSearch()
	{
		CurrentTab = Tabs.First((DashboardTabViewModel tab) => tab.Key == "bing");
		IsSearchOverlayOpen = true;
		_audioService.Play("select");
	}

	private void RequestFriendsOverlay()
	{
		IsQuickMenuOpen = false;
		IsDetailsOpen = false;
		this.FriendsOverlayRequested?.Invoke(this, EventArgs.Empty);
	}

	public Task<RunningGameCloseResult> CloseRunningGameAsync(bool forceKill, CancellationToken cancellationToken = default(CancellationToken))
	{
		return _runningGameService.CloseAsync(forceKill, cancellationToken);
	}

	private void GoBack()
	{
		if (IsSearchOverlayOpen)
		{
			IsSearchOverlayOpen = false;
			_audioService.Play("back");
		}
		else if (IsDetailsOpen)
		{
			if (IsGameDetailsSeeAllOpen)
			{
				IsGameDetailsSeeAllOpen = false;
				_audioService.Play("back");
			}
			else
			{
				IsDetailsOpen = false;
				_audioService.Play("back");
			}
		}
		else if (IsThemeCreatorOpen)
		{
			IsThemeCreatorOpen = false;
			_audioService.Play("back");
		}
		else if (IsSteamSetupOpen)
		{
			IsSteamSetupOpen = false;
			_audioService.Play("back");
		}
		else if (IsThemeMenuOpen)
		{
			IsThemeMenuOpen = false;
			_audioService.Play("back");
		}
		else if (IsMyGamesOpen)
		{
			IsMyGamesOpen = false;
			OnPropertyChanged("CurrentThemeBackgroundPath");
			_audioService.Play("back");
		}
		else if (IsLauncherSettingsOpen)
		{
			IsLauncherSettingsOpen = false;
			OnPropertyChanged("CurrentThemeBackgroundPath");
			_audioService.Play("back");
		}
		else if (IsProfileEditorOpen)
		{
			IsProfileEditorOpen = false;
			_audioService.Play("back");
		}
		else if (IsMusicPlayerOpen)
		{
			if (IsMusicVisualizerFullscreen)
			{
				IsMusicVisualizerFullscreen = false;
			}
			else
			{
				IsMusicPlayerOpen = false;
			}
			_audioService.Play("back");
		}
		else if (IsQuickMenuOpen)
		{
			IsQuickMenuOpen = false;
			_audioService.Play("back");
		}
		else
		{
			CurrentTab = Tabs[1];
			_audioService.Play("back");
		}
	}

	private async Task AddGameAsync()
	{
		string text = _filePickerService.PickExecutable();
		if (!string.IsNullOrWhiteSpace(text))
		{
			string destination = NormalizeAddDestination(Settings.DefaultAddDestination);
			GameMetadata game = new GameMetadata
			{
				Title = Path.GetFileNameWithoutExtension(text).Replace("_", " "),
				LaunchType = "Exe",
				ExecutablePath = text,
				WorkingDirectory = (Path.GetDirectoryName(text) ?? string.Empty),
				Platform = "PC",
				Genre = ((destination == "My Apps") ? "App" : "Manual")
			};
			_library.Games.Add(game);
			GameCardViewModel item = new GameCardViewModel(game, _accentBrushes[Games.Count % _accentBrushes.Count]);
			Games.Add(item);
			SortGamesByTitle(game.Id);
			await PersistLibraryAsync();
			OnPropertyChanged("MyGamesCountText");
			StatusMessage = "Added " + game.Title + " to " + destination;
		}
	}

	private async Task EditSelectedGameAsync(object? _)
	{
		if (SelectedGame != null)
		{
			string text = _filePickerService.PickExecutable();
			if (!string.IsNullOrWhiteSpace(text))
			{
				SelectedGame.Game.ExecutablePath = text;
				SelectedGame.Game.WorkingDirectory = Path.GetDirectoryName(text) ?? string.Empty;
				await PersistLibraryAsync();
				StatusMessage = "Updated " + SelectedGame.Title;
			}
		}
	}

	private async Task ChooseSelectedHomeImageAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		string text = _filePickerService.PickImage(GetCustomCoverFolder("Home Screen Cover"));
		if (!string.IsNullOrWhiteSpace(text))
		{
			SelectedGame.Game.BackgroundArtPath = CopyCustomArtwork(text, "Home Screen Cover", SelectedGame.Title);
			SelectedGame.Refresh();
			if (TrayGame == SelectedGame)
			{
				OnPropertyChanged("OpenTrayCoverArtPath");
			}
			await PersistLibraryAsync();
			StatusMessage = "Updated Home image for " + SelectedGame.Title;
		}
	}

	private async Task ChooseSelectedGameMenuImageAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		string text = _filePickerService.PickImage(GetCustomCoverFolder("Game Menu Cover"));
		if (!string.IsNullOrWhiteSpace(text))
		{
			SelectedGame.Game.CoverArtPath = CopyCustomArtwork(text, "Game Menu Cover", SelectedGame.Title);
			SelectedGame.Refresh();
			await PersistLibraryAsync();
			StatusMessage = "Updated My Games image for " + SelectedGame.Title;
		}
	}

	private async Task SaveSelectedGameAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		SelectedGame.Refresh();
		if (TrayGame == SelectedGame)
		{
			OnPropertyChanged("OpenTrayTitle");
		}
		SortGamesByTitle(SelectedGame.Game.Id);
		await PersistLibraryAsync();
		StatusMessage = "Saved " + SelectedGame.Title;
	}

	private async Task SetOpenTrayGameAsync(object? _)
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		TrayGame = SelectedGame;
		Settings.OpenTrayGameId = SelectedGame.Game.Id;
		await SaveSettingsAsync();
		StatusMessage = SelectedGame.Title + " is now on Open Tray";
	}

	private async Task RemoveSelectedGameAsync()
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Choose a game first";
			return;
		}
		GameCardViewModel removed = SelectedGame;
		_library.Games.Remove(removed.Game);
		Games.Remove(removed);
		if (string.Equals(Settings.OpenTrayGameId, removed.Game.Id, StringComparison.OrdinalIgnoreCase))
		{
			Settings.OpenTrayGameId = string.Empty;
			TrayGame = null;
			await _settingsService.SaveAsync(Settings);
		}
		SelectedGame = Games.FirstOrDefault();
		FeaturedGame = SelectedGame;
		SortGamesByTitle(SelectedGame?.Game.Id);
		await PersistLibraryAsync();
		StatusMessage = "Removed " + removed.Title + " from My Games";
	}

	private async Task ChooseProfilePictureAsync(object? _)
	{
		string text = _filePickerService.PickImage(Path.Combine(AppPaths.AppFolder, "Assets", "Profile"));
		if (!string.IsNullOrWhiteSpace(text))
		{
			Profile = new Profile
			{
				Gamertag = Profile.Gamertag,
				GamerPicturePath = text,
				Gamerscore = Profile.Gamerscore,
				OnlineStatus = Profile.OnlineStatus,
				Motto = Profile.Motto,
				Description = Profile.Description
			};
			await _profileService.SaveAsync(Profile);
			StatusMessage = "Profile picture updated";
		}
	}

	private async Task SaveProfileAsync()
	{
		EnsureProfileDefaults();
		await _profileService.SaveAsync(Profile);
		OnPropertyChanged("Profile");
		StatusMessage = "Profile saved";
	}

	private async Task ShutdownAsync()
	{
		await _settingsService.SaveAsync(Settings);
		await _profileService.SaveAsync(Profile);
		System.Windows.Application.Current.Shutdown();
	}

	private async Task OpenSteamSetupAsync()
	{
		SteamCommunityConfig steamCommunityConfig = await _steamCommunityService.LoadConfigAsync();
		SteamSetupApiKey = steamCommunityConfig.SteamApiKey;
		SteamSetupSteamId64 = steamCommunityConfig.SteamId64;
		SteamSetupStatus = (_steamCommunityService.IsConfigured ? "Steam is connected. Use Test Connection to check it." : "Paste your Steam Web API key and SteamID64.");
		IsSteamSetupOpen = true;
		_audioService.Play("select");
	}

	private async Task SaveSteamSetupAsync()
	{
		SteamCommunityConfig steamCommunityConfig = BuildSteamSetupConfig();
		if (string.IsNullOrWhiteSpace(steamCommunityConfig.SteamApiKey) || string.IsNullOrWhiteSpace(steamCommunityConfig.SteamId64))
		{
			SteamSetupStatus = "Steam API key and SteamID64 are both required.";
			_audioService.Play("back");
			return;
		}
		await _steamCommunityService.SaveConfigAsync(steamCommunityConfig);
		SteamSetupStatus = "Steam setup saved.";
		StatusMessage = "Steam setup saved";
		_audioService.Play("select");
	}

	private async Task TestSteamSetupAsync()
	{
		SteamSetupStatus = "Testing Steam connection...";
		SteamConnectionTestResult steamConnectionTestResult = await _steamCommunityService.TestConnectionAsync(BuildSteamSetupConfig());
		SteamSetupStatus = steamConnectionTestResult.Message;
		StatusMessage = (steamConnectionTestResult.Success ? ("Steam connected: " + steamConnectionTestResult.DisplayName) : steamConnectionTestResult.Message);
		_audioService.Play(steamConnectionTestResult.Success ? "select" : "back");
	}

	private SteamCommunityConfig BuildSteamSetupConfig()
	{
		return new SteamCommunityConfig
		{
			SteamApiKey = SteamSetupApiKey.Trim(),
			SteamId64 = ExtractSteamId64(SteamSetupSteamId64)
		};
	}

	private void OpenYouTube()
	{
		OpenExternalUrl("https://www.youtube.com", "Opening YouTube");
	}

	private void OpenSelectedGameStore()
	{
		if (SelectedGame == null || !string.Equals(SelectedGame.Game.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(SelectedGame.Game.SteamAppId))
		{
			StatusMessage = "Steam store is only available for Steam games";
		}
		else
		{
			OpenExternalUrl("steam://store/" + SelectedGame.Game.SteamAppId, "Opening " + SelectedGame.Title + " in Steam");
		}
	}

	private void OpenGameDetailsExtra(object? parameter)
	{
		if (parameter is GameDetailsExtraViewModel gameDetailsExtraViewModel && !string.IsNullOrWhiteSpace(gameDetailsExtraViewModel.SteamAppId))
		{
			OpenExternalUrl("steam://store/" + gameDetailsExtraViewModel.SteamAppId, "Opening " + gameDetailsExtraViewModel.Title + " in Steam");
			_audioService.Play("select");
		}
	}

	private void OpenExternalUrl(string url, string statusMessage)
	{
		try
		{
			Window window = System.Windows.Application.Current?.MainWindow;
			if (window != null)
			{
				window.WindowState = WindowState.Minimized;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
			StatusMessage = statusMessage;
			_audioService.Play("select");
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
	}

	private static string GetClipboardText()
	{
		try
		{
			return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText().Trim() : string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static string ExtractSteamId64(string value)
	{
		string text = value.Trim();
		if (text.All(char.IsDigit))
		{
			return text;
		}
		int num = text.IndexOf("/profiles/", StringComparison.OrdinalIgnoreCase);
		if (num >= 0)
		{
			int count = num + "/profiles/".Length;
			string text2 = new string(text.Skip(count).TakeWhile(char.IsDigit).ToArray());
			if (!string.IsNullOrWhiteSpace(text2))
			{
				return text2;
			}
		}
		return text;
	}

	private static string GetCustomCoverFolder(string folderName)
	{
		return EnsureDirectory(Path.Combine(AppPaths.AppFolder, "Assets", "Custom Files", "CoverArt", folderName));
	}

	private static string GetMusicFolder()
	{
		return EnsureDirectory(AppPaths.FindFolder(Path.Combine("Assets", "Custom Files", "Music Files"), (string folder) => Directory.EnumerateFiles(folder).Any(IsSupportedMusicFile)));
	}

	private static bool IsSupportedMusicFile(string path)
	{
		string extension = Path.GetExtension(path);
		if (extension != null)
		{
			if (!extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".wma", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase))
			{
				return extension.Equals(".aac", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private void AudioAnalysis_OnFrameReady(object? sender, AudioAnalysisFrame frame)
	{
		System.Windows.Application current = System.Windows.Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			ApplyAudioAnalysis(frame);
			return;
		}
		val.BeginInvoke((Delegate)(Action)delegate
		{
			ApplyAudioAnalysis(frame);
		}, (DispatcherPriority)7, Array.Empty<object>());
	}

	private void ApplyAudioAnalysis(AudioAnalysisFrame frame)
	{
		VisualizerBass = frame.Bass;
		VisualizerMid = frame.Mid;
		VisualizerTreble = frame.Treble;
		VisualizerLoudness = frame.Loudness;
		VisualizerPeak = frame.Peak;
	}

	private void EnsureAudioAnalysisState()
	{
		if (IsMusicPlaying)
		{
			_audioAnalysisService.Start();
		}
		else
		{
			_audioAnalysisService.Stop();
		}
		OnPropertyChanged("IsAudioAnalysisRunning");
	}

	private static string EnsureDirectory(string path)
	{
		Directory.CreateDirectory(path);
		return path;
	}

	private static string CopyCustomArtwork(string sourcePath, string folderName, string title)
	{
		string customCoverFolder = GetCustomCoverFolder(folderName);
		string fullPath = Path.GetFullPath(sourcePath);
		if (string.Equals(Path.GetDirectoryName(fullPath), customCoverFolder, StringComparison.OrdinalIgnoreCase))
		{
			return fullPath;
		}
		string extension = Path.GetExtension(fullPath);
		string text = MakeSafeFileName(title);
		string text2 = Path.Combine(customCoverFolder, text + extension);
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(customCoverFolder, $"{text} {num++}{extension}");
		}
		File.Copy(fullPath, text2);
		return text2;
	}

	private static string MakeSafeFileName(string value)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		string text = new string(value.Select((char ch) => (!invalid.Contains(ch)) ? ch : '_').ToArray()).Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "cover";
	}

	private void LoadMusicLibrary()
	{
		string musicFolder = GetMusicFolder();
		HashSet<string> extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".wma", ".m4a", ".aac" };
		string selectedPath = CurrentMusicTrack?.Path;
		List<string> list = (from path in Directory.EnumerateFiles(musicFolder)
			where extensions.Contains(Path.GetExtension(path))
			select path).OrderBy<string, string>(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase).ToList();
		MusicTracks.Clear();
		foreach (string item in list)
		{
			MusicTracks.Add(new MusicTrackViewModel(item));
		}
		_musicIndex = ((selectedPath == null) ? (-1) : MusicTracks.ToList().FindIndex((MusicTrackViewModel track) => string.Equals(track.Path, selectedPath, StringComparison.OrdinalIgnoreCase)));
		CurrentMusicTrack = ((_musicIndex >= 0) ? MusicTracks[_musicIndex] : null);
		OnPropertyChanged("MusicTrackCountText");
	}

	public void EnsureMusicLibraryLoaded()
	{
		LoadMusicLibrary();
	}

	private void PlayMusicTrack(MusicTrackViewModel? track)
	{
		if (track == null)
		{
			if (CurrentMusicTrack != null)
			{
				_musicPlayer.Play();
				_musicTimer.Start();
				IsMusicPlaying = true;
			}
			return;
		}
		int num = MusicTracks.IndexOf(track);
		if (num >= 0 && File.Exists(track.Path))
		{
			_musicIndex = num;
			CurrentMusicTrack = track;
			_musicPlayer.Open(new Uri(track.Path, UriKind.Absolute));
			_musicPlayer.Volume = MusicVolume;
			_musicPlayer.Play();
			_musicTimer.Start();
			IsMusicPlaying = true;
			StatusMessage = "Playing " + track.Title;
			RefreshMusicProgress();
		}
	}

	private void ToggleMusicPlayback()
	{
		if (CurrentMusicTrack == null)
		{
			if (MusicTracks.Count == 0)
			{
				LoadMusicLibrary();
			}
			PlayMusicTrack(MusicTracks.FirstOrDefault());
		}
		else if (IsMusicPlaying)
		{
			_musicPlayer.Pause();
			_musicTimer.Stop();
			IsMusicPlaying = false;
		}
		else
		{
			_musicPlayer.Play();
			_musicTimer.Start();
			IsMusicPlaying = true;
		}
	}

	private void StopMusic()
	{
		_musicPlayer.Stop();
		_musicTimer.Stop();
		IsMusicPlaying = false;
		MusicProgress = 0.0;
		MusicPositionText = "0:00";
	}

	private void NextMusicTrack()
	{
		if (MusicTracks.Count == 0)
		{
			LoadMusicLibrary();
		}
		if (MusicTracks.Count != 0)
		{
			int index = (IsShuffleEnabled ? _random.Next(MusicTracks.Count) : ((_musicIndex + 1 + MusicTracks.Count) % MusicTracks.Count));
			PlayMusicTrack(MusicTracks[index]);
		}
	}

	private void PreviousMusicTrack()
	{
		if (MusicTracks.Count == 0)
		{
			LoadMusicLibrary();
		}
		if (MusicTracks.Count != 0)
		{
			int index = (_musicIndex - 1 + MusicTracks.Count) % MusicTracks.Count;
			PlayMusicTrack(MusicTracks[index]);
		}
	}

	private void RefreshMusicProgress()
	{
		TimeSpan position = _musicPlayer.Position;
		MusicPositionText = FormatTime(position);
		if (_musicPlayer.NaturalDuration.HasTimeSpan)
		{
			TimeSpan timeSpan = _musicPlayer.NaturalDuration.TimeSpan;
			MusicDurationText = FormatTime(timeSpan);
			MusicProgress = ((timeSpan.TotalSeconds <= 0.0) ? 0.0 : Math.Clamp(position.TotalSeconds / timeSpan.TotalSeconds * 100.0, 0.0, 100.0));
		}
		else
		{
			MusicDurationText = "0:00";
			MusicProgress = 0.0;
		}
	}

	private static string FormatTime(TimeSpan value)
	{
		if (!(value.TotalHours >= 1.0))
		{
			return value.ToString("m\\:ss");
		}
		return value.ToString("h\\:mm\\:ss");
	}

	private async Task ScanFolderAsync()
	{
		string text = _filePickerService.PickFolder();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (!_library.LibraryPaths.Contains<string>(text, StringComparer.OrdinalIgnoreCase))
		{
			_library.LibraryPaths.Add(text);
		}
		IReadOnlyList<GameMetadata> source = await _libraryService.ScanFolderAsync(text);
		HashSet<string> knownPaths = _library.Games.Select((GameMetadata game) => game.ExecutablePath).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		int added = 0;
		string destination = NormalizeAddDestination(Settings.DefaultAddDestination);
		foreach (GameMetadata item in source.Where((GameMetadata game) => knownPaths.Add(game.ExecutablePath)))
		{
			if (destination == "My Apps")
			{
				item.Genre = "App";
			}
			_library.Games.Add(item);
			Games.Add(new GameCardViewModel(item, _accentBrushes[Games.Count % _accentBrushes.Count]));
			added++;
		}
		SortGamesByTitle(SelectedGame?.Game.Id);
		await PersistLibraryAsync();
		OnPropertyChanged("MyGamesCountText");
		StatusMessage = ((added == 1) ? ("Imported 1 item to " + destination) : $"Imported {added} items to {destination}");
	}

	private async Task ToggleFavoriteAsync(object? _)
	{
		if (SelectedGame != null)
		{
			GameCardViewModel toggledGame = SelectedGame;
			toggledGame.Game.IsFavorite = !toggledGame.Game.IsFavorite;
			await PersistLibraryAsync();
			RefreshDerivedLists();
			if (toggledGame.Game.IsFavorite)
			{
				_audioService.Play("select");
			}
			if (_isLibraryShowingPins && !toggledGame.Game.IsFavorite)
			{
				SelectedGame = LibraryMenuGames.FirstOrDefault();
			}
			StatusMessage = (toggledGame.Game.IsFavorite ? "Pinned to Home" : "Removed from pins");
		}
	}

	private async Task RefreshSelectedGameDetailsAsync()
	{
		GameCardViewModel selected = SelectedGame;
		if (selected == null || !string.Equals(selected.Game.LaunchType, "Steam", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(selected.Game.SteamAppId))
		{
			_selectedGameDlc = Array.Empty<SteamGameDlc>();
			selected?.Refresh();
			RefreshGameDetailsPanels();
			return;
		}
		try
		{
			SteamGameDetails steamGameDetails = await _steamCommunityService.LoadGameDetailsAsync(selected.Game.SteamAppId);
			if (selected != SelectedGame)
			{
				return;
			}
			bool flag = false;
			_selectedGameDlc = steamGameDetails.Dlc;
			TimeSpan? playtime = steamGameDetails.Playtime;
			if (playtime.HasValue)
			{
				TimeSpan valueOrDefault = playtime.GetValueOrDefault();
				if (valueOrDefault != selected.Game.Playtime)
				{
					selected.Game.Playtime = valueOrDefault;
					flag = true;
				}
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.Genre) && !string.Equals(selected.Game.Genre, steamGameDetails.Genre, StringComparison.Ordinal))
			{
				selected.Game.Genre = steamGameDetails.Genre;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.Rating) && !string.Equals(selected.Game.Rating, steamGameDetails.Rating, StringComparison.Ordinal))
			{
				selected.Game.Rating = steamGameDetails.Rating;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.MultiplayerInfo) && !string.Equals(selected.Game.MultiplayerInfo, steamGameDetails.MultiplayerInfo, StringComparison.Ordinal))
			{
				selected.Game.MultiplayerInfo = steamGameDetails.MultiplayerInfo;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.CoOpInfo) && !string.Equals(selected.Game.CoOpInfo, steamGameDetails.CoOpInfo, StringComparison.Ordinal))
			{
				selected.Game.CoOpInfo = steamGameDetails.CoOpInfo;
				flag = true;
			}
			if (!string.IsNullOrWhiteSpace(steamGameDetails.StoreScreenshotPath) && !string.Equals(selected.Game.StoreScreenshotPath, steamGameDetails.StoreScreenshotPath, StringComparison.Ordinal))
			{
				selected.Game.StoreScreenshotPath = steamGameDetails.StoreScreenshotPath;
				flag = true;
			}
			if (Math.Abs(selected.Game.ReviewStarRating - steamGameDetails.ReviewStarRating) > 0.01)
			{
				selected.Game.ReviewStarRating = steamGameDetails.ReviewStarRating;
				flag = true;
			}
			if (selected.Game.ReviewCount != steamGameDetails.ReviewCount)
			{
				selected.Game.ReviewCount = steamGameDetails.ReviewCount;
				flag = true;
			}
			selected.Refresh();
			if (flag)
			{
				await PersistLibraryAsync();
			}
			RefreshGameDetailsPanels();
		}
		catch
		{
			_selectedGameDlc = Array.Empty<SteamGameDlc>();
			selected.Refresh();
			RefreshGameDetailsPanels();
		}
	}

	private void RefreshGameDetailsPanels()
	{
		GameDetailsExtras.Clear();
		foreach (GameDetailsExtraViewModel item in BuildGameDetailsExtras())
		{
			GameDetailsExtras.Add(item);
		}
		GameDetailsPreviewExtras.Clear();
		foreach (GameDetailsExtraViewModel item2 in GameDetailsExtras.Take(5))
		{
			GameDetailsPreviewExtras.Add(item2);
		}
		if (GameDetailsExtras.Count > 5)
		{
			GameDetailsPreviewExtras.Add(new GameDetailsExtraViewModel
			{
				Title = "See All",
				IconPath = GameDetailsSeeAllIconPath,
				IsSeeAll = true,
				Command = OpenGameDetailsExtrasSeeAllCommand
			});
		}
		OnPropertyChanged("GameDetailsDeveloperText");
		OnPropertyChanged("GameDetailsPublisherText");
		OnPropertyChanged("GameDetailsGenreText");
		OnPropertyChanged("GameDetailsLocalCapabilitiesText");
		OnPropertyChanged("GameDetailsOnlineCapabilitiesText");
		OnPropertyChanged("GameDetailsNoteText");
		OnPropertyChanged("GameDetailsSeeAllCountText");
		OnPropertyChanged("GameDetailsGalleryImagePath");
		OnPropertyChanged("GameDetailsGalleryCountText");
	}

	private IEnumerable<GameDetailsExtraViewModel> BuildGameDetailsExtras()
	{
		string rating = SelectedGame?.DetailsReviewStarsText;
		if (string.IsNullOrWhiteSpace(rating))
		{
			rating = "*****";
		}
		foreach (SteamGameDlc item in _selectedGameDlc.Where((SteamGameDlc dlc) => !string.IsNullOrWhiteSpace(dlc.AppId) && !string.IsNullOrWhiteSpace(dlc.Name)))
		{
			yield return new GameDetailsExtraViewModel
			{
				Title = item.Name,
				PriceText = item.PriceText,
				RatingText = rating,
				IconPath = GameDetailsAddOnIconPath,
				SteamAppId = item.AppId,
				Command = OpenGameDetailsExtraCommand
			};
		}
	}

	private IEnumerable<string> BuildGameDetailsGalleryImages()
	{
		GameMetadata gameMetadata = SelectedGame?.Game;
		if (gameMetadata == null)
		{
			yield break;
		}
		foreach (string item in new string[4] { gameMetadata.StoreScreenshotPath, gameMetadata.BackgroundArtPath, gameMetadata.HeaderImagePath, gameMetadata.CoverArtPath }.Where((string path) => !string.IsNullOrWhiteSpace(path)).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			yield return item;
		}
	}

	private IEnumerable<string> BuildLocalCapabilities()
	{
		yield return "Players 1";
		yield return "Hard Drive Required";
		yield return "Controller Supported";
		if (!string.IsNullOrWhiteSpace(SelectedGame?.DetailsPlaytimeText))
		{
			yield return SelectedGame.DetailsPlaytimeText.Replace("Time played: ", "Time played ");
		}
	}

	private IEnumerable<string> BuildOnlineCapabilities()
	{
		string text = SelectedGame?.DetailsMultiplayerText;
		string coOp = SelectedGame?.DetailsCoOpText;
		yield return string.IsNullOrWhiteSpace(text) ? "Online Multiplayer: None" : text;
		yield return string.IsNullOrWhiteSpace(coOp) ? "Online Co-op: None" : coOp;
	}

	private string BuildGameDetailsNote()
	{
		if (SelectedGame == null)
		{
			return string.Empty;
		}
		if (!SelectedGame.IsSteamGame)
		{
			return SelectedGame.Title + " was added manually. Steam store metadata is not available for this game.";
		}
		return SelectedGame.Title + " was imported from Steam. Store metadata is shown when available; some capabilities may depend on Steam store categories and installed game data.";
	}

	private async Task SaveSettingsAsync()
	{
		await _settingsService.SaveAsync(Settings);
		await PersistLibraryAsync();
		_startupRegistrationService.SetLaunchOnStartup(Settings.LaunchOnWindowsStartup);
		StatusMessage = "Settings saved";
	}

	private async Task ExportDataAsync()
	{
		string path = _filePickerService.PickSaveJsonFile();
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		try
		{
			await _importExportService.ExportAsync(_library, Profile, Settings, path);
			StatusMessage = "Backup exported";
			System.Windows.MessageBox.Show("Dashboard data exported successfully." + Environment.NewLine + Environment.NewLine + path, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			App.LogException(ex, "DashboardViewModel.ExportDataAsync");
			System.Windows.MessageBox.Show("Export failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async Task ImportDataAsync()
	{
		string text = _filePickerService.PickJsonFile();
		if (!string.IsNullOrWhiteSpace(text))
		{
			DashboardImportResult result = await _importExportService.ImportAsync(text);
			if (!result.Success)
			{
				System.Windows.MessageBox.Show(result.Message, "Import Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			await ReloadSavedDataAsync();
			_startupRegistrationService.SetLaunchOnStartup(Settings.LaunchOnWindowsStartup);
			StatusMessage = "Backup imported";
			System.Windows.MessageBox.Show((result.SafetyBackupPath == null) ? result.Message : $"{result.Message}{Environment.NewLine}{Environment.NewLine}Safety backup created:{Environment.NewLine}{result.SafetyBackupPath}", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private async Task ScanSteamGamesAsync()
	{
		_ = 1;
		try
		{
			SteamGameScanResult result = await _steamLibraryScannerService.ScanAsync(_library);
			if (!string.IsNullOrWhiteSpace(result.Message))
			{
				StatusMessage = result.Message;
			}
			if (result.Added > 0 || result.Updated > 0)
			{
				SyncGamesCollectionFromLibrary();
				RefreshDerivedLists();
				SortGamesByTitle(SelectedGame?.Game.Id);
				await PersistLibraryAsync();
			}
			WriteSteamScanDebugReport(result);
			System.Windows.MessageBox.Show(string.IsNullOrWhiteSpace(result.Message) ? $"Steam scan complete.{Environment.NewLine}{Environment.NewLine}Added: {result.Added}{Environment.NewLine}Updated: {result.Updated}{Environment.NewLine}Skipped: {result.Skipped}" : $"{result.Message}{Environment.NewLine}{Environment.NewLine}Added: {result.Added}{Environment.NewLine}Updated: {result.Updated}{Environment.NewLine}Skipped: {result.Skipped}", "Scan Steam Games", MessageBoxButton.OK, (result.Added > 0 || result.Updated > 0) ? MessageBoxImage.Asterisk : MessageBoxImage.Exclamation);
		}
		catch (Exception ex)
		{
			App.LogException(ex, "DashboardViewModel.ScanSteamGamesAsync");
			StatusMessage = "Steam scan failed";
			System.Windows.MessageBox.Show("Steam scan failed." + Environment.NewLine + Environment.NewLine + ex.Message, "Scan Steam Games", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async Task LoadThemesAsync()
	{
		IReadOnlyList<DashboardTheme> obj = await _themeService.LoadThemesAsync();
		AvailableThemes.Clear();
		foreach (DashboardTheme item in obj)
		{
			AvailableThemes.Add(item);
		}
	}

	private static List<DashboardTabCustomizationViewModel> BuildDashboardCustomizationTabs()
	{
		int num = 5;
		List<DashboardTabCustomizationViewModel> list = new List<DashboardTabCustomizationViewModel>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<DashboardTabCustomizationViewModel> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = new DashboardTabCustomizationViewModel("home", "Home", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[9]
		{
			Tile("home", "open-tray", "Open Tray", 0.0, 0.0, 177.0, 122.0, color: true, imageEditable: false),
			Tile("home", "pins", "My Pins", 0.0, 123.0, 177.0, 122.0, color: true, imageEditable: false),
			Tile("home", "recent", "Recent", 0.0, 246.0, 177.0, 122.0, color: true, imageEditable: false),
			Tile("home", "halo", "Halo 4", 179.0, 0.0, 423.0, 242.0, color: false, imageEditable: true, "Assets/Tiles/halo4home.jpg"),
			Tile("home", "dexter", "Dexter", 179.0, 244.0, 210.0, 124.0, color: false, imageEditable: true, "Assets/Tiles/dexterhome.jpg"),
			Tile("home", "maroon5", "Maroon 5", 391.0, 244.0, 211.0, 124.0, color: false, imageEditable: true, "Assets/Tiles/maroon5home.jpg"),
			Tile("home", "kinect", "Kinect Central", 604.0, 0.0, 160.0, 122.0, color: false, imageEditable: true, "Assets/Tiles/dancecentralhome.jpg"),
			Tile("home", "sports", "Sports", 604.0, 123.0, 160.0, 122.0, color: false, imageEditable: true, "Assets/Tiles/espn.jpg"),
			Tile("home", "youtube", "YouTube", 604.0, 246.0, 160.0, 122.0, color: false, imageEditable: true, "Assets/Tiles/youtubehome.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("video", "Video", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[7]
		{
			Tile("video", "apps", "My Video Apps", 0.0, 0.0, 146.0, 148.0, color: true, imageEditable: false),
			Tile("video", "marketplace", "Video Marketplace", 0.0, 150.0, 146.0, 148.0, color: true, imageEditable: false),
			Tile("video", "feature", "The Dark Knight", 148.0, 0.0, 308.0, 301.0, color: false, imageEditable: true, "Assets/Tiles/thedarkkightvideo.jpg"),
			Tile("video", "cloudy", "Cloudy", 458.0, 0.0, 148.0, 150.0, color: false, imageEditable: true, "Assets/Tiles/cloudywithachanceofmeatballsvideo.jpg"),
			Tile("video", "recent", "Recent", 608.0, 0.0, 198.0, 150.0, color: false, imageEditable: false, "", "#FFFFFFFF", "#FF2B2B2B"),
			Tile("video", "kungfu", "Kung Fu Panda 2", 458.0, 152.0, 148.0, 149.0, color: false, imageEditable: true, "Assets/Tiles/kungfupanda2video.jpg"),
			Tile("video", "hbogo", "HBO GO", 608.0, 152.0, 198.0, 149.0, color: false, imageEditable: true, "Assets/Tiles/hbogovideo.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("games", "Games", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[5]
		{
			Tile("games", "mygames", "My Games", 0.0, 0.0, 155.0, 153.0, color: true, imageEditable: false),
			Tile("games", "marketplace", "Game Marketplace", 0.0, 155.0, 155.0, 149.0, color: true, imageEditable: false),
			Tile("games", "forza", "Forza Horizon", 157.0, 0.0, 400.0, 303.0, color: false, imageEditable: true, "Assets/Tiles/forzahorizongames.jpg"),
			Tile("games", "minecraft", "Minecraft", 559.0, 0.0, 200.0, 151.0, color: false, imageEditable: true, "Assets/Tiles/minecraftgames.jpg"),
			Tile("games", "blackops", "Black Ops II", 559.0, 153.0, 200.0, 150.0, color: false, imageEditable: true, "Assets/Tiles/blackops2games.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("music", "Music", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[6]
		{
			Tile("music", "player", "Music Player", 0.0, 0.0, 148.0, 148.0, color: true, imageEditable: false),
			Tile("music", "marketplace", "Music Marketplace", 0.0, 150.0, 148.0, 148.0, color: true, imageEditable: false),
			Tile("music", "feature", "Overexposed", 150.0, 0.0, 306.0, 298.0, color: false, imageEditable: true, "Assets/Tiles/overexposedmusic.jpg"),
			Tile("music", "recent", "Recent Music", 458.0, 0.0, 158.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/evanescencerecent.png"),
			Tile("music", "search", "Search", 618.0, 0.0, 188.0, 148.0, color: false, imageEditable: false, "", "#FF191919"),
			Tile("music", "panchiko", "Panchiko", 458.0, 150.0, 348.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/panchikomusic.jpg")
		}));
		num2++;
		span[num2] = new DashboardTabCustomizationViewModel("apps", "Apps", new _003C_003Ez__ReadOnlyArray<DashboardTileCustomizationViewModel>(new DashboardTileCustomizationViewModel[7]
		{
			Tile("apps", "myapps", "My Apps", 0.0, 0.0, 148.0, 148.0, color: true, imageEditable: false),
			Tile("apps", "browse", "Browse Apps", 0.0, 150.0, 148.0, 148.0, color: true, imageEditable: false),
			Tile("apps", "hbogo", "HBO GO", 150.0, 0.0, 306.0, 298.0, color: false, imageEditable: true, "Assets/Tiles/hbogoapps.jpg"),
			Tile("apps", "netflix", "Netflix", 458.0, 0.0, 158.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/netflixapps.png"),
			Tile("apps", "youtube", "YouTube", 618.0, 0.0, 188.0, 148.0, color: false, imageEditable: true, "Assets/Tiles/youtubeapps.jpg"),
			Tile("apps", "hulu", "Hulu Plus", 458.0, 150.0, 158.0, 148.0, color: false, imageEditable: true),
			Tile("apps", "espn", "ESPN", 618.0, 150.0, 188.0, 148.0, color: false, imageEditable: true)
		}));
		num2++;
		return list;
		static DashboardTileCustomizationViewModel Tile(string tab, string id, string title, double left, double top, double width, double height, bool color, bool imageEditable, string image = "", string placeholderColor = "#FF202628", string titleColor = "#FFFFFFFF")
		{
			return new DashboardTileCustomizationViewModel(tab + "." + id, title, tab, left, top, width, height, color, imageEditable, image, placeholderColor, titleColor);
		}
	}

	public DashboardTileCustomization? GetDashboardTileCustomization(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || !Settings.DashboardTileCustomizations.TryGetValue(key, out DashboardTileCustomization value))
		{
			return null;
		}
		return value;
	}

	public string GetDashboardTileTitle(string key, string fallback)
	{
		if (string.IsNullOrWhiteSpace(key) || !Settings.DashboardTileCustomizations.TryGetValue(key, out DashboardTileCustomization value) || string.IsNullOrWhiteSpace(value.TitleOverride))
		{
			return fallback;
		}
		return value.TitleOverride;
	}

	private void MoveDashboardCustomizerTab(int step)
	{
		if (DashboardCustomizationTabs.Count != 0)
		{
			_customizerTabIndex = (_customizerTabIndex + step + DashboardCustomizationTabs.Count) % DashboardCustomizationTabs.Count;
			SelectDefaultDashboardCustomizerTile();
			OnPropertyChanged("CurrentDashboardCustomizationTab");
			OnPropertyChanged("CurrentDashboardCustomizationTabName");
			_audioService.Play("tab");
		}
	}

	private void SelectDefaultDashboardCustomizerTile()
	{
		SelectedDashboardTile = CurrentDashboardCustomizationTab?.Tiles.FirstOrDefault((DashboardTileCustomizationViewModel tile) => tile.AllowsImageCustomization) ?? CurrentDashboardCustomizationTab?.Tiles.FirstOrDefault();
	}

	private void SelectDashboardTile(DashboardTileCustomizationViewModel? tile)
	{
		if (tile != null)
		{
			SelectedDashboardTile = tile;
			_audioService.Play("select");
		}
	}

	private async Task ChooseDashboardTileImageAsync(object? _)
	{
		if (SelectedDashboardTile == null || !SelectedDashboardTile.AllowsImageCustomization)
		{
			StatusMessage = "This tile uses the global tile color";
			return;
		}
		string text = _filePickerService.PickImage();
		if (!string.IsNullOrWhiteSpace(text))
		{
			SelectedDashboardTile.ImagePath = text;
			PersistSelectedDashboardTile();
			await SaveDashboardCustomizationAsync();
			StatusMessage = "Customized " + SelectedDashboardTile.Title;
		}
	}

	private async Task ResetDashboardTileImageAsync(object? _)
	{
		if (SelectedDashboardTile == null || !SelectedDashboardTile.AllowsImageCustomization)
		{
			StatusMessage = "This tile uses the global tile color";
			return;
		}
		SelectedDashboardTile.ResetImage();
		PersistSelectedDashboardTile();
		RefreshDashboardTileBindings();
		await SaveDashboardCustomizationAsync();
		StatusMessage = "Reset " + SelectedDashboardTile.Title;
	}

	private async Task ResetDashboardTileTitleAsync(object? _)
	{
		if (SelectedDashboardTile != null)
		{
			SelectedDashboardTile.ResetTitle();
			PersistSelectedDashboardTile();
			RefreshDashboardTileBindings();
			OnPropertyChanged("SelectedDashboardTileTitle");
			await SaveDashboardCustomizationAsync();
			StatusMessage = "Reset " + SelectedDashboardTile.DefaultTitle + " text";
		}
	}

	private async Task ResetDashboardTabImagesAsync(object? _)
	{
		DashboardTabCustomizationViewModel tab = CurrentDashboardCustomizationTab;
		if (tab == null)
		{
			return;
		}
		foreach (DashboardTileCustomizationViewModel tile in tab.Tiles)
		{
			Settings.DashboardTileCustomizations.Remove(tile.Key);
			tile.ResetImage();
			tile.ResetTitle();
		}
		RefreshDashboardTileBindings();
		await SaveDashboardCustomizationAsync();
		StatusMessage = tab.Name + " custom images reset";
	}

	private void OpenDashboardTileColorPicker()
	{
		System.Windows.Media.Color color = ParseColor(Settings.DashboardTileColor);
		using ColorDialog colorDialog = new ColorDialog
		{
			FullOpen = true,
			Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B)
		};
		if (colorDialog.ShowDialog() == DialogResult.OK)
		{
			_customTileRed = colorDialog.Color.R;
			_customTileGreen = colorDialog.Color.G;
			_customTileBlue = colorDialog.Color.B;
			OnPropertyChanged("CustomTileRed");
			OnPropertyChanged("CustomTileGreen");
			OnPropertyChanged("CustomTileBlue");
			UpdateDashboardTileColorFromSliders();
			StatusMessage = "Dashboard tile color updated";
		}
	}

	private async Task ResetDashboardTileColorAsync(object? _)
	{
		Settings.DashboardTileColor = "#FF60B616";
		ApplyDashboardAccentResources(Settings.DashboardTileColor);
		ApplyDashboardTileColorToSliders(Settings.DashboardTileColor);
				OnPropertyChanged("DashboardTileBrush");
				OnPropertyChanged("DashboardTileDarkBrush");
				OnPropertyChanged("DashboardTileColorPreviewBrush");
		await SaveDashboardCustomizationAsync();
		StatusMessage = "Dashboard tile color reset";
	}

	private void SelectedDashboardTile_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag;
		switch (e.PropertyName)
		{
		case "Zoom":
		case "OffsetX":
		case "OffsetY":
		case "ImagePath":
		case "TitleOverride":
		case "Title":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			PersistSelectedDashboardTile();
			RefreshDashboardTileBindings();
			OnPropertyChanged("SelectedDashboardTileTitle");
			SaveDashboardCustomizationAsync();
		}
	}

	private void PersistSelectedDashboardTile()
	{
		if (SelectedDashboardTile != null)
		{
			if (!SelectedDashboardTile.HasCustomization)
			{
				Settings.DashboardTileCustomizations.Remove(SelectedDashboardTile.Key);
				return;
			}
			Settings.DashboardTileCustomizations[SelectedDashboardTile.Key] = new DashboardTileCustomization
			{
				ImagePath = SelectedDashboardTile.ImagePath,
				TitleOverride = SelectedDashboardTile.TitleOverride,
				Zoom = SelectedDashboardTile.Zoom,
				OffsetX = SelectedDashboardTile.OffsetX,
				OffsetY = SelectedDashboardTile.OffsetY
			};
		}
	}

	private void SyncDashboardCustomizerFromSettings()
	{
		foreach (DashboardTileCustomizationViewModel item in DashboardCustomizationTabs.SelectMany((DashboardTabCustomizationViewModel tab) => tab.Tiles))
		{
			DashboardTileCustomization value2;
			if (!item.AllowsImageCustomization)
			{
				item.ResetImage();
				if (Settings.DashboardTileCustomizations.TryGetValue(item.Key, out DashboardTileCustomization value))
				{
					item.TitleOverride = value.TitleOverride;
					if (!item.HasCustomTitle)
					{
						Settings.DashboardTileCustomizations.Remove(item.Key);
					}
				}
			}
			else if (Settings.DashboardTileCustomizations.TryGetValue(item.Key, out value2))
			{
				item.ImagePath = value2.ImagePath;
				item.TitleOverride = value2.TitleOverride;
				item.Zoom = ((value2.Zoom <= 0.0) ? 1.0 : value2.Zoom);
				item.OffsetX = value2.OffsetX;
				item.OffsetY = value2.OffsetY;
			}
			else
			{
				item.ResetImage();
				item.ResetTitle();
			}
		}
	}

	private async Task SaveDashboardCustomizationAsync()
	{
		await _settingsService.SaveAsync(Settings);
		OnPropertyChanged("DashboardTileBrush");
		OnPropertyChanged("DashboardTileDarkBrush");
		OnPropertyChanged("DashboardTileColorPreviewBrush");
	}

	private void RefreshDashboardTileBindings()
	{
		OnPropertyChanged("Settings");
		OnPropertyChanged("DashboardTileBrush");
		OnPropertyChanged("DashboardTileDarkBrush");
		OnPropertyChanged("DashboardTileColorPreviewBrush");
	}

	private void UpdateDashboardTileColorFromSliders()
	{
		Settings.DashboardTileColor = $"#FF{_customTileRed:X2}{_customTileGreen:X2}{_customTileBlue:X2}";
		ApplyDashboardAccentResources(Settings.DashboardTileColor);
		OnPropertyChanged("DashboardTileBrush");
		OnPropertyChanged("DashboardTileDarkBrush");
		OnPropertyChanged("DashboardTileColorPreviewBrush");
		SaveDashboardCustomizationAsync();
	}

	private void ApplyDashboardTileColorToSliders(string color)
	{
		System.Windows.Media.Color color2 = ParseColor(color);
		_customTileRed = color2.R;
		_customTileGreen = color2.G;
		_customTileBlue = color2.B;
		OnPropertyChanged("CustomTileRed");
		OnPropertyChanged("CustomTileGreen");
		OnPropertyChanged("CustomTileBlue");
	}

	private static void ApplyDashboardAccentResources(string color)
	{
		System.Windows.Media.Color color2 = ParseColor(color);
		System.Windows.Media.Color color3 = ToDarkAccentColor(color2);
		System.Windows.Media.Color color4 = System.Windows.Media.Color.FromArgb(204, (byte)Math.Max(0.0, (double)(int)color2.R * 0.5), (byte)Math.Max(0.0, (double)(int)color2.G * 0.5), (byte)Math.Max(0.0, (double)(int)color2.B * 0.5));
		ApplyAccentResourceSet(System.Windows.Application.Current.Resources, color2, color3, color4);
		foreach (Window window in System.Windows.Application.Current.Windows)
		{
			ApplyAccentResourceSet(window.Resources, color2, color3, color4);
		}
	}

	private static void ApplyAccentResourceSet(ResourceDictionary resources, System.Windows.Media.Color accent, System.Windows.Media.Color darkAccent, System.Windows.Media.Color pressedAccent)
	{
		resources["MetroGreenBrush"] = new SolidColorBrush(accent);
		resources["MetroGreenDarkBrush"] = new SolidColorBrush(darkAccent);
		resources["MetroTilePressedBrush"] = new SolidColorBrush(pressedAccent);
	}

	private static string NormalizeDashboardTileColor(string? color)
	{
		try
		{
			return ParseColor(color).ToString(CultureInfo.InvariantCulture);
		}
		catch
		{
			return "#FF60B616";
		}
	}

	private static string ToDarkAccentColor(string color)
	{
		return ToDarkAccentColor(ParseColor(color)).ToString(CultureInfo.InvariantCulture);
	}

	private static System.Windows.Media.Color ToDarkAccentColor(System.Windows.Media.Color color)
	{
		return System.Windows.Media.Color.FromArgb(color.A, (byte)Math.Max(0.0, (double)(int)color.R * 0.58), (byte)Math.Max(0.0, (double)(int)color.G * 0.58), (byte)Math.Max(0.0, (double)(int)color.B * 0.58));
	}

	private static SolidColorBrush CreateBrush(string color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(ParseColor(color));
		if (((Freezable)solidColorBrush).CanFreeze)
		{
			((Freezable)solidColorBrush).Freeze();
		}
		return solidColorBrush;
	}

	private static System.Windows.Media.Color ParseColor(string? color)
	{
		if (string.IsNullOrWhiteSpace(color))
		{
			return System.Windows.Media.Color.FromRgb(96, 182, 22);
		}
		return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
	}

	private static byte ToByte(double value)
	{
		return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
	}

	private async Task SelectThemeAsync(object? parameter)
	{
		DashboardTheme theme = (parameter as DashboardTheme) ?? SelectedTheme ?? AvailableThemes.FirstOrDefault((DashboardTheme themeItem) => themeItem.IsBuiltIn) ?? new DashboardTheme
		{
			Name = "Xbox 360",
			IsBuiltIn = true
		};
		ApplySelectedTheme(theme.Name);
		Settings.ThemeName = theme.Name;
		await _settingsService.SaveAsync(Settings);
		IsThemeMenuOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		StatusMessage = (theme.IsBuiltIn ? "Xbox 360 theme restored" : ("Theme selected: " + theme.Name));
		_audioService.Play("select");
	}

	private async Task ChooseThemeSectionImageAsync(string sectionKey)
	{
		string text = _filePickerService.PickImage();
		if (!string.IsNullOrWhiteSpace(text))
		{
			switch (sectionKey)
			{
			case "home":
				ThemeHomePreviewPath = text;
				break;
			case "games":
				ThemeGamesPreviewPath = text;
				break;
			case "settings":
				ThemeSettingsPreviewPath = text;
				break;
			case "apps":
				ThemeAppsPreviewPath = text;
				break;
			}
			await Task.CompletedTask;
			StatusMessage = "Theme preview updated";
		}
	}

	private async Task SaveThemeAsync(object? _)
	{
		if (string.IsNullOrWhiteSpace(ThemeNameInput))
		{
			StatusMessage = "Enter a theme name first";
			return;
		}
		DashboardTheme createdTheme = await _themeService.CreateThemeAsync(ThemeNameInput, ThemeHomePreviewPath, ThemeGamesPreviewPath, ThemeSettingsPreviewPath, ThemeAppsPreviewPath);
		await LoadThemesAsync();
		ApplySelectedTheme(createdTheme.Name);
		Settings.ThemeName = createdTheme.Name;
		await _settingsService.SaveAsync(Settings);
		IsThemeCreatorOpen = false;
		OnPropertyChanged("CurrentThemeBackgroundPath");
		ResetPendingThemeDraft();
		StatusMessage = "Created theme: " + createdTheme.Name;
		_audioService.Play("select");
	}

	private void ApplySelectedTheme(string? themeName)
	{
		string normalizedName = NormalizeThemeName(themeName);
		SelectedTheme = AvailableThemes.FirstOrDefault((DashboardTheme theme) => string.Equals(theme.Name, normalizedName, StringComparison.OrdinalIgnoreCase)) ?? AvailableThemes.FirstOrDefault((DashboardTheme theme) => theme.IsBuiltIn) ?? new DashboardTheme
		{
			Name = "Xbox 360",
			IsBuiltIn = true
		};
		OnPropertyChanged("ThemeMenuVisibilityTitle");
		OnPropertyChanged("CurrentThemeBackgroundPath");
	}

	private static string NormalizeThemeName(string? themeName)
	{
		if (string.IsNullOrWhiteSpace(themeName) || string.Equals(themeName, "Metro Green", StringComparison.OrdinalIgnoreCase))
		{
			return "Xbox 360";
		}
		return themeName.Trim();
	}

	private string ResolveThemeSectionKey()
	{
		if (IsLauncherSettingsOpen)
		{
			return "settings";
		}
		if (IsMyGamesOpen)
		{
			if (!_isLibraryShowingApps)
			{
				return "games";
			}
			return "apps";
		}
		return CurrentTab?.Key switch
		{
			"bing" => "home", 
			"home" => "home", 
			"social" => "home", 
			"video" => "home", 
			"games" => "home", 
			"music" => "home", 
			"apps" => "home", 
			"settings" => "home", 
			_ => "home", 
		};
	}

	private void ResetPendingThemeDraft()
	{
		ThemeNameInput = string.Empty;
		ThemeHomePreviewPath = string.Empty;
		ThemeGamesPreviewPath = string.Empty;
		ThemeSettingsPreviewPath = string.Empty;
		ThemeAppsPreviewPath = string.Empty;
	}

	private void EnsureProfileDefaults()
	{
		string gamerPicturePath = Path.Combine(AppPaths.AppFolder, "Assets", "Profile", "profilepicture.jpg");
		if (string.IsNullOrWhiteSpace(Profile.Gamertag))
		{
			Profile.Gamertag = "MetroPilot";
		}
		if (string.IsNullOrWhiteSpace(Profile.GamerPicturePath) || IsOldDefaultProfilePicture(Profile.GamerPicturePath))
		{
			Profile.GamerPicturePath = gamerPicturePath;
		}
		if (string.IsNullOrWhiteSpace(Profile.OnlineStatus))
		{
			Profile.OnlineStatus = "Online";
		}
		if (string.IsNullOrWhiteSpace(Profile.Motto))
		{
			Profile.Motto = "(No motto)";
		}
		if (string.IsNullOrWhiteSpace(Profile.Description))
		{
			Profile.Description = "(No bio)";
		}
	}

	private static bool IsOldDefaultProfilePicture(string path)
	{
		if (path.EndsWith(Path.Combine("Assets", "Art", "profilepicture.jpg"), StringComparison.OrdinalIgnoreCase))
		{
			return !File.Exists(path);
		}
		return false;
	}

	private static string NormalizeGameCoverFitMode(string? mode)
	{
		bool flag;
		switch (mode)
		{
		case "Cover":
		case "Fill":
		case "Fit":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "Auto";
		}
		return mode;
	}

	private static string NormalizeAddDestination(string? destination)
	{
		if (!string.Equals(destination, "My Apps", StringComparison.OrdinalIgnoreCase))
		{
			return "My Games";
		}
		return "My Apps";
	}

	private string NormalizeAudioOutputDeviceName(string? deviceName)
	{
		return AudioOutputDeviceOptions.FirstOrDefault((string option) => string.Equals(option, deviceName, StringComparison.OrdinalIgnoreCase)) ?? "Default";
	}

	public void RefreshAudioOutputDevices()
	{
		IReadOnlyList<string> readOnlyList;
		try
		{
			readOnlyList = _audioService.GetOutputDeviceNames();
		}
		catch (Exception exception)
		{
			App.LogException(exception, "DashboardViewModel.RefreshAudioOutputDevices");
			readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>("Default");
		}
		if (readOnlyList.Count == 0)
		{
			readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>("Default");
		}
		string selected = Settings.AudioOutputDeviceName;
		AudioOutputDeviceOptions.Clear();
		foreach (string item in readOnlyList)
		{
			AudioOutputDeviceOptions.Add(item);
		}
		if (!AudioOutputDeviceOptions.Any((string option) => string.Equals(option, selected, StringComparison.OrdinalIgnoreCase)))
		{
			Settings.AudioOutputDeviceName = AudioOutputDeviceOptions.FirstOrDefault() ?? "Default";
			OnPropertyChanged("AudioOutputDeviceName");
		}
	}

	private static SocialIntegrationMode NormalizeSocialIntegrationMode(SocialIntegrationMode mode)
	{
		return SocialIntegrationMode.LocalOnly;
	}

	private static string ToSocialIntegrationDisplay(SocialIntegrationMode mode)
	{
		return "Local";
	}

	private static SocialIntegrationMode ParseSocialIntegrationMode(string? mode)
	{
		return SocialIntegrationMode.LocalOnly;
	}

	private static bool IsAppEntry(GameMetadata game)
	{
		return string.Equals(game.Genre, "App", StringComparison.OrdinalIgnoreCase);
	}

	private void SortGamesByTitle(string? selectedGameId = null)
	{
		if (selectedGameId == null)
		{
			selectedGameId = SelectedGame?.Game.Id;
		}
		_library.Games = _library.Games.OrderBy<GameMetadata, string>((GameMetadata game) => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
		List<GameCardViewModel> list = Games.OrderBy<GameCardViewModel, string>((GameCardViewModel game) => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
		Games.Clear();
		foreach (GameCardViewModel item in list)
		{
			Games.Add(item);
		}
		SelectedGame = Games.FirstOrDefault((GameCardViewModel game) => string.Equals(game.Game.Id, selectedGameId, StringComparison.OrdinalIgnoreCase)) ?? Games.FirstOrDefault();
		FeaturedGame = SelectedGame;
	}

	private async Task PersistLibraryAsync()
	{
		await _libraryService.SaveAsync(_library);
		RefreshDerivedLists();
	}

	private void SyncGamesCollectionFromLibrary()
	{
		Games.Clear();
		int num = 0;
		foreach (GameMetadata game in _library.Games)
		{
			Games.Add(new GameCardViewModel(game, _accentBrushes[num++ % _accentBrushes.Count]));
		}
	}

	private void WriteSteamScanDebugReport(SteamGameScanResult result)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(SteamScanDebugLogPath));
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[STEAM SCAN]");
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder2);
			handler.AppendLiteral("added: ");
			handler.AppendFormatted(result.Added);
			stringBuilder3.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("updated: ");
			handler.AppendFormatted(result.Updated);
			stringBuilder4.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("skipped: ");
			handler.AppendFormatted(result.Skipped);
			stringBuilder5.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("message: ");
			handler.AppendFormatted(result.Message);
			stringBuilder6.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder2);
			handler.AppendLiteral("saved library game count: ");
			handler.AppendFormatted(_library.Games.Count);
			stringBuilder7.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("loaded Games menu count: ");
			handler.AppendFormatted(Games.Count);
			stringBuilder8.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder9 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder2);
			handler.AppendLiteral("my games visible count: ");
			handler.AppendFormatted(Games.Count((GameCardViewModel game) => !IsAppEntry(game.Game)));
			stringBuilder9.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder10 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder2);
			handler.AppendLiteral("library file path: ");
			handler.AppendFormatted(Path.Combine(AppPaths.AppFolder, "UserData", "library.json"));
			stringBuilder10.AppendLine(ref handler);
			File.WriteAllText(SteamScanDebugLogPath, stringBuilder.ToString());
		}
		catch
		{
		}
	}

	private void RefreshDerivedLists()
	{
		OnPropertyChanged("RecentGames");
		OnPropertyChanged("PinnedGames");
		OnPropertyChanged("ImportedGames");
		OnPropertyChanged("LibraryPaths");
		OnPropertyChanged("MyGamesCountText");
		OnPropertyChanged("LibraryMenuGames");
		RefreshVisibleLibraryMenuGames();
		OnPropertyChanged("LibraryMenuCountText");
	}

	private IEnumerable<GameCardViewModel> GetLibraryMenuGames()
	{
		if (!_isLibraryShowingPins)
		{
			if (!_isLibraryShowingApps)
			{
				return Games.Where((GameCardViewModel game) => !IsAppEntry(game.Game));
			}
			return Games.Where((GameCardViewModel game) => IsAppEntry(game.Game));
		}
		return Games.Where((GameCardViewModel game) => game.Game.IsFavorite);
	}

	private void RefreshVisibleLibraryMenuGames()
	{
		List<GameCardViewModel> list = GetLibraryMenuGames().ToList();
		if (list.Count == 0)
		{
			_libraryMenuStartIndex = 0;
			VisibleLibraryMenuGames.Clear();
			return;
		}
		int num = ((SelectedGame != null) ? list.IndexOf(SelectedGame) : 0);
		if (num < 0)
		{
			num = 0;
		}
		int num2 = Math.Min(6, list.Count);
		int num3 = Math.Max(0, list.Count - num2);
		_libraryMenuStartIndex = Math.Clamp(_libraryMenuStartIndex, 0, num3);
		if (num < _libraryMenuStartIndex || num >= _libraryMenuStartIndex + num2)
		{
			if (num >= _libraryMenuStartIndex + num2 && num > num3)
			{
				_libraryMenuStartIndex = Math.Min(_libraryMenuStartIndex + 1, num3);
			}
			else
			{
				int val = num / num2 * num2;
				_libraryMenuStartIndex = Math.Min(val, num3);
			}
		}
		int num4 = Math.Max(0, _libraryMenuStartIndex - 1);
		int num5 = num2 + 1;
		if (_libraryMenuStartIndex > 0)
		{
			num5++;
		}
		List<GameCardViewModel> list2 = list.Skip(num4).Take(Math.Min(num5, list.Count - num4)).ToList();
		if (VisibleLibraryMenuGames.Count == list2.Count && VisibleLibraryMenuGames.Zip(list2).All(((GameCardViewModel First, GameCardViewModel Second) pair) => pair.First == pair.Second))
		{
			OnPropertyChanged("LibraryMenuScrollOffset");
			return;
		}
		VisibleLibraryMenuGames.Clear();
		foreach (GameCardViewModel item in list2)
		{
			VisibleLibraryMenuGames.Add(item);
		}
		OnPropertyChanged("LibraryMenuScrollOffset");
	}

	private void OnGamesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RefreshDerivedLists();
	}

	private void RunningGameService_OnStateChanged(object? sender, EventArgs e)
	{
		System.Windows.Application current = System.Windows.Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			HandleRunningGameStateChangedOnUiThread();
		}
		else
		{
			val.BeginInvoke((Delegate)new Action(HandleRunningGameStateChangedOnUiThread), (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private void HandleRunningGameStateChangedOnUiThread()
	{
		OnPropertyChanged("HasRunningLaunchedGame");
		OnPropertyChanged("RunningLaunchedGameTitle");
		OnPropertyChanged("RunningGameFooterActionText");
		if (!_runningGameService.ConsumePlaytimeUpdate())
		{
			return;
		}
		foreach (GameCardViewModel game in Games)
		{
			game.Refresh();
		}
		PersistLibraryAsync();
	}
}
