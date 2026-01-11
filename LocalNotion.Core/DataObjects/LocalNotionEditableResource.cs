// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public abstract class LocalNotionEditableResource : LocalNotionResource {

	[JsonProperty("cover", NullValueHandling = NullValueHandling.Ignore)]
	public string Cover { get; set; }

	[JsonProperty("thumbnail")]
	public LocalNotionThumbnail Thumbnail { get; set; } = LocalNotionThumbnail.None;

	[JsonProperty("feature", NullValueHandling = NullValueHandling.Ignore)]
	public string FeatureImageID { get; set; }

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string Name { get; set; }

	[JsonProperty("keywords", NullValueHandling = NullValueHandling.Ignore)]
	public string[] Keywords { get; set; }

	[JsonProperty("cms", NullValueHandling = NullValueHandling.Ignore)]
	public CMSProperties CMSProperties { get; set; } = null;

	[JsonProperty("created_on")]
	public DateTime CreatedOn { get; set; }

	[JsonProperty("last_edited_on")]
	public DateTime LastEditedOn { get; set; }

}
