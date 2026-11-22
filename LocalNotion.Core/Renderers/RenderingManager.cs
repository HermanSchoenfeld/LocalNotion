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

	private readonly Dictionary<string, string> _previousCmsPaths = new(StringComparer.OrdinalIgnoreCase);

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
	public IDisposable BeginBatch() {
		var renderer = new StandaloneHtmlRenderer(_themes.CreateSnapshot());
		var previousRenderer = _html;
		var previousAssets = _publishedAssets;
		var previousSources = _sources;
		if (previousAssets is null) {
			_cmsDestinations.Clear();
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
		if (cmsItems != null)
			foreach (var item in cmsItems)
				Reserve(() => ReserveCmsPath(item));
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
		var destination = ReserveCmsPath(item);
		var directoryValue = Path.GetDirectoryName(destination);
		Logger.Info($"Rendering CMS item '{item.RenderPath}'");
		R.DocumentBlock document = new CmsRenderModelBuilder(cmsRepository, directoryValue, Logger, _sources).Build(item);
		var result = _html.Render(document, Options(directoryValue, RenderMode.ReadOnly));
		PublishAssets(result.Assets);
		var html = result.Html;
#if CleanHTML
		if (document.RenderFrame && !result.SuppressFormatting)
			html = HtmlFormatting.Format(html);
#endif
		WriteOutput(destination, html);
		if (_previousCmsPaths.Remove(item.Slug, out var previous) && !PathsEqual(previous, destination) && File.Exists(previous))
			File.Delete(previous);
		item.Dirty = false;
		if (cmsRepository.ContainsCmsItem(item.Slug))
			cmsRepository.UpdateCMSItem(item);
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
		}
		if (previous != null && !PathsEqual(previous, destination))
			_previousCmsPaths[item.Slug] = previous;
		item.RenderPath = Path.GetRelativePath(root, destination).Replace('\\', '/');
		_cmsDestinations[item.Slug] = destination;
		if (Repository is CMSLocalNotionRepository cmsRepository && cmsRepository.ContainsCmsItem(item.Slug))
			cmsRepository.UpdateCMSItem(item);
		return destination;
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

	private static void WriteOutput(string destination, string html) {
		var temporary = Path.Combine(Path.GetDirectoryName(destination), ".render-" + Guid.NewGuid().ToString("N") + ".tmp");
		try {
			File.WriteAllText(temporary, html);
			File.Move(temporary, destination, overwrite: true);
		} finally { if (File.Exists(temporary)) File.Delete(temporary); }
	}

	private static bool PathsEqual(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
				OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	private static void EnsureSupported(RenderType type) {
		if (type != RenderType.HTML)
			throw new NotSupportedException($"Rendering '{type}' is not supported; use HTML.");
	}
}
