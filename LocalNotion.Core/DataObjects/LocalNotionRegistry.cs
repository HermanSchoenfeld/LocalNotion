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
using Newtonsoft.Json.Linq;

namespace LocalNotion.Core;

public class LocalNotionRegistry {
	private IList<LocalNotionResource> _resources = new List<LocalNotionResource>();
	private IDictionary<string, CMSItem> _cmsRenders = new Dictionary<string, CMSItem>();
	private IDictionary<string, LocalNotionRedirect> _redirects = new Dictionary<string, LocalNotionRedirect>(StringComparer.Ordinal);

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

	[JsonProperty("git", NullValueHandling = NullValueHandling.Ignore, ObjectCreationHandling = ObjectCreationHandling.Replace)]
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

	[JsonProperty("redirects")]
	public LocalNotionRedirect[] Redirects {
		get => _redirects.Values.ToArray();
		set => _redirects = (value ?? []).ToDictionary(x => x.From, StringComparer.Ordinal);
	}

	[JsonExtensionData]
	public IDictionary<string, JToken> AdditionalData { get; set; }


	[JsonIgnore]
	internal IDictionary<string, CMSItem> CMSItemsBySlug => _cmsRenders;

	[JsonIgnore]
	internal IDictionary<string, LocalNotionRedirect> RedirectsByFrom => _redirects;

	public void Add(LocalNotionResource resource) {
		_resources.Add(resource);
	}

	public void Remove(LocalNotionResource resource) {
		_resources.Remove(resource);
	}

	public void Add(CMSItem item) {
		_cmsRenders[item.Slug] = item;
	}

	// A registry without redirects keeps its existing shape.
	public bool ShouldSerializeRedirects() => _redirects.Count > 0;

	public static LocalNotionRegistry Load(string registryFile) {
		var registry = JsonConvert.DeserializeObject<LocalNotionRegistry>(File.ReadAllText(registryFile));
		if (registry is null)
			throw new JsonSerializationException("The registry must contain a JSON object.");
		return registry;
	}

	public static bool IsForCms(string registryFile) => Load(registryFile).CMSDatabase is not null;
}
