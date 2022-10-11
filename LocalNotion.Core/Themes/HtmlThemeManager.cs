using Hydrogen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using LocalNotion.Extensions;

namespace LocalNotion.Core;

public class HtmlThemeManager : IThemeManager {

	public HtmlThemeManager(IPathResolver pathResolver, ILogger logger = null) {
		Guard.ArgumentNotNull(pathResolver, nameof(pathResolver));
		PathResolver = pathResolver;
		Logger = logger;
	}

	public IPathResolver PathResolver { get; }

	private ILogger Logger { get; }

	public bool TryLoadTheme(string theme, out ThemeInfo themeInfo) {
		var fetched = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
		return TryGetTemplateInfoInternal(theme, out themeInfo, fetched);


		bool TryGetTemplateInfoInternal(string theme, out ThemeInfo themeInfo, ISet<string> alreadyFetched) {
			themeInfo = null;

			if (alreadyFetched.Contains(theme))
				return false;

			var templatePath = PathResolver.GetThemePath(theme, FileSystemPathType.Absolute);

			var templateInfoPath = Path.Combine(templatePath, Constants.ThemeInfoFileName);
			if (!File.Exists(templateInfoPath)) {
				themeInfo = null;
				return false;
			}
			var htmlTemplateInfo = Tools.Json.ReadFromFile<HtmlThemeInfo>(templateInfoPath);
			htmlTemplateInfo.TemplatePath = templatePath;
			themeInfo = htmlTemplateInfo;
			alreadyFetched.Add(theme);

			// Setup all the theme tokens for the files
			foreach (var templateFile in Tools.FileSystem.GetFiles(templatePath, recursive: true).Select(x => x.ToUnixPath())) {
				var templateFileLocalPath =  Path.Combine(templatePath, templateFile).ToUnixPath();
				var templateFileRemotePath = $"{htmlTemplateInfo.OnlineUrl.TrimEnd("/")}/{templateFile}".ToUnixPath();
				var fileContents = Tools.Values.Future.AlwaysLoad(() => File.ReadAllText(templateFileLocalPath));

				// Add the "url" token for each file
				htmlTemplateInfo.Tokens.Add($"theme://{templateFile}", new HtmlThemeInfo.Token { Local = templateFileLocalPath, Remote = templateFileRemotePath });

				// Include token is a future to the file contents (loaded every time)
				htmlTemplateInfo.Tokens.Add($"include://{templateFile}", new HtmlThemeInfo.Token { Local = fileContents, Remote = fileContents });
			}

			// LoadAsync base theme if applicable
			if (!string.IsNullOrWhiteSpace(htmlTemplateInfo.Base)) {
				 if (!TryGetTemplateInfoInternal(htmlTemplateInfo.Base, out var baseTemplateInfo, alreadyFetched))
					return false;
				 htmlTemplateInfo.BaseTheme = (HtmlThemeInfo)baseTemplateInfo;
			}
			return true;
		}
	}

	public static void ExtractEmbeddedThemes(string folder, bool overwrite, ILogger logger) {
		Guard.ArgumentNotNull(folder, nameof(folder));
		new ManifestEmbeddedFileProvider(Assembly.GetExecutingAssembly())
			.ExtractDirectory("/Themes", folder, overwrite, logger);
	}

}
