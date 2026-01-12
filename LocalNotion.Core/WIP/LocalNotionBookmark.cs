// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Runtime.CompilerServices;
using Sphere10.Framework;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionBookmark {

	[JsonProperty("title")]
	public string Title { get; init; }

	[JsonProperty("summary", NullValueHandling = NullValueHandling.Ignore)]
	public string Summary { get; init; }

	[JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
	public string Url { get; init; }

	[JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
	public string ImageUrl { get; set; }

	[JsonProperty("thumbnail")]
	public LocalNotionThumbnail Thumbnail { get; set; } = LocalNotionThumbnail.None;
}