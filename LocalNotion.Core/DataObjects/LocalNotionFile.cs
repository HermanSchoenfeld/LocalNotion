// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Newtonsoft.Json;

namespace LocalNotion.Core;

public class LocalNotionFile : LocalNotionResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.File;

	[JsonProperty("mimetype")]
	public string MimeType { get; set; }

	public static bool TryParse(string resourceID, string filename, string parentResourceID, string mimeType, out LocalNotionFile localNotionFile) {
		localNotionFile = new() {
			ID = resourceID,
			LastSyncedOn = DateTime.UtcNow,
			MimeType = mimeType ?? (Tools.Network.TryGetMimeType(filename, out var mt) ? mt : "application/octet-stream"),
			Title = filename,
			ParentResourceID = parentResourceID
		};
		return true;
	}

	public static LocalNotionFile Parse(string resourceID, string filename, string parentResourceID, string mimeType) 
		=> TryParse(resourceID, filename, parentResourceID, mimeType, out var LocalNotionFile) ? LocalNotionFile : throw new FormatException($"Unable to parse {nameof(LocalNotionFile)}");
}
