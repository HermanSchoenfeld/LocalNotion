// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

namespace LocalNotion.Core;

public class BreadCrumbItem {

	public LocalNotionResourceType Type { get; set; }

	public BreadCrumbItemTraits Traits { get; set; }

	public string Data { get; set; }

	public string Text { get; set; }

	public string Url { get; set; }
}
