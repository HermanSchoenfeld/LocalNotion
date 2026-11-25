// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Reflection;
using LocalNotion.CLI;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
public class CliMetadataTests {

	[Test]
	public void CompiledCopyrightPreservesCopyrightSymbol() {
		var copyright = typeof(GitSentry).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
		Assert.That(copyright, Is.Not.Null);
		Assert.That(copyright.Copyright, Does.Contain("\u00A9").And.Not.Contain("\uFFFD"));
	}

}
