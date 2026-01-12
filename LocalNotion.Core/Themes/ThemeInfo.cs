// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Notion.Client;

namespace LocalNotion.Core;


[JsonConverter(typeof(JsonSubtypes), "type")]
[JsonSubtypes.KnownSubType(typeof(HtmlThemeInfo), ThemeType.Html)]
public abstract class ThemeInfo {

	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public virtual ThemeType Type { get; set; }


	[JsonIgnore]
	public string FilePath { get; set; }

}