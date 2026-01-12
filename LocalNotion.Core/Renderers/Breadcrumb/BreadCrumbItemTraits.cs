// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

namespace LocalNotion.Core;

[Flags]
public enum BreadCrumbItemTraits : uint {
	IsCurrentPage = 1 << 0,
	IsPage = 1 << 1,
	IsCMSPage = 1 << 2,
	IsFile = 1 << 3,
	IsDatabase = 1 << 4,
	IsCategory = 1 << 5,
	IsRoot = 1 << 6,
	IsWorkspace = 1 << 7,
	HasUrl = 1 << 8,
	HasIcon = 1 << 9,
	HasEmojiIcon = 1 << 10,
	HasImageIcon = 1 << 11
}
