using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Sphere10.Framework;
using Sphere10.Framework.Data;
using JsonSubTypes;
using Newtonsoft.Json;

namespace LocalNotion.Core;

[Flags]
[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum HtmlThemeTraits {

	[EnumMember(Value = "partial_page")]
	PartialPage,

	[EnumMember(Value = "suppress_formatting")]
	SuppressFormatting 
}

public class HtmlThemeInfo : ThemeInfo {

	public override ThemeType Type => ThemeType.Html;
	private string _onlineUrl = string.Empty;
	private HtmlThemeTraits _traits = 0;
	
	[JsonProperty("base")]
	public string Base { get; set; }

	[JsonIgnore]
	public HtmlThemeInfo BaseTheme { get; set; } = null;

	[JsonProperty("online_url", NullValueHandling = NullValueHandling.Ignore)]
	public string OnlineUrl { 
		get => !string.IsNullOrEmpty(_onlineUrl) ? _onlineUrl : (BaseTheme?.OnlineUrl ?? string.Empty);
		set => _onlineUrl = value;
	}

	[JsonProperty("traits", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public HtmlThemeTraits Traits { 
		get => _traits | BaseTheme?.Traits ?? 0;
		set => _traits = value;
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
