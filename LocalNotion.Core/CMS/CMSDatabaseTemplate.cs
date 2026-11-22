// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Notion.Client;
using Sphere10.Framework;

namespace LocalNotion.Core;

/// <summary>
/// The portable, schema-only CMS template based on staging.sphere10.com, with supported behavior and menu presets.
/// No content, credentials, source database IDs or property IDs are included.
/// </summary>
public sealed class CMSDatabaseTemplate {

	public DatabasesCreateRequest CreateRequest(Guid parentPageID, string title) {
		Guard.Argument(parentPageID != Guid.Empty, nameof(parentPageID), "A non-empty parent page ID is required.");
		Guard.ArgumentNotNullOrWhitespace(title, nameof(title));
		Guard.ArgumentLTE(title.Length, 2000, nameof(title), "The database title must contain no more than 2000 characters.");
		// Notion caps schema option arrays at 100; the creator seeds the remaining presets through a temporary page.
		var properties = CreateProperties();
		foreach (var property in properties.Values.Where(x => x.Type == "multi_select")) {
			var configuration = (JObject)property.AdditionalData["multi_select"];
			configuration["options"] = new JArray(((JArray)configuration["options"]).Take(100));
		}

		return new DatabasesCreateRequest {
			Parent = new PageParentOfDatabaseRequest { PageId = parentPageID.ToString("D") },
			Title = new List<RichTextBaseInput> { new RichTextTextInput { Text = new Text { Content = title } } },
			IsInline = false,
			InitialDataSource = new InitialDataSourceRequest { Properties = properties }
		};
	}

	public Dictionary<string, DataSourcePropertyConfigRequest> CreateProperties() {
		// Read on demand so non-CMS commands do not initialize or retain the template.
		using var stream = typeof(CMSDatabaseTemplate).Assembly.GetManifestResourceStream("LocalNotion.Core.CMS.BlankCMSDatabase.json");
		Guard.Ensure(stream != null, "The embedded CMS database template is missing.");
		using var reader = new StreamReader(stream);
		var properties = JsonConvert.DeserializeObject<Dictionary<string, DataSourcePropertyConfigRequest>>(reader.ReadToEnd());
		Guard.Ensure(properties != null && properties.Count == 21, "The embedded CMS database template is invalid.");
		return properties;
	}

	public Dictionary<string, PropertyValue>[] CreateOverflowBatches() {
		var batches = new List<Dictionary<string, PropertyValue>>();
		foreach (var property in CreateProperties().Where(x => x.Value.Type == "multi_select")) {
			var configuration = (JObject)property.Value.AdditionalData["multi_select"];
			var options = ((JArray)configuration["options"]).ToObject<List<SelectOption>>();
			for (var offset = 100; offset < options.Count; offset += 100)
				batches.Add(new Dictionary<string, PropertyValue> {
					[property.Key] = new MultiSelectPropertyValue { MultiSelect = options.Skip(offset).Take(100).ToList() }
				});
		}
		return batches.ToArray();
	}

	public void Validate(DataSource dataSource) {
		var expected = CreateProperties();
		Guard.Ensure(dataSource?.Properties != null && dataSource.Properties.Count == expected.Count, "The new CMS property count did not match the template.");
		foreach (var property in expected) {
			Guard.Ensure(dataSource.Properties.TryGetValue(property.Key, out var actual) && actual.Type == property.Value.Type,
				"The new CMS property types did not match the template.");
			if (property.Value.Type == "number") {
				var format = (string)((JObject)property.Value.AdditionalData["number"])["format"];
				Guard.Ensure(actual is NumberDataSourcePropertyConfig number && number.Number?.Format == format, "The new CMS number format did not match the template.");
			}
			if (property.Value.Type is not ("select" or "multi_select"))
				continue;

			var expectedOptions = (JArray)((JObject)property.Value.AdditionalData[property.Value.Type])["options"];
			var actualOptions = actual switch {
				SelectDataSourcePropertyConfig select => select.Select?.Options,
				MultiSelectDataSourcePropertyConfig multiSelect => multiSelect.MultiSelect?.Options,
				_ => null
			};
			Guard.Ensure(actualOptions != null && actualOptions.Count == expectedOptions.Count, "The new CMS preset option count did not match the template.");
			for (var index = 0; index < expectedOptions.Count; index++) {
				Guard.Ensure(actualOptions[index].Name == (string)expectedOptions[index]["name"]
					&& actualOptions[index].Color?.ToString().ToLowerInvariant() == (string)expectedOptions[index]["color"],
					"The new CMS preset option names, colors or order did not match the template.");
			}
		}
	}
}
