// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

public class LocalNotionRegistry {
	private IList<LocalNotionResource> _resources = new List<LocalNotionResource>();
	private IDictionary<string, CMSItem> _cmsRenders = new Dictionary<string, CMSItem>();

	[JsonProperty("version")]
	public int Version { get; set; } = 1;

	[JsonProperty("notion_api_key", NullValueHandling = NullValueHandling.Ignore)]
	public string NotionApiKey { get; set; } = null;

	[JsonProperty("default_themes")]
	public string[] DefaultThemes { get; set; } = { "default" };

	[JsonProperty("paths")]
	public LocalNotionPathProfile Paths { get; set; } = LocalNotionPathProfile.Backup;

	[JsonProperty("cms_database")]
	public string CMSDatabase { get; set; } = null;

	//[JsonProperty("cms_primary_datasource")]
	//public string CMSPrimaryDataSource { get; set; } = null;

	[JsonProperty("git", NullValueHandling = NullValueHandling.Ignore)]
	public GitSettings GitSettings { get; set; } = GitSettings.Default;

	[JsonProperty("nginx", NullValueHandling = NullValueHandling.Ignore)]
	public NGinxSettings NGinxSettings { get; set; } = NGinxSettings.Default;

	[JsonProperty("apache", NullValueHandling = NullValueHandling.Ignore)]
	public ApacheSettings ApacheSettings { get; set; } = ApacheSettings.Default;

	[JsonProperty("log_level")]
	[JsonConverter(typeof(StringEnumConverter))]
	public LogLevel LogLevel { get; set; }

	[JsonProperty("resources")]
	public LocalNotionResource[] Resources {
		get => _resources.ToArray();
		set => _resources = (value ?? Array.Empty<LocalNotionResource>()).ToList();
	}

	[JsonProperty("cms_items")]
	public CMSItem[] CMSItems {
		get => _cmsRenders.Values.ToArray();
		set => _cmsRenders = value?.ToDictionary(x => x.Slug);
	}


	[JsonIgnore]
	internal IDictionary<string, CMSItem> CMSItemsBySlug => _cmsRenders;

	public void Add(LocalNotionResource resource) {
		_resources.Add(resource);
	}

	public void Remove(LocalNotionResource resource) {
		_resources.Remove(resource);
	}

	public void Add(CMSItem item) {
		_cmsRenders[item.Slug] = item;
	}

	public static bool IsForCms(string registryFile) {
		var registry = JsonConvert.DeserializeObject<LocalNotionRegistry>(File.ReadAllText(registryFile));
		return registry.CMSDatabase is not null;
	}
}