// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

namespace LocalNotion.Core;

/// <summary>
/// How a <see cref="LocalNotionRedirect"/> answers requests for its old address.
/// </summary>
public enum RedirectType {

	/// <summary>
	/// A permanent move (HTTP 301). Search engines transfer the old address's standing to the new one.
	/// Local Notion records every address change it makes as a hard redirect.
	/// </summary>
	Hard,

	/// <summary>
	/// A temporary move (HTTP 302). The old address stays indexed, so a soft redirect suits a move that may be undone.
	/// </summary>
	Soft
}
