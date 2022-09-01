using Hydrogen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using LocalNotion.Extensions;

namespace LocalNotion.Core;

public class HtmlThemeManager : IThemeManager {

	public HtmlThemeManager(string themeFolder, ILogger logger = null) {
		Guard.ArgumentNotNull(themeFolder, nameof(themeFolder));
		Logger = logger ?? new NoOpLogger();
		if (!Directory.Exists(themeFolder)) {
			Directory.CreateDirectory(themeFolder);
			// Spit out the templates
		}
		ThemeDirectoryPath = themeFolder;
	}

	public string ThemeDirectoryPath { get; }

	private ILogger Logger { get; }

	public string FetchTemplateFolderPath(string theme) 
		=> Path.Combine(ThemeDirectoryPath, theme) ;

	public bool TryLoadTemplate(string template, out ThemeInfo themeInfo) {
		var fetched = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
		return TryGetTemplateInfoInternal(template, out themeInfo, fetched);


		bool TryGetTemplateInfoInternal(string template, out ThemeInfo templateInfo, HashSet<string> alreadyFetched) {
			templateInfo = null;

			if (alreadyFetched.Contains(template))
				return false;

			var templatePath = FetchTemplateFolderPath(template);

			var templateInfoPath = Path.Combine(templatePath, Constants.TemplateInfoFilename);
			if (!File.Exists(templateInfoPath)) {
				templateInfo = null;
				return false;
			}
			var htmlTemplateInfo = Tools.Json.ReadFromFile<HtmlThemeInfo>(templateInfoPath);
			htmlTemplateInfo.TemplatePath = templatePath;
			templateInfo = htmlTemplateInfo;
			alreadyFetched.Add(template);

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

			// Load base theme if applicable
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
