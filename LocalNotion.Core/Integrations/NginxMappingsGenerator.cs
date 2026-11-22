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
using System.Text;
using System.Threading.Tasks;
using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;
public class NginxMappingsGenerator(ILocalNotionRepository localNotionRepository) {

	public const string MappingFileName = "ln-urls.conf";

	public const string RedirectsFileName = "ln-redirects.conf";

	private const string NGinxConf = 
		"""
		worker_processes auto;  # Automatically adjusts to the number of available CPU cores
		worker_rlimit_nofile 8192;  # Increase file descriptor limit to handle more connections
		
		events {
		    worker_connections 4096;  # Allows more concurrent connections per worker
		
		    # Linux-only Settings
		    use epoll;  # Use epoll for efficient event handling on Linux (comment this out on Windows)
		}
		
		http {
		    include       mime.types;
		    default_type  application/octet-stream;
		
		    sendfile on;  # Efficient file transfer using zero-copy in the kernel
		    tcp_nopush on;  # Optimizes packet transmission for large responses (comment this out on Windows)
		    tcp_nodelay on;  # Reduces latency for small responses by disabling delayed ACKs
		    keepalive_timeout 15;  # Shortened for better handling of persistent connections from the reverse proxy
		    client_max_body_size 50k;  # Lowered limit as no uploads are required; helps block oversized requests
		    send_timeout 20s;  # Default timeout for general requests
		
		    server {
		        listen 80;
		        server_name localhost;
		
		        # Windows hosting (nb: nginx.exe inside /.localnotion/nginx)
		        # root ../../;
		
		        # Linux hosting (NGINX docker container)
		        root /usr/share/nginx/html/;
		
		        # Docker logging to stdout and stderr
		        # Note: In Docker, NGINX logs are directed to stdout and stderr by default for easy access via `docker logs`
		        # access_log /dev/stdout;
		        # error_log /dev/stderr;
	
		        # Caching for common static assets (commented out for Cloudflare)
		        # Note: If using Cloudflare, avoid setting caching here as Cloudflare will handle asset caching.
		        # location ~* \.(jpg|jpeg|png|gif|ico|css|js|webp)$ {
		        #     expires 30d;
		        #     access_log off;  # Reduce disk I/O by disabling logging for assets
		        # }
		
		        # Repository metadata and Git history must never be served as static content.
		        location = /.localnotion { return 404; }
		        location ^~ /.localnotion/ { return 404; }
		        location = /.git { return 404; }
		        location ^~ /.git/ { return 404; }

		        include ln-urls.conf;
		    }
		}
		""";

