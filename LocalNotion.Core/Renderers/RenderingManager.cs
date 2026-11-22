// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sphere10.Framework.Web.VisualRenderer;
using Sphere10.Framework;
using StandaloneHtmlRenderer = Sphere10.Framework.Web.VisualRenderer.HtmlRenderer;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core;

/// <summary>Owns source projection, destination allocation, and persistence outside the renderer.</summary>
public class RenderingManager {

	private readonly Dictionary<string, string> _cmsDestinations = new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string[]> _cmsListingDestinations = new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CmsArticleListing> _cmsArticleListings = new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CmsRenderState> _previousCmsStates = new(StringComparer.OrdinalIgnoreCase);

	private readonly ThemeCatalog _themes;

	private ILocalNotionRepository _sources;

	private StandaloneHtmlRenderer _html;

	private HashSet<string> _publishedAssets;

	public RenderingManager(ILocalNotionRepository repository, ILogger logger = null) {
		this.Repository = repository ?? throw new ArgumentNullException(nameof(repository));
		this.Logger = logger ?? new NoOpLogger();
		_sources = repository;
		_themes = new ThemeCatalog(new ThemeOptions {
			ThemesDirectory = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute)
		});
		_html = new StandaloneHtmlRenderer(_themes);
	}

	public ILogger Logger { get; set; }

	public string AssetsDirectory => Path.Combine(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), Constants.DefaultRenderAssetsFolderName);

	protected ILocalNotionRepository Repository { get; }

	/// <summary>Uses one theme and source snapshot and verifies each asset once for a render batch.</summary>
	public ActionScope BeginBatch() {
		var renderer = new StandaloneHtmlRenderer(_themes.CreateSnapshot());
		var previousRenderer = _html;
		var previousAssets = _publishedAssets;
		var previousSources = _sources;
		if (previousAssets is null) {
			_cmsDestinations.Clear();
			_cmsListingDestinations.Clear();
			_cmsArticleListings.Clear();
			_previousCmsStates.Clear();
			_sources = new RenderSourceRepository(Repository);
		}
		_html = renderer;
		_publishedAssets = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
		return new ActionScope(() => {
			_html = previousRenderer;
			_publishedAssets = previousAssets;
			_sources = previousSources;
		});
	}

	/// <summary>Reserve the whole render batch before projection so forward links use final filenames.</summary>
	public void PrepareRenderPaths(IEnumerable<string> resourceIDs, RenderType renderTypeValue = RenderType.HTML, IEnumerable<CMSItem> cmsItems = null, bool faultTolerant = false, CancellationToken cancellationTokenValue = default) {
		EnsureSupported(renderTypeValue);
		cancellationTokenValue.ThrowIfCancellationRequested();
		foreach (var resourceID in resourceIDs.Distinct(StringComparer.OrdinalIgnoreCase))
			Reserve(() => ReserveResourcePath(resourceID));
		if (cmsItems != null) {
			var items = cmsItems.ToArray();
			foreach (var item in items)
				Reserve(() => ReserveCmsPath(item));
			// Primary CMS destinations have priority over supplementary listing files.
			foreach (var item in items.Where(item => item.ItemType == CMSItemType.CategoryPage))
				Reserve(() => ReserveCmsListingPaths(item));
		}
		void Reserve(Action actionValue) {
			cancellationTokenValue.ThrowIfCancellationRequested();
			try { actionValue(); } catch (OperationCanceledException) { throw; } catch (Exception error) when (faultTolerant) { Logger.Warning("Failed to reserve a render destination; rendering will report the affected resource."); Logger.Exception(error); }
		}
	}

	public string RenderLocalResource(string resourceID, RenderType renderTypeValue, RenderMode renderModeValue) {
		EnsureSupported(renderTypeValue);
		ArgumentNullException.ThrowIfNull(resourceID);
		var resource = GetEditableResource(resourceID);
		var destination = ReserveResourcePath(resource.ID);
		var directoryValue = Path.GetDirectoryName(destination);
		var temporary = Path.GetTempFileName();
		try {
			R.DocumentBlock model = new NotionRenderModelBuilder(_sources, Logger).Build(resource.ID, directoryValue);
			var result = _html.Render(model, Options(directoryValue, renderModeValue));
			PublishAssets(result.Assets);
			File.WriteAllText(temporary, result.Html);
			return Repository.ImportResourceRender(resource.ID, RenderType.HTML, temporary);
		} catch (OperationCanceledException) {
			throw;
		} catch (Exception error) {
			// Preserve the existing diagnostic-render behavior and the original exception.
			Tools.Exceptions.ExecuteIgnoringException(() => {
				File.WriteAllText(temporary, error.ToDiagnosticString());
				Repository.ImportResourceRender(resource.ID, RenderType.HTML, temporary);
			});
			throw;
		} finally {
			File.Delete(temporary);
		}
	}

	public void RenderCMSItem(CMSItem item) {
		if (Repository is not CMSLocalNotionRepository cmsRepository)
			throw new InvalidOperationException("CMS rendering requires a CMS repository.");
		ArgumentNullException.ThrowIfNull(item);
		CaptureCmsState(item);
		var previous = _previousCmsStates[item.Slug];
		string[] destinations;
		try {
			var destination = ReserveCmsPath(item);
			destinations = item.ItemType == CMSItemType.CategoryPage ? ReserveCmsListingPaths(item) : [destination];
			var outputs = new List<CmsOutput>();
			// Project and render every slice before replacing any existing listing output.
			for (var index = 0; index < destinations.Length; index++) {
				var directoryValue = Path.GetDirectoryName(destinations[index]);
				Logger.Info($"Rendering CMS item '{Path.GetRelativePath(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), destinations[index])}'");
				R.DocumentBlock document = new CmsRenderModelBuilder(cmsRepository, directoryValue, Logger, _sources)
					.Build(item, index + 1, item.ItemType == CMSItemType.CategoryPage ? _cmsArticleListings[item.Slug] : null);
				var result = _html.Render(document, Options(directoryValue, RenderMode.ReadOnly));
				PublishAssets(result.Assets);
				var html = result.Html;
#if CleanHTML
				if (document.RenderFrame && !result.SuppressFormatting)
					html = HtmlFormatting.Format(html);
#endif
				outputs.Add(new CmsOutput(destinations[index], html));
			}
			WriteOutputs(outputs.ToArray(), () => {
				item.ListingRenderPaths = item.ItemType == CMSItemType.CategoryPage
					? destinations.Select(path => Path.GetRelativePath(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), path).Replace('\\', '/')).ToArray() : [];
				item.Dirty = false;
				if (cmsRepository.ContainsCmsItem(item.Slug))
					cmsRepository.UpdateCMSItem(item);
			});
		} catch {
			RestoreCmsState(item);
			throw;
		}
		_previousCmsStates.Remove(item.Slug);
		// Retirement follows the commit boundary. A cleanup failure must retain the new, complete manifest.
		foreach (var path in previous.ListingRenderPaths.Append(previous.RenderPath).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct()) {
			var absolute = Path.GetFullPath(path, Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
			if (!destinations.Any(current => PathsEqual(current, absolute)) && File.Exists(absolute))
				File.Delete(absolute);
		}
	}

	private LocalNotionEditableResource GetEditableResource(string resourceID) {
		if (!Repository.TryGetResource(resourceID, out var resource) && Guid.TryParse(resourceID, out var guidValue)) {
			if (!Repository.TryGetResource(LocalNotionHelper.ObjectGuidToId(guidValue), out resource))
				Repository.TryGetResource(guidValue.ToString("N"), out resource);
		}
		return resource as LocalNotionEditableResource
			?? throw new InvalidOperationException($"Resource '{resourceID}' is not a page or database.");
	}

	private string ReserveResourcePath(string resourceID) {
		var resource = GetEditableResource(resourceID);
		if (!resource.Renders.TryGetValue(RenderType.HTML, out var entry)) {
			Repository.ImportBlankResourceRender(resource.ID, RenderType.HTML);
			entry = resource.Renders[RenderType.HTML];
		}
		return Path.GetFullPath(entry.LocalPath, Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
	}

	private string ReserveCmsPath(CMSItem item) {
		CaptureCmsState(item);
		if (_cmsDestinations.TryGetValue(item.Slug, out var reserved))
			return reserved;
		var root = Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		var desired = Repository.Paths.CalculateResourceFilePath(LocalNotionResourceType.CMS, item.Slug, item.Title, RenderType.HTML, FileSystemPathType.Absolute);
		var previous = string.IsNullOrWhiteSpace(item.RenderPath) ? null : Path.GetFullPath(item.RenderPath, root);
		var unconflictedPrevious = previous == null ? null : Repository.Paths.RemoveConflictResolutionFromFilePath(previous);
		var destination = unconflictedPrevious != null && PathsEqual(unconflictedPrevious, desired)
			? previous : Repository.Paths.ResolveConflictingFilePath(desired);
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		// A placeholder reserves the filename for subsequent members of this batch.
		if (!File.Exists(destination)) {
			using var placeholder = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
			_previousCmsStates[item.Slug].NewOutputs.Add(destination);
		}
		item.RenderPath = Path.GetRelativePath(root, destination).Replace('\\', '/');
		_cmsDestinations[item.Slug] = destination;
		if (Repository is CMSLocalNotionRepository cmsRepository && cmsRepository.ContainsCmsItem(item.Slug))
			cmsRepository.UpdateCMSItem(item);
		return destination;
	}

	private string[] ReserveCmsListingPaths(CMSItem item) {
		if (_cmsListingDestinations.TryGetValue(item.Slug, out var reserved))
			return reserved;
		if (Repository is not CMSLocalNotionRepository cmsRepository)
			throw new InvalidOperationException("CMS rendering requires a CMS repository.");
		var primary = ReserveCmsPath(item);
		var root = Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		var state = _previousCmsStates[item.Slug];
		var listing = new CmsArticleListing(cmsRepository, item);
		_cmsArticleListings[item.Slug] = listing;
		var destinations = new string[listing.PageCount];
		destinations[0] = primary;
		for (var index = 1; index < destinations.Length; index++) {
			var desired = Path.Combine(Path.GetDirectoryName(primary), Path.GetFileNameWithoutExtension(primary) + "_page_" + (index + 1) + ".html");
			var previous = state.ListingRenderPaths.Length > index ? Path.GetFullPath(state.ListingRenderPaths[index], root) : null;
			var destination = previous != null && PathsEqual(Repository.Paths.RemoveConflictResolutionFromFilePath(previous), desired)
				&& !_cmsDestinations.Values.Any(path => PathsEqual(path, previous))
				? previous : Repository.Paths.ResolveConflictingFilePath(desired);
			if (!File.Exists(destination)) {
				using var placeholder = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
				state.NewOutputs.Add(destination);
			}
			destinations[index] = destination;
		}
		item.ListingRenderPaths = destinations.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray();
		_cmsListingDestinations[item.Slug] = destinations;
		return destinations;
	}

	private void CaptureCmsState(CMSItem item) {
		if (!_previousCmsStates.ContainsKey(item.Slug))
			_previousCmsStates[item.Slug] = new CmsRenderState(item.RenderPath, item.ListingRenderPaths?.ToArray() ?? [], item.Dirty);
	}

	private void RestoreCmsState(CMSItem item) {
		if (!_previousCmsStates.Remove(item.Slug, out var state))
			return;
		item.RenderPath = state.RenderPath;
		item.ListingRenderPaths = state.ListingRenderPaths;
		item.Dirty = state.Dirty;
		_cmsDestinations.Remove(item.Slug);
		_cmsListingDestinations.Remove(item.Slug);
		_cmsArticleListings.Remove(item.Slug);
		if (Repository is CMSLocalNotionRepository cmsRepository && cmsRepository.ContainsCmsItem(item.Slug))
			cmsRepository.UpdateCMSItem(item);
		foreach (var path in state.NewOutputs)
			if (File.Exists(path) && new FileInfo(path).Length == 0)
				File.Delete(path);
	}

	private RenderOptions Options(string directoryValue, RenderMode mode) {
		var assetRelativePath = Path.GetRelativePath(directoryValue, AssetsDirectory).Replace('\\', '/');
		var assetBase = Repository.Paths.Mode == LocalNotionMode.Online
			? Repository.Paths.GetRemoteHostedBaseUrl().TrimEnd('/') + "/" + Path.GetRelativePath(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), AssetsDirectory).Replace('\\', '/')
			: assetRelativePath;
		return new RenderOptions {
			Themes = Repository.DefaultThemes,
			Mode = mode == RenderMode.Editable ? Sphere10.Framework.Web.VisualRenderer.RenderMode.Editable : Sphere10.Framework.Web.VisualRenderer.RenderMode.ReadOnly,
			Environment = Repository.Paths.Mode == LocalNotionMode.Online ? RenderEnvironment.Online : RenderEnvironment.Offline,
			AssetBaseUrl = assetBase
		};
	}

	private void PublishAssets(IEnumerable<RenderAsset> assets) {
		var root = Path.GetFullPath(AssetsDirectory);
		var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
		foreach (var asset in assets) {
			var pathValue = Path.GetFullPath(asset.RelativePath.Replace('/', Path.DirectorySeparatorChar), root);
			if (!pathValue.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
				throw new InvalidOperationException($"Asset path '{asset.RelativePath}' escapes the output directory.");
			if (_publishedAssets?.Contains(pathValue) == true)
				continue;
			if (File.Exists(pathValue) && File.ReadAllBytes(pathValue).AsSpan().SequenceEqual(asset.Content.Span)) {
				_publishedAssets?.Add(pathValue);
				continue;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(pathValue));
			var temporary = pathValue + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try {
				File.WriteAllBytes(temporary, asset.Content.ToArray());
				File.Move(temporary, pathValue, overwrite: true);
				_publishedAssets?.Add(pathValue);
			} finally { if (File.Exists(temporary)) File.Delete(temporary); }
		}
	}

	private static void WriteOutputs(CmsOutput[] outputs, Action commit) {
		var staged = new List<CmsStagedOutput>();
		try {
			// Stage complete replacements and backups before publishing the first page.
			foreach (var output in outputs) {
				var temporary = Path.Combine(Path.GetDirectoryName(output.Destination), ".render-" + Guid.NewGuid().ToString("N"));
				var entry = new CmsStagedOutput(output.Destination, temporary + ".tmp", temporary + ".bak");
				staged.Add(entry);
				File.WriteAllText(entry.Temporary, output.Html);
				if (File.Exists(entry.Destination))
					File.Copy(entry.Destination, entry.Backup);
			}
			foreach (var entry in staged) {
				File.Move(entry.Temporary, entry.Destination, overwrite: true);
				entry.Published = true;
			}
			commit();
		} catch {
			foreach (var entry in staged.Where(entry => entry.Published)) {
				if (File.Exists(entry.Backup))
					File.Move(entry.Backup, entry.Destination, overwrite: true);
				else
					File.Delete(entry.Destination);
			}
			throw;
		} finally {
			foreach (var entry in staged) {
				// Temporary-file cleanup must not turn a committed output into a metadata rollback.
				Tools.Exceptions.ExecuteIgnoringException(() => File.Delete(entry.Temporary));
				Tools.Exceptions.ExecuteIgnoringException(() => File.Delete(entry.Backup));
			}
		}
	}

	private static bool PathsEqual(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
				OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	private static void EnsureSupported(RenderType type) {
		if (type != RenderType.HTML)
			throw new NotSupportedException($"Rendering '{type}' is not supported; use HTML.");
	}

	#region Inner Types

	private sealed record CmsOutput(string Destination, string Html);

	private sealed class CmsRenderState(string renderPath, string[] listingRenderPaths, bool dirty) {
		public string RenderPath { get; } = renderPath;
		public string[] ListingRenderPaths { get; } = listingRenderPaths;
		public bool Dirty { get; } = dirty;
		public List<string> NewOutputs { get; } = new();
	}

	private sealed class CmsStagedOutput(string destination, string temporary, string backup) {
		public string Destination { get; } = destination;
		public string Temporary { get; } = temporary;
		public string Backup { get; } = backup;
		public bool Published { get; set; }
	}

	#endregion
}
