using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sphere10.Framework;
using Microsoft.Extensions.FileProviders;

namespace LocalNotion.Extensions {
	public static class IFileProviderExtensions {

		public static void ExtractDirectory(this IFileProvider provider, string fromDirectory, string toRoot, bool overwrite, ILogger logger = null) {
			logger ??= new NoOpLogger();
			foreach(var item in provider.GetDirectoryContents(fromDirectory)) {
				var sourcePath = Path.Combine(fromDirectory, item.Name).ToUnixPath();
				var destPath = Path.Combine(toRoot, item.Name);
				if (item.IsDirectory) {
					if (!Directory.Exists(destPath)) 
						Tools.FileSystem.CreateDirectory(destPath);
					provider.ExtractDirectory(sourcePath, Path.Combine(toRoot, destPath), overwrite, logger);
				} else {
					if (overwrite || !File.Exists(destPath)) {
						logger.Debug($"Extracting `{sourcePath}` -> {destPath}");
						using var stream = item.CreateReadStream();
						stream.WriteToFile(destPath, FileMode.Create);
					}
				}
			}
		}
	}
}
