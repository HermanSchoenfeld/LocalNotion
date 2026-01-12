// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

public class CMSProperties {

	[JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
	[JsonConverter(typeof(StringEnumConverter))]
	public CMSPageType PageType { get; set; }

	[JsonProperty("publish_on", NullValueHandling = NullValueHandling.Ignore)]
	public DateTimeOffset? PublishOn { get; set; }

	[JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
	[JsonConverter(typeof(StringEnumConverter))]
	public CMSPageStatus Status { get; set; }

	[JsonProperty("themes", NullValueHandling = NullValueHandling.Ignore)]
	public string[] Themes { get; set; }

	[JsonProperty("custom_slug")]
	public string CustomSlug { get; set; }

	[JsonProperty("sequence", NullValueHandling = NullValueHandling.Ignore)]
	public int? Sequence { get; set; }

	[JsonProperty("root", NullValueHandling = NullValueHandling.Ignore)]
	public string Root { get; set; }

	[JsonProperty("category1", NullValueHandling = NullValueHandling.Ignore)]
	public string Category1 { get; set; }

	[JsonProperty("category2", NullValueHandling = NullValueHandling.Ignore)]
	public string Category2 { get; set; }

	[JsonProperty("category3", NullValueHandling = NullValueHandling.Ignore)]
	public string Category3 { get; set; }

	[JsonProperty("category4", NullValueHandling = NullValueHandling.Ignore)]
	public string Category4 { get; set; }

	[JsonProperty("category5", NullValueHandling = NullValueHandling.Ignore)]
	public string Category5 { get; set; }
	
	[JsonProperty("summary", NullValueHandling = NullValueHandling.Ignore)]
	public string Summary { get; set; }

	[JsonProperty("Tags", NullValueHandling = NullValueHandling.Ignore)]
	public string[] Tags { get; set; }

	[JsonIgnore]
	public IEnumerable<string> Categories =>
		PageType switch {
			CMSPageType.Gallery => [Root],
			_ => new [] { Root, Category1, Category2, Category3, Category4, Category5 }.TakeWhile(x => !string.IsNullOrWhiteSpace(x)).ToArray()
		};


	public string GetTipCategory() {
		if (!string.IsNullOrWhiteSpace(Category5))
			return Category5;
		if (!string.IsNullOrWhiteSpace(Category4))
			return Category4;
		if (!string.IsNullOrWhiteSpace(Category3))
			return Category3;
		if (!string.IsNullOrWhiteSpace(Category2))
			return Category2;
		if (!string.IsNullOrWhiteSpace(Category1))
			return Category1;
		return Root;
	}
}