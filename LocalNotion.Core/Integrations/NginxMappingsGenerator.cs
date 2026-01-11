using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;
public class NginxMappingsGenerator(ILocalNotionRepository localNotionRepository) {

	public const string MappingFileName = "ln-urls.conf";

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

		# Catch-all rule to remove trailing slash
		location ~ (.+)/$ {
		    return 301 $1;
		}

		""";

	private const string LocationTemplate =
		"""
		location = {slug} {
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
		var mappingsFilePath = Path.Combine(CalculateNGinxFolderPath(), "conf", MappingFileName);
		await using var writer = new FileTextWriter(mappingsFilePath, FileMode.Create);
		await writer.WriteLineAsync(MappingsFileHeader);

		// Generate entries for hostable resources
		foreach (var hostableResource in LocalNotionHelper.EnumerateWebHostableResources(LocalNotionRepository)) {
			var location = LocationTemplate.FormatWithDictionary(
				new Dictionary<string, string> {
					["slug"] = "/" + hostableResource.Slug,
					["relPath"] = SanitizePath(hostableResource.RelPath),
					["mimeType"] = hostableResource.MimeType
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
		var allRequiredFiles = LocalNotionRepository.CMSItems.Select(x => x.RenderPath).Select(x => Path.Join(repoPath, x));
		allRequiredFiles = allRequiredFiles.Union(ExemptFiles.Select(x => Path.Combine(cmsPath, x)));

		var allActualFiles = Directory.EnumerateFiles(cmsPath);

		foreach ( var file in allActualFiles.Except(allRequiredFiles)) {
			File.Delete(file);
		}
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

}
