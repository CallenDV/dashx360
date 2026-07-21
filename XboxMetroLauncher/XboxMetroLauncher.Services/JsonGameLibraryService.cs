using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XboxMetroLauncher.Models;
using XboxMetroLauncher.Utilities;

namespace XboxMetroLauncher.Services;

public sealed class JsonGameLibraryService : IGameLibraryService
{
	private const string LibraryFileName = "library.json";

	private readonly IJsonStore _store;

	private readonly string _libraryPath;

	public JsonGameLibraryService(IJsonStore store)
	{
		_store = store;
		_libraryPath = Path.Combine(AppPaths.UserDataFolder, "library.json");
	}

	public async Task<GameLibrary> LoadAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		GameLibrary gameLibrary = await ReadLibraryFileAsync(_libraryPath, cancellationToken);
		if (gameLibrary != null)
		{
			await SaveAsync(gameLibrary, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return gameLibrary;
		}
		string path = AppPaths.FindFile(Path.Combine("Data", "library.seed.json"));
		if (File.Exists(path))
		{
			GameLibrary seeded = await ReadLibraryFileAsync(path, cancellationToken);
			if (seeded != null)
			{
				await SaveAsync(seeded, cancellationToken);
				return seeded;
			}
		}
		return new GameLibrary();
	}

	public Task SaveAsync(GameLibrary library, CancellationToken cancellationToken = default(CancellationToken))
	{
		return _store.WriteAsync("library.json", library, cancellationToken);
	}

	private static async Task<GameLibrary?> ReadLibraryFileAsync(string path, CancellationToken cancellationToken)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		await using FileStream stream = File.OpenRead(path);
		using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
		{
			AllowTrailingCommas = true,
			CommentHandling = JsonCommentHandling.Skip
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		JsonElement root = document.RootElement;
		if (root.ValueKind == JsonValueKind.Array)
		{
			return new GameLibrary
			{
				Games = root.Deserialize<List<GameMetadata>>() ?? new List<GameMetadata>()
			};
		}
		if (root.ValueKind == JsonValueKind.Object)
		{
			return root.Deserialize<GameLibrary>();
		}
		return null;
	}

	public Task<IReadOnlyList<GameMetadata>> ScanFolderAsync(string folderPath, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!Directory.Exists(folderPath))
		{
			return Task.FromResult((IReadOnlyList<GameMetadata>)Array.Empty<GameMetadata>());
		}
		HashSet<string> ignoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UnityCrashHandler64", "UnityCrashHandler32", "CrashReportClient", "unins000", "uninstall" };
		return Task.FromResult((IReadOnlyList<GameMetadata>)(from path in Directory.EnumerateFiles(folderPath, "*.exe", SearchOption.AllDirectories)
			where !ignoredNames.Contains(Path.GetFileNameWithoutExtension(path))
			select new GameMetadata
			{
				Title = CleanTitle(Path.GetFileNameWithoutExtension(path)),
				LaunchType = "Exe",
				ExecutablePath = path,
				WorkingDirectory = (Path.GetDirectoryName(path) ?? folderPath),
				Platform = "PC",
				Genre = "Imported"
			} into game
			orderby game.Title
			select game).ToList());
	}

	private static string CleanTitle(string value)
	{
		return value.Replace("_", " ").Replace("-", " ").Trim();
	}
}
