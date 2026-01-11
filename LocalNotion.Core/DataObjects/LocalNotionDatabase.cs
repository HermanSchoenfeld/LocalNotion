// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionDatabase : LocalNotionEditableResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.Database;

	[JsonProperty("primary_datasource_id")]
	public string PrimaryDataSourceID { get; set; }

	[JsonProperty("description")]
	public string Description { get; set; }

	[JsonProperty("properties")]
	public IDictionary<string, DataSourcePropertyConfig> Properties { get; set; }
	
}