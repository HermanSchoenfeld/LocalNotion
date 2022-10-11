using System.Text.RegularExpressions;
using Hydrogen;
using Hydrogen.Data;
using JsonSubTypes;
using Newtonsoft.Json;

namespace LocalNotion.Core;

public class HtmlThemeInfo : ThemeInfo {

	public override ThemeType Type => ThemeType.Html;
	private string _onlineurl = string.Empty;
	private bool _suppressFormatting = false;

	[JsonProperty("base")]
	public string Base { get; set; }

	[JsonIgnore]
	public HtmlThemeInfo BaseTheme { get; set; } = null;


	[JsonProperty("online_url", NullValueHandling = NullValueHandling.Ignore)]
	public string OnlineUrl { 
		get => !string.IsNullOrEmpty(_onlineurl) ? _onlineurl : (BaseTheme?.OnlineUrl ?? string.Empty);
		set => _onlineurl = value;
	}

	[JsonProperty("SuppressFormatting", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public bool SuppressFormatting { 
		get => _suppressFormatting || (BaseTheme?.SuppressFormatting ?? false);
		set => _suppressFormatting = value;
	}
	
	// The tokens below get filled with users custom tokens and all template files (key is "/folder/file.txt" value is fully resolved absolute path)
	[JsonProperty("tokens")]  
	public IDictionary<string, Token> Tokens { get; set; } = new Dictionary<string, Token>();

	
	public class Token {

		[JsonProperty("offline")]
		public object Local { get; set; } = string.Empty;

		[JsonProperty("online")]
		public object Remote { get; set; } = string.Empty;
	}
		
}