	private const string MimeTypesFile = 
		"""
		types {
		    text/html                                        html htm shtml;
		    text/css                                         css;
		    text/xml                                         xml;
		    image/gif                                        gif;
		    image/jpeg                                       jpeg jpg;
		    application/javascript                           js;
		    application/atom+xml                             atom;
		    application/rss+xml                              rss;
		
		    text/mathml                                      mml;
		    text/plain                                       txt;
		    text/vnd.sun.j2me.app-descriptor                 jad;
		    text/vnd.wap.wml                                 wml;
		    text/x-component                                 htc;
		
		    image/avif                                       avif;
		    image/png                                        png;
		    image/svg+xml                                    svg svgz;
		    image/tiff                                       tif tiff;
		    image/vnd.wap.wbmp                               wbmp;
		    image/webp                                       webp;
		    image/x-icon                                     ico;
		    image/x-jng                                      jng;
		    image/x-ms-bmp                                   bmp;
		
		    font/woff                                        woff;
		    font/woff2                                       woff2;
		
		    application/java-archive                         jar war ear;
		    application/json                                 json;
		    application/mac-binhex40                         hqx;
		    application/msword                               doc;
		    application/pdf                                  pdf;
		    application/postscript                           ps eps ai;
		    application/rtf                                  rtf;
		    application/vnd.apple.mpegurl                    m3u8;
		    application/vnd.google-earth.kml+xml             kml;
		    application/vnd.google-earth.kmz                 kmz;
		    application/vnd.ms-excel                         xls;
		    application/vnd.ms-fontobject                    eot;
		    application/vnd.ms-powerpoint                    ppt;
		    application/vnd.oasis.opendocument.graphics      odg;
		    application/vnd.oasis.opendocument.presentation  odp;
		    application/vnd.oasis.opendocument.spreadsheet   ods;
		    application/vnd.oasis.opendocument.text          odt;
		    application/vnd.openxmlformats-officedocument.presentationml.presentation
		                                                     pptx;
		    application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
		                                                     xlsx;
		    application/vnd.openxmlformats-officedocument.wordprocessingml.document
		                                                     docx;
		    application/vnd.wap.wmlc                         wmlc;
		    application/wasm                                 wasm;
		    application/x-7z-compressed                      7z;
		    application/x-cocoa                              cco;
		    application/x-java-archive-diff                  jardiff;
		    application/x-java-jnlp-file                     jnlp;
		    application/x-makeself                           run;
		    application/x-perl                               pl pm;
		    application/x-pilot                              prc pdb;
		    application/x-rar-compressed                     rar;
		    application/x-redhat-package-manager             rpm;
		    application/x-sea                                sea;
		    application/x-shockwave-flash                    swf;
		    application/x-stuffit                            sit;
		    application/x-tcl                                tcl tk;
		    application/x-x509-ca-cert                       der pem crt;
		    application/x-xpinstall                          xpi;
		    application/xhtml+xml                            xhtml;
		    application/xspf+xml                             xspf;
		    application/zip                                  zip;
		
		    application/octet-stream                         bin exe dll;
		    application/octet-stream                         deb;
		    application/octet-stream                         dmg;
		    application/octet-stream                         iso img;
		    application/octet-stream                         msi msp msm;
		
		    audio/midi                                       mid midi kar;
		    audio/mpeg                                       mp3;
		    audio/ogg                                        ogg;
		    audio/x-m4a                                      m4a;
		    audio/x-realaudio                                ra;
		
		    video/3gpp                                       3gpp 3gp;
		    video/mp2t                                       ts;
		    video/mp4                                        mp4;
		    video/mpeg                                       mpeg mpg;
		    video/quicktime                                  mov;
		    video/webm                                       webm;
		    video/x-flv                                      flv;
		    video/x-m4v                                      m4v;
		    video/x-mng                                      mng;
		    video/x-ms-asf                                   asx asf;
		    video/x-ms-wmv                                   wmv;
		    video/x-msvideo                                  avi;
		}
		""";

	
	private const string MappingsFileHeader =
		"""
		# This file contains the uri to file mappings for hosting via nginx.
		#
		# Include this file from your nginx.conf inside your sever declarion
		#
		# Example:
		#
		#   ...
		#   server {
		#	root /path/to/parent/folder;  
		#	include /path/to/parent/folder/nginx.conf 
		#       ...
		#   }
		#   ...
		#
		# NOTE: ensure that the server root is configured to point to the folder containing this file

		# Rule to route 401 errors to @auth location
		error_page 401 @auth;

		# Catch-all rule to remove trailing slash
		location ~ (.+)/$ {
		    return 301 $1;
		}

		# Basic auth sername/logon rule
		location @auth {
			add_header WWW-Authenticate 'Basic realm="Restricted Area"' always;
			return 401;
		}		

		""";

	private const string RedirectsFileHeader =
		"""
		# Old page and file addresses that Local Notion keeps after an address changes. They are recorded in the
		# repository registry ("redirects") and regenerated with ln-urls.conf, which includes this file.
		# Hard redirects answer 301 and soft redirects 302; query strings carry over to the new address.

		""";

