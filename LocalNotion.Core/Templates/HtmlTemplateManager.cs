using Hydrogen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using LocalNotion.Extensions;

namespace LocalNotion.Core;

public class HtmlTemplateManager : ITemplateManager {

	public HtmlTemplateManager(string templateFolder, ILogger logger = null) {
		Guard.ArgumentNotNull(templateFolder, nameof(templateFolder));
		Logger = logger ?? new NoOpLogger();
		if (!Directory.Exists(templateFolder)) {
			Directory.CreateDirectory(templateFolder);
			// Spit out the templates
		}
		TemplateDirectoryPath = templateFolder;
	
		ExtractEmbeddedTemplates(TemplateDirectoryPath, false, Logger);
	}

	public string TemplateDirectoryPath { get; }

	private ILogger Logger { get; }

	public string FetchTemplateFolderPath(string template) 
		=> Path.Combine(TemplateDirectoryPath, template) ;

	public bool TryLoadTemplate(string template, out TemplateInfo templateInfo) {
		var fetched = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
		return TryGetTemplateInfoInternal(template, out templateInfo, fetched);


		bool TryGetTemplateInfoInternal(string template, out TemplateInfo templateInfo, HashSet<string> alreadyFetched) {
			templateInfo = null;

			if (alreadyFetched.Contains(template))
				return false;

			var templatePath = FetchTemplateFolderPath(template);

			var templateInfoPath = Path.Combine(templatePath, Constants.TemplateInfoJsonFilename);
			if (!File.Exists(templateInfoPath)) {
				templateInfo = null;
				return false;
			}
			var htmlTemplateInfo = Tools.Json.ReadFromFile<HtmlTemplateInfo>(templateInfoPath);
			htmlTemplateInfo.TemplatePath = templatePath;
			templateInfo = htmlTemplateInfo;
			alreadyFetched.Add(template);

			// Setup all the template tokens for the files
			foreach (var templateFile in Tools.FileSystem.GetFiles(templatePath, recursive: true).Select(x => x.ToUnixPath())) {
				var templateFileLocalPath =  Path.Combine(templatePath, templateFile).ToUnixPath();
				var templateFileRemotePath = $"{htmlTemplateInfo.OnlineUrl.TrimEnd("/")}/{templateFile}".ToUnixPath();
				var fileContents = Tools.Values.Future.AlwaysLoad(() => File.ReadAllText(templateFileLocalPath));

				// Add the "url" token for each file
				htmlTemplateInfo.Tokens.Add($"template://{templateFile}", new HtmlTemplateInfo.Token { Local = templateFileLocalPath, Remote = templateFileRemotePath });

				// Include token is a future to the file contents (loaded every time)
				htmlTemplateInfo.Tokens.Add($"include://{templateFile}", new HtmlTemplateInfo.Token { Local = fileContents, Remote = fileContents });
			}

			// Load base template if applicable
			if (!string.IsNullOrWhiteSpace(htmlTemplateInfo.Base)) {
				 if (!TryGetTemplateInfoInternal(htmlTemplateInfo.Base, out var baseTemplateInfo, alreadyFetched))
					return false;
				 htmlTemplateInfo.BaseTemplate = (HtmlTemplateInfo)baseTemplateInfo;
			}
			return true;
		}
	}

	public static void ExtractEmbeddedTemplates(string folder, bool overwrite, ILogger logger) {
		Guard.ArgumentNotNull(folder, nameof(folder));
		new ManifestEmbeddedFileProvider(Assembly.GetExecutingAssembly())
			.ExtractDirectory("/Templates", folder, overwrite, logger);
	}

}
