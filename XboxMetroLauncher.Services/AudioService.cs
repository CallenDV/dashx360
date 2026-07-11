using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class AudioService : IAudioService
{
	private sealed record NaudioPlayback(WaveOutEvent Output, AudioFileReader Reader);

	private readonly Func<bool> _isEnabled;

	private readonly Func<string> _selectedOutputDeviceName;

	private readonly Panel? _host;

	private readonly List<NaudioPlayback> _activeNaudioPlayers = new List<NaudioPlayback>();

	private readonly List<MediaPlayer> _activePlayers = new List<MediaPlayer>();

	private readonly List<MediaElement> _activeElements = new List<MediaElement>();

	private readonly Dictionary<MediaPlayer, string> _playerSoundNames = new Dictionary<MediaPlayer, string>();

	private readonly Dictionary<MediaElement, string> _elementSoundNames = new Dictionary<MediaElement, string>();

	private readonly Dictionary<string, DateTimeOffset> _lastPlayTimes = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

	private const int MaxActivePlayers = 8;

	private static readonly Dictionary<string, string[]> SoundFiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["startup"] = new string[2] { "02. Startup (2010).mp3", "startup.wav" },
		["page-left"] = new string[2] { "08. Page Left.mp3", "tab.wav" },
		["page-right"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["tab"] = new string[2] { "09. Page Right.mp3", "tab.wav" },
		["select"] = new string[3] { "10. Select A.mp3", "13. Select.mp3", "select.wav" },
		["activate"] = new string[3] { "10. Select A.mp3", "13. Select.mp3", "select.wav" },
		["back"] = new string[3] { "14. Back.mp3", "15. Back 2.mp3", "back.wav" },
		["focus"] = new string[3] { "13. Select.mp3", "11. Select A (Alt).mp3", "focus.wav" }
	};

	public AudioService(Func<bool> isEnabled, Panel? host = null, Func<string>? selectedOutputDeviceName = null)
	{
		_isEnabled = isEnabled;
		_host = host;
		_selectedOutputDeviceName = selectedOutputDeviceName ?? ((Func<string>)(() => "Default"));
	}

	public IReadOnlyList<string> GetOutputDeviceNames()
	{
		List<string> list = new List<string>();
		try
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\MMDevices\\Audio\\Render");
			if (registryKey == null)
			{
				return new _003C_003Ez__ReadOnlySingleElementList<string>("Default");
			}
			string[] subKeyNames = registryKey.GetSubKeyNames();
			foreach (string text in subKeyNames)
			{
				using RegistryKey registryKey2 = registryKey.OpenSubKey(text);
				if (!IsActiveRenderDevice(registryKey2?.GetValue("DeviceState")))
				{
					continue;
				}
				using RegistryKey registryKey3 = registryKey.OpenSubKey(text + "\\Properties");
				string endpointName = (registryKey3?.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string) ?? string.Empty;
				string deviceName = (registryKey3?.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string) ?? string.Empty;
				string text2 = FormatOutputDeviceName(endpointName, deviceName);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					dictionary.TryGetValue(text2, out var value);
					dictionary[text2] = value + 1;
					list.Add((value == 0) ? text2 : $"{text2} ({value + 1})");
				}
			}
		}
		catch (Exception exception)
		{
			App.LogException(exception, "AudioService.GetOutputDeviceNames");
		}
		if (list.Count != 0)
		{
			return list;
		}
		int i = 1;
		List<string> list2 = new List<string>(i);
		CollectionsMarshal.SetCount(list2, i);
		Span<string> span = CollectionsMarshal.AsSpan(list2);
		int num = 0;
		span[num] = "Default";
		num++;
		return list2;
	}

	public void Play(string soundName)
	{
		if (!_isEnabled() || IsThrottled(soundName))
		{
			return;
		}
		string text = ResolveSoundPath(soundName);
		if (text == null)
		{
			SystemSounds.Asterisk.Play();
		}
		else
		{
			if (!IsDefaultOutputDevice(_selectedOutputDeviceName()) && TryPlayThroughNaudio(text))
			{
				return;
			}
			if (_host == null)
			{
				if (string.Equals(Path.GetExtension(text), ".wav", StringComparison.OrdinalIgnoreCase))
				{
					using (SoundPlayer soundPlayer = new SoundPlayer(text))
					{
						soundPlayer.Play();
						return;
					}
				}
				MediaPlayer mediaPlayer = null;
				try
				{
					TrimActivePlayers();
					mediaPlayer = new MediaPlayer();
					mediaPlayer.MediaEnded += delegate
					{
						ClosePlayer(mediaPlayer);
					};
					mediaPlayer.MediaFailed += delegate
					{
						ClosePlayer(mediaPlayer);
					};
					_activePlayers.Add(mediaPlayer);
					_playerSoundNames[mediaPlayer] = soundName;
					mediaPlayer.Open(new Uri(text, UriKind.Absolute));
					mediaPlayer.Play();
					return;
				}
				catch
				{
					if (mediaPlayer != null)
					{
						ClosePlayer(mediaPlayer);
					}
					return;
				}
			}
			PlayThroughElement(text, soundName);
		}
	}

	public void Stop(string soundName)
	{
		foreach (MediaPlayer item in (from pair in _playerSoundNames
			where string.Equals(pair.Value, soundName, StringComparison.OrdinalIgnoreCase)
			select pair.Key).ToList())
		{
			ClosePlayer(item);
		}
		foreach (MediaElement item2 in (from pair in _elementSoundNames
			where string.Equals(pair.Value, soundName, StringComparison.OrdinalIgnoreCase)
			select pair.Key).ToList())
		{
			CloseElement(item2);
		}
		foreach (NaudioPlayback item3 in _activeNaudioPlayers.ToList())
		{
			CloseNaudioPlayback(item3);
		}
	}

	private bool TryPlayThroughNaudio(string path)
	{
		AudioFileReader audioFileReader = null;
		WaveOutEvent waveOutEvent = null;
		try
		{
			TrimActiveNaudioPlayers();
			audioFileReader = new AudioFileReader(path);
			waveOutEvent = new WaveOutEvent
			{
				DeviceNumber = ResolveOutputDeviceNumber(_selectedOutputDeviceName())
			};
			NaudioPlayback playback = new NaudioPlayback(waveOutEvent, audioFileReader);
			waveOutEvent.PlaybackStopped += delegate
			{
				CloseNaudioPlayback(playback);
			};
			waveOutEvent.Init(audioFileReader);
			_activeNaudioPlayers.Add(playback);
			waveOutEvent.Play();
			return true;
		}
		catch (Exception exception)
		{
			App.LogException(exception, "AudioService.NAudio");
			try
			{
				waveOutEvent?.Dispose();
				audioFileReader?.Dispose();
			}
			catch
			{
			}
			return false;
		}
	}

	private static bool IsDefaultOutputDevice(string? deviceName)
	{
		if (!string.IsNullOrWhiteSpace(deviceName))
		{
			return string.Equals(deviceName, "Default", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private int ResolveOutputDeviceNumber(string? deviceName)
	{
		if (string.IsNullOrWhiteSpace(deviceName) || string.Equals(deviceName, "Default", StringComparison.OrdinalIgnoreCase))
		{
			return -1;
		}
		int num = GetOutputDeviceNames().Select((string name, int index) => new { name, index }).FirstOrDefault(device => string.Equals(device.name, deviceName, StringComparison.OrdinalIgnoreCase))?.index ?? 0;
		if (num > 0)
		{
			return num - 1;
		}
		return -1;
	}

	private static bool IsActiveRenderDevice(object? state)
	{
		try
		{
			return Convert.ToInt32(state, CultureInfo.InvariantCulture) == 1;
		}
		catch
		{
			return false;
		}
	}

	private static string FormatOutputDeviceName(string endpointName, string deviceName)
	{
		endpointName = endpointName.Trim();
		deviceName = deviceName.Trim();
		if (string.IsNullOrWhiteSpace(endpointName))
		{
			return deviceName;
		}
		if (string.IsNullOrWhiteSpace(deviceName) || string.Equals(endpointName, deviceName, StringComparison.OrdinalIgnoreCase))
		{
			return endpointName;
		}
		return endpointName + " (" + deviceName + ")";
	}

	private void PlayThroughElement(string path, string soundName)
	{
		Panel? host = _host;
		object obj = ((host != null) ? ((DispatcherObject)host).Dispatcher : null);
		if (obj == null)
		{
			Application current = Application.Current;
			obj = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		}
		Dispatcher val = (Dispatcher)obj;
		if (val != null && !val.CheckAccess())
		{
			val.BeginInvoke((Delegate)(Action)delegate
			{
				PlayThroughElement(path, soundName);
			}, Array.Empty<object>());
		}
		else
		{
			if (_host == null)
			{
				return;
			}
			try
			{
				TrimActiveElements();
				MediaElement element = new MediaElement
				{
					Width = 1.0,
					Height = 1.0,
					Opacity = 0.0,
					IsHitTestVisible = false,
					LoadedBehavior = MediaState.Manual,
					UnloadedBehavior = MediaState.Manual,
					Volume = 1.0,
					Source = new Uri(path, UriKind.Absolute)
				};
				element.MediaEnded += delegate
				{
					CloseElement(element);
				};
				element.MediaFailed += delegate
				{
					CloseElement(element);
				};
				_activeElements.Add(element);
				_elementSoundNames[element] = soundName;
				_host.Children.Add(element);
				element.Play();
			}
			catch
			{
			}
		}
	}

	private static string? ResolveSoundPath(string soundName)
	{
		string[] value;
		string[] array = (SoundFiles.TryGetValue(soundName, out value) ? value : new string[2]
		{
			soundName + ".mp3",
			soundName + ".wav"
		});
		foreach (string item in AppPaths.CandidateRoots().SelectMany((string root) => new string[3]
		{
			Path.Combine(root, "Assets", "Audio", "Sounds"),
			Path.Combine(root, "sounds"),
			Path.Combine(root, "Assets", "Audio")
		}).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			.ToList())
		{
			string[] array2 = array;
			foreach (string path in array2)
			{
				string text = Path.Combine(item, path);
				if (File.Exists(text))
				{
					return text;
				}
			}
		}
		return null;
	}

	private void ClosePlayer(MediaPlayer mediaPlayer)
	{
		mediaPlayer.Close();
		_activePlayers.Remove(mediaPlayer);
		_playerSoundNames.Remove(mediaPlayer);
	}

	private void TrimActivePlayers()
	{
		while (_activePlayers.Count >= 8)
		{
			ClosePlayer(_activePlayers[0]);
		}
	}

	private void CloseNaudioPlayback(NaudioPlayback playback)
	{
		if (_activeNaudioPlayers.Remove(playback))
		{
			try
			{
				playback.Output.Stop();
			}
			catch
			{
			}
			playback.Output.Dispose();
			playback.Reader.Dispose();
		}
	}

	private void TrimActiveNaudioPlayers()
	{
		while (_activeNaudioPlayers.Count >= 8)
		{
			CloseNaudioPlayback(_activeNaudioPlayers[0]);
		}
	}

	private void CloseElement(MediaElement element)
	{
		try
		{
			element.Stop();
			element.Source = null;
			_host?.Children.Remove(element);
			_activeElements.Remove(element);
			_elementSoundNames.Remove(element);
		}
		catch
		{
		}
	}

	private void TrimActiveElements()
	{
		while (_activeElements.Count >= 8)
		{
			CloseElement(_activeElements[0]);
		}
	}

	private bool IsThrottled(string soundName)
	{
		TimeSpan timeSpan;
		switch (soundName)
		{
		case "select":
			timeSpan = TimeSpan.FromMilliseconds(80.0);
			break;
		case "focus":
			timeSpan = TimeSpan.FromMilliseconds(110.0);
			break;
		case "page-left":
		case "page-right":
		case "tab":
			timeSpan = TimeSpan.FromMilliseconds(150.0);
			break;
		default:
			timeSpan = TimeSpan.Zero;
			break;
		}
		TimeSpan timeSpan2 = timeSpan;
		if (timeSpan2 == TimeSpan.Zero)
		{
			return false;
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		if (_lastPlayTimes.TryGetValue(soundName, out var value) && utcNow - value < timeSpan2)
		{
			return true;
		}
		_lastPlayTimes[soundName] = utcNow;
		return false;
	}
}
