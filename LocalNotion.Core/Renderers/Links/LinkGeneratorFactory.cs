// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Diagnostics;
using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public static class LinkGeneratorFactory {

	public static ILinkGenerator Create(ILocalNotionRepository repository) 
		=> Create(repository, repository.Paths.Mode);

	public static ILinkGenerator Create(ILocalNotionRepository repository, LocalNotionMode mode) 
		=> mode switch {
			LocalNotionMode.Online => new OnlineLinkGenerator(repository),
			LocalNotionMode.Offline => new OfflineLinkGenerator(repository),
			_ => throw new NotSupportedException(mode.ToString())
		};
}