using Hydrogen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using LocalNotion.Extensions;
using Notion.Client;
using System.Runtime.Serialization;

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
			htmlTemplateInfo.FilePath = templatePath;
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


	public DictionaryChain<string, object> LoadThemeTokens(HtmlThemeInfo[] themes, string pageID, LocalNotionMode localNotionMode, RenderMode renderMode, out bool suppressFormatting) {
		Guard.ArgumentNotNull(themes, nameof(themes));
		DictionaryChain<string, object> tokens = null;


		var htmlThemes = themes.Cast<HtmlThemeInfo>().ToArray();

		suppressFormatting = htmlThemes.Any(t => t.Traits.HasFlag(HtmlThemeTraits.SuppressFormatting));

		// Generate all the theme tokens
		foreach(var theme in htmlThemes)
			tokens = tokens == null ?
				GetModeCorrectedTokens(theme) 
				: tokens.AttachHead( GetModeCorrectedTokens(theme) );

		// Attach rendering-specific tokens
		tokens = tokens.AttachHead(new Dictionary<string, object> { ["render_mode"] = renderMode.GetAttribute<EnumMemberAttribute>().Value });

		return tokens;

		DictionaryChain<string, object> GetModeCorrectedTokens(HtmlThemeInfo theme) {
			// This method provides a "chain of responsibility" style dictionary of all the theme tokens
			// Also it resolves a theme:// tokens which are aliases for links to a theme resource.
			var dictionary = new DictionaryChain<string, object>(
				theme.Tokens.ToDictionary(x => x.Key, x => localNotionMode switch { LocalNotionMode.Offline => ToLocalPathIfApplicable(x.Key, x.Value.Local), LocalNotionMode.Online => x.Value.Remote, _ => throw new NotSupportedException(localNotionMode.ToString())}),
				theme.BaseTheme != null ? GetModeCorrectedTokens(theme.BaseTheme) : null
			);

			object ToLocalPathIfApplicable(string key, object value) {
				if (key.StartsWith("theme://")) {
					Guard.Ensure(value != null, $"Unexpected null value for key '{key}'");
					var thisRendersExpectedParentFolder = PathResolver.GetResourceFolderPath(LocalNotionResourceType.Page, pageID, FileSystemPathType.Absolute);
					return Path.GetRelativePath(thisRendersExpectedParentFolder, value.ToString()).ToUnixPath();
				}
				return value;
			}
			return dictionary;
		}

	}

	public static void ExtractEmbeddedThemes(string folder, bool overwrite, ILogger logger) {
		Guard.ArgumentNotNull(folder, nameof(folder));
		new ManifestEmbeddedFileProvider(Assembly.GetExecutingAssembly())
			.ExtractDirectory("/Themes", folder, overwrite, logger);
	}

}
