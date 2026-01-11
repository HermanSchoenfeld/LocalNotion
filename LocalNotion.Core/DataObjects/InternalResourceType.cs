// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum InternalResourceType {
	[EnumMember(Value = "object")]
	Objects,

	[EnumMember(Value = "graph")]
	Graphs,

	[EnumMember(Value = "themes")]
	Themes,

	[EnumMember(Value = "logs")]
	Logs
}