	// The mask keeps Nginx starting where the hosting configuration does not provide this file yet.
	private const string RedirectsInclude =
		"""

		# Redirects from old addresses
		include ln-redirects*.conf;
		""";

	private const string LocationTemplate =
		"""
		location = {slug} {
			try_files {relPath} =404;
			default_type {mimeType};
		}

		""";

		private const string ProtectedLocationTemplate =
		"""
		location = {slug} {
			if ($http_authorization != "Basic {base64AuthCode}") {
				return 401;
			}

			try_files {relPath} =404;
			default_type {mimeType};
		}

		""";
	private static readonly string[] ExemptFiles = ["sitemap.xml"];


	public ILocalNotionRepository LocalNotionRepository { get; } = localNotionRepository;

	public string CalculateNGinxFolderPath() => Path.Combine(LocalNotionRepository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), ".localnotion", "nginx");

	public async Task GenerateNGinxFolder(bool overwrite = false) {
		var folderPath = CalculateNGinxFolderPath();
		if (!Directory.Exists(folderPath))
			await Tools.FileSystem.CreateDirectoryAsync(folderPath);
		else if (overwrite) {
			await Tools.FileSystem.DeleteDirectoryAsync(folderPath);
			await Tools.FileSystem.CreateDirectoryAsync(folderPath);
		}

		// conf folder
		var confPath = Path.Combine(folderPath, "conf");
		if (!Directory.Exists(confPath))
			await Tools.FileSystem.CreateDirectoryAsync(confPath);

		// temp folder
		var tempPath = Path.Combine(folderPath, "temp");
		if (!Directory.Exists(tempPath))
			await Tools.FileSystem.CreateDirectoryAsync(tempPath);

		// logs folder
		var logsPath = Path.Combine(folderPath, "logs");
		if (!Directory.Exists(logsPath))
			await Tools.FileSystem.CreateDirectoryAsync(logsPath);

		// conf file
		var confFile = Path.Combine(confPath, "nginx.conf");
		if (!File.Exists(confFile)) 
			await File.WriteAllTextAsync(confFile, NGinxConf);

		// mime.types file
		var mimeTypesFile = Path.Combine(confPath, "mime.types");
		if (!File.Exists(mimeTypesFile))
			await File.WriteAllTextAsync(mimeTypesFile, MimeTypesFile);
	}

	public async Task GenerateUrlMappingsFile() {
		var hostableResources = LocalNotionHelper.EnumerateWebHostableResources(LocalNotionRepository).ToArray();
		var listingRenderPaths = CalculateListingRenderPaths(hostableResources);
		var listingTargets = listingRenderPaths.SelectMany(listing => {
			var resource = hostableResources.Single(resource => resource.Slug == listing.Key);
			return listing.Value.Skip(1).Select(path => new LocalNotionHelper.WebHostableResource(resource.Slug, path, resource.MimeType, resource.Authentication));
		}).ToArray();
		// Validate protection before opening the file: a collision must preserve the previous configuration.
		var protectedPhysicalAliases = CalculateProtectedPhysicalAliases(hostableResources, listingTargets);
		await GenerateRedirectsFile(hostableResources, listingRenderPaths, protectedPhysicalAliases);
		var mappingsFilePath = Path.Combine(CalculateNGinxFolderPath(), "conf", MappingFileName);
		await using var writer = new FileTextWriter(mappingsFilePath, FileMode.Create);
		await writer.WriteLineAsync(MappingsFileHeader);

		// Generate entries for hostable resources
		foreach (var hostableResource in hostableResources) {
			if (listingRenderPaths.TryGetValue(hostableResource.Slug, out var paths)) {
				await writer.WriteLineAsync(GenerateListingLocation(hostableResource, paths));
				if (hostableResource.Slug.Length > 0 && !hostableResource.Slug.EndsWith('/')) {
					var alias = QuoteNginxLiteral("/" + hostableResource.Slug + "/");
					var destination = QuoteNginxLiteral("/" + hostableResource.Slug + "$is_args$args");
					await writer.WriteLineAsync($"location = {alias} {{ return 301 {destination}; }}");
				}
				continue;
			}
			var template = !string.IsNullOrWhiteSpace(hostableResource.Authentication) ? ProtectedLocationTemplate : LocationTemplate;
			var location = 				
				template.FormatWithDictionary(
				new Dictionary<string, string> {
					["slug"] = "/" + hostableResource.Slug,
					["relPath"] = SanitizePath(hostableResource.RelPath),
					["mimeType"] = hostableResource.MimeType,
					["base64AuthCode"] = hostableResource.Authentication?.ToBase64()
				}, true
			);
			await writer.WriteLineAsync(location);
		}

		// The static root also exposes backing files. Give those URLs the same protection as their routes.
		foreach (var (physicalUrl, resource) in protectedPhysicalAliases) {
			var location = ProtectedLocationTemplate.FormatWithDictionary(
				new Dictionary<string, string> {
					["slug"] = QuoteNginxLiteral(physicalUrl),
					["relPath"] = SanitizePath(resource.RelPath),
					["mimeType"] = resource.MimeType,
					["base64AuthCode"] = resource.Authentication.ToBase64()
				}, true
			);
			await writer.WriteLineAsync(location);
		}

		// Generate entries for theme resources
		// TODO

		// Generate sitemap.xml entry
		var siteMapPath = LocalNotionRepository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Relative);
		var sitemapEntry = LocationTemplate.FormatWithDictionary(
			new Dictionary<string, string> {
				["slug"] = "/sitemap.xml",
				["relPath"] = SanitizePath(Path.Combine(siteMapPath, "sitemap.xml")),
				["mimeType"] = "application/xml"
			}, true
		);
		await writer.WriteLineAsync(sitemapEntry);
		await writer.WriteLineAsync(RedirectsInclude);
	}

	public async Task GenerateSiteMap() {
		var cmsRepo = LocalNotionRepository as CMSLocalNotionRepository;
		if (cmsRepo is null)
			return;

		var cmsPath = LocalNotionRepository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute);
		if (!Directory.Exists(cmsPath))
			return;
		var siteMapGenerator = new SitemapGenerator();
		var siteMapXml = siteMapGenerator.Generate(cmsRepo);
		var siteMapPath = Path.Combine(cmsPath, "sitemap.xml");
		Tools.Xml.WriteToFile(siteMapPath, siteMapXml);
	}
	
	public async Task RemoveOldCmsRenders() {
		var cmsPath = LocalNotionRepository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute);
		if (!Directory.Exists(cmsPath))
			return;

		var repoPath = LocalNotionRepository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		var allRequiredFiles = LocalNotionRepository.CMSItems
			.SelectMany(item => (item.ListingRenderPaths ?? []).Append(item.RenderPath))
			.Where(renderPath => !string.IsNullOrWhiteSpace(renderPath))
			.Select(renderPath => Path.GetFullPath(renderPath, repoPath))
			.ToHashSet(pathComparer);
		allRequiredFiles.UnionWith(ExemptFiles.Select(file => Path.GetFullPath(file, cmsPath)));

		// Stored paths use portable separators; compare canonical file paths before deleting any renders.
		foreach (var file in Directory.EnumerateFiles(cmsPath))
			if (!allRequiredFiles.Contains(Path.GetFullPath(file)))
				File.Delete(file);
	}

	public static async Task<string> GenerateNGinxFiles(ILocalNotionRepository localNotionRepository) {
		var generator = new NginxMappingsGenerator(localNotionRepository); 
		var folderPath = generator.CalculateNGinxFolderPath();
		if (!Directory.Exists(folderPath))
			await generator.GenerateNGinxFolder();
		await generator.GenerateUrlMappingsFile();
		await generator.GenerateSiteMap();
		await generator.RemoveOldCmsRenders();
		return folderPath;
	}

	public static string CalculateNGinxFolder(ILocalNotionRepository localNotionRepository) {
		var generator = new NginxMappingsGenerator(localNotionRepository); 
		return generator.CalculateNGinxFolderPath();
	}

	public static string CalculateMappingFile(ILocalNotionRepository localNotionRepository) 
		=> Path.Combine(CalculateNGinxFolder(localNotionRepository), "conf", MappingFileName);

	public static string SanitizePath(string path) {
		if (string.IsNullOrWhiteSpace(path))
			return path;

		path = path.ToUnixPath();
		if (!path.StartsWith("/") && !path.StartsWith("../"))
			path = "/" + path;

		return $"\"{path}\"";
	}

	private Dictionary<string, string[]> CalculateListingRenderPaths(LocalNotionHelper.WebHostableResource[] resources) {
		var listings = new Dictionary<string, string[]>(StringComparer.Ordinal);
		var listingItems = LocalNotionRepository.CMSItems.Where(item => item.ListingRenderPaths is { Length: > 0 }).ToArray();
		if (listingItems.Length == 0)
			return listings;
		var repoPath = LocalNotionRepository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		var cmsPath = LocalNotionRepository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Relative);
		var sitemap = new LocalNotionHelper.WebHostableResource("sitemap.xml", Path.Combine(cmsPath, "sitemap.xml"), "application/xml", null);
		var allResources = resources.Append(sitemap).ToArray();
		var allTargets = allResources.Select(resource => NormalizeTarget(resource.RelPath)).ToArray();
		var cmsTargetPrefix = NormalizeTarget(cmsPath).TrimEnd('/') + "/";
		var listingTargets = new HashSet<string>(StringComparer.Ordinal);
		foreach (var item in listingItems) {
			if (item.Slug is null || resources.Count(resource => resource.Slug == item.Slug) != 1 || listings.ContainsKey(item.Slug))
				throw new InvalidOperationException("An article listing must have one unambiguous hosting route.");
			if (item.Slug.Any(character => char.IsControl(character) || character is '$' or '"'))
				throw new InvalidOperationException("An article listing route contains unsupported Nginx path syntax.");
			if (item.Slug.Length > 0 && !item.Slug.EndsWith('/') && allResources.Any(resource => resource.Slug == item.Slug + "/"))
				throw new InvalidOperationException("An article listing's trailing-slash alias conflicts with another hosting route.");
			var ownResource = resources.Single(resource => resource.Slug == item.Slug);
			var paths = item.ListingRenderPaths.Select(path => {
				if (string.IsNullOrWhiteSpace(path) || path.Any(character => char.IsControl(character) || character is '$' or '"'))
					throw new InvalidOperationException("An article listing target contains unsupported Nginx path syntax.");
				if (path.IndexOfAny(['?', '#']) >= 0)
					throw new InvalidOperationException("An article listing target cannot contain query or fragment delimiters.");
				var target = NormalizeTarget(path);
				if (target is "." or ".." || target.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(target))
					throw new InvalidOperationException("An article listing target must remain beneath the repository root.");
				if (!target.StartsWith(cmsTargetPrefix, StringComparison.Ordinal) || !target.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException("An article listing target must be an HTML render beneath the CMS directory.");
				if (OperatingSystem.IsWindows() && File.Exists(Path.Combine(repoPath, target)) && !HasExactFileSystemCase(repoPath, target))
					throw new InvalidOperationException("An article listing target's case does not match its existing file.");
				if (!listingTargets.Add(target))
					throw new InvalidOperationException("Article listings cannot share physical page targets.");
				for (var i = 0; i < allResources.Length; i++) {
					var other = allResources[i];
					if (ReferenceEquals(other, ownResource)) {
						if (other.Slug == target)
							throw new InvalidOperationException("An article listing route cannot be one of its physical page targets.");
						continue;
					}
					if (allTargets[i] == target || "/" + other.Slug == "/" + target)
						throw new InvalidOperationException("An article listing target conflicts with another hosting route.");
				}
				return target;
			}).ToArray();
			if (paths[0] != NormalizeTarget(item.RenderPath))
				throw new InvalidOperationException("An article listing's first target must match its primary render path.");
			listings.Add(item.Slug, paths);
		}
		return listings;

		string NormalizeTarget(string path) {
			if (string.IsNullOrWhiteSpace(path))
				throw new InvalidOperationException("A hosting route has no physical target.");
			return Path.GetRelativePath(repoPath, Path.GetFullPath(path.ToUnixPath(), repoPath)).ToUnixPath();
		}
	}

	private static string GenerateListingLocation(LocalNotionHelper.WebHostableResource resource, string[] paths) {
		var location = new StringBuilder();
		location.AppendLine($"location = {QuoteNginxLiteral("/" + resource.Slug)} {{");
		if (!string.IsNullOrWhiteSpace(resource.Authentication)) {
			location.AppendLine($"\tif ($http_authorization != \"Basic {resource.Authentication.ToBase64()}\") {{");
			location.AppendLine("\t\treturn 401;");
			location.AppendLine("\t}");
		}

		// Only fixed generated targets reach the internal redirect. Tracking arguments remain untouched.
		location.AppendLine("\tif ($args ~* \"(^|&)page(?:=[^&]*)?&(?:[^&]*&)*page(?:=|&|$)\") { return 400; }");
		location.AppendLine("\tset $ln_listing_path \"\";");
		location.AppendLine($"\tif ($arg_page = \"\") {{ set $ln_listing_path {SanitizePath(paths[0])}; }}");
		for (var i = 0; i < paths.Length; i++)
			location.AppendLine($"\tif ($arg_page = \"{i + 1}\") {{ set $ln_listing_path {SanitizePath(paths[i])}; }}");
		location.AppendLine("\tif ($ln_listing_path = \"\") { return 404; }");
		// A successful if/set enters an implicit location that does not inherit try_files.
		// Re-select the validated physical route instead; its existing authentication policy applies again.
		location.AppendLine("\trewrite ^ $ln_listing_path last;");
		location.AppendLine($"\tdefault_type {resource.MimeType};");
		location.AppendLine("}");
		return location.ToString();
	}

	private KeyValuePair<string, LocalNotionHelper.WebHostableResource>[] CalculateProtectedPhysicalAliases(
		LocalNotionHelper.WebHostableResource[] resources, LocalNotionHelper.WebHostableResource[] additionalTargets
	) {
		var cmsPath = LocalNotionRepository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Relative);
		var mappedRouteCount = resources.Length + 1;
		resources = resources.Append(new LocalNotionHelper.WebHostableResource("sitemap.xml", Path.Combine(cmsPath, "sitemap.xml"), "application/xml", null))
			.Concat(additionalTargets).ToArray();
		var aliases = new Dictionary<string, LocalNotionHelper.WebHostableResource>(StringComparer.Ordinal);
		var repoPath = LocalNotionRepository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		foreach (var resource in resources.Where(resource => !string.IsNullOrWhiteSpace(resource.Authentication)))
			if (resource.RelPath.Any(character => char.IsControl(character) || character is '$' or '"'))
				throw new InvalidOperationException("A protected hosting target contains unsupported Nginx path syntax.");
		var targets = resources.Select(resource => {
			var fullPath = Path.GetFullPath(resource.RelPath.ToUnixPath(), repoPath);
			return Path.GetRelativePath(repoPath, fullPath).ToUnixPath();
		}).ToArray();
		for (var i = 0; i < resources.Length; i++) {
			var resource = resources[i];
			if (string.IsNullOrWhiteSpace(resource.Authentication))
				continue;
			var target = targets[i];
			if (target is "." or ".." || target.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(target))
				throw new InvalidOperationException("A protected hosting target cannot be represented safely beneath the repository root.");
			if (OperatingSystem.IsWindows() && File.Exists(Path.Combine(repoPath, target)) && !HasExactFileSystemCase(repoPath, target))
				throw new InvalidOperationException("A protected hosting target's case does not match its existing file.");

			var physicalUrl = "/" + target;
			var alreadyMapped = false;
			for (var j = 0; j < resources.Length; j++) {
				var other = resources[j];
				// Nginx normalizes incoming URIs, not the literal configured location. Do not URL-decode filenames.
				// Additional listing files carry their route's policy but are not themselves friendly routes.
				var isPhysicalLocation = j < mappedRouteCount && "/" + other.Slug == physicalUrl;
				if (targets[j] != target && !isPhysicalLocation)
					continue;
				if (targets[j] != target || other.Authentication != resource.Authentication || other.MimeType != resource.MimeType)
					throw new InvalidOperationException("A protected hosting target conflicts with another route's target or authentication policy.");
				alreadyMapped |= isPhysicalLocation;
			}
			// Targets sharing one file were checked above to carry the same policy, so the first alias stands for them all.
			if (!alreadyMapped && !aliases.ContainsKey(physicalUrl))
				aliases.Add(physicalUrl, resource);
		}
		return aliases.ToArray();
	}

	private static bool HasExactFileSystemCase(string root, string relativePath) {
		var current = root;
		foreach (var part in relativePath.Split('/')) {
			var entry = Directory.EnumerateFileSystemEntries(current).FirstOrDefault(entry => Path.GetFileName(entry) == part);
			if (entry is null)
				return false;
			current = entry;
		}
		return true;
	}

	private async Task GenerateRedirectsFile(
		LocalNotionHelper.WebHostableResource[] resources,
		Dictionary<string, string[]> listingRenderPaths,
		KeyValuePair<string, LocalNotionHelper.WebHostableResource>[] protectedPhysicalAliases
	) {
		// A redirect never replaces a location ln-urls.conf declares, and it only leads to a route that is served.
		var routes = resources.Select(resource => resource.Slug).ToHashSet(StringComparer.Ordinal);
		var locations = routes.Select(slug => "/" + slug)
			.Concat(listingRenderPaths.Keys.Where(slug => slug.Length > 0 && !slug.EndsWith('/')).Select(slug => "/" + slug + "/"))
			.Concat(protectedPhysicalAliases.Select(alias => alias.Key))
			.Append("/sitemap.xml")
			.ToHashSet(StringComparer.Ordinal);
		var redirectsFilePath = Path.Combine(CalculateNGinxFolderPath(), "conf", RedirectsFileName);
		await using var writer = new FileTextWriter(redirectsFilePath, FileMode.Create);
		await writer.WriteLineAsync(RedirectsFileHeader);
		foreach (var redirect in LocalNotionRepository.Redirects.OrderBy(redirect => redirect.From, StringComparer.Ordinal)) {
			if (!routes.Contains(redirect.To) || locations.Contains("/" + redirect.From))
				continue;
			if ((redirect.From + redirect.To).Any(character => character == '$' || char.IsControl(character))) {
				LocalNotionRepository.Logger.Warning($"Skipping redirect '{redirect.From}': its addresses contain characters Nginx cannot match literally.");
				continue;
			}
			var status = redirect.Type == RedirectType.Soft ? 302 : 301;
			// Location headers carry an encoded address; the matched location keeps the decoded one Nginx compares.
			// A relative Location keeps the visitor's scheme and host behind a proxy, instead of the origin's http address.
			var destination = "/" + string.Join("/", redirect.To.Split('/').Select(Uri.EscapeDataString)) + "$is_args$args";
			await writer.WriteLineAsync($"location = {QuoteNginxLiteral("/" + redirect.From)} {{ absolute_redirect off; return {status} {QuoteNginxLiteral(destination)}; }}");
		}
	}

	private static string QuoteNginxLiteral(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

}
