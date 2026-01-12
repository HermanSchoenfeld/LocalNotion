// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

namespace LocalNotion.Core;

#pragma warning disable CS8618
public class DownloadOptions {

	public bool Render { get; init; } = true;

	public RenderType RenderType { get; init; } = RenderType.HTML;

	public RenderMode RenderMode { get; init; } = RenderMode.ReadOnly;

	public bool FaultTolerant { get; init; } = true;

	public bool ForceRefresh { get; init; } = false;

	public static DownloadOptions Default { get; } = new();

	public static DownloadOptions WithoutRender(DownloadOptions options)
		=> new () {
			Render = false,
			RenderType = options.RenderType,
			RenderMode = options.RenderMode,
			FaultTolerant = options.FaultTolerant,
			ForceRefresh = options.ForceRefresh
		};
}
