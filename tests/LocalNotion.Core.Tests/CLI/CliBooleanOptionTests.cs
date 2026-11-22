// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using CommandLine;
using LocalNotion.CLI;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class CliBooleanOptionTests {

	[TestCase("pull")]
	[TestCase("sync")]
	[TestCase("render")]
	public void OmittedOptionsPreserveEnabledDefaults(string verb) {
		var result = Parse(verb, "--all");
		Assert.That(result.Tag, Is.EqualTo(ParserResultType.Parsed));
		Assert.That(GetBoolean(result.Value, "fault-tolerant"), Is.True);
		if (verb != "render")
			Assert.That(GetBoolean(result.Value, "render"), Is.True);
	}

	[TestCase("pull", "render")]
	[TestCase("pull", "fault-tolerant")]
	[TestCase("sync", "render")]
	[TestCase("sync", "fault-tolerant")]
	[TestCase("render", "fault-tolerant")]
	public void BareOptionsRemainEnabled(string verb, string option) {
		var result = Parse(verb, "--all", "--" + option);
		Assert.That(result.Tag, Is.EqualTo(ParserResultType.Parsed));
		Assert.That(GetBoolean(result.Value, option), Is.True);
	}

	[TestCaseSource(nameof(ExplicitBooleanCases))]
	public void ExplicitBooleanValuesAreHonored(string verb, string option, bool enabled, bool inline) {
		var value = enabled.ToString().ToLowerInvariant();
		var result = Parse(inline ? [verb, "--all", "--" + option + "=" + value] : [verb, "--all", "--" + option, value]);
		Assert.That(result.Tag, Is.EqualTo(ParserResultType.Parsed));
		Assert.That(GetBoolean(result.Value, option), Is.EqualTo(enabled));
	}

	[TestCaseSource(nameof(InvalidBooleanCases))]
	public void InvalidBooleanValuesAreRejected(string verb, string option, bool inline) {
		var result = Parse(inline ? [verb, "--all", "--" + option + "=maybe"] : [verb, "--all", "--" + option, "maybe"]);
		Assert.That(result.Tag, Is.EqualTo(ParserResultType.NotParsed));
	}

	[TestCase("pull")]
	[TestCase("sync")]
	public void BothFalseFlagsAreHonoredTogetherWithAnObjectList(string verb) {
		var result = Parse(verb, "--objects", "b237ceef-1796-40ec-ac03-ad71cbe954c6", "--render", "false", "--fault-tolerant", "false");
		Assert.That(result.Tag, Is.EqualTo(ParserResultType.Parsed));
		Assert.That(GetBoolean(result.Value, "render"), Is.False);
		Assert.That(GetBoolean(result.Value, "fault-tolerant"), Is.False);
	}

	[TestCase("pull")]
	[TestCase("sync")]
	public void AdjacentBareOptionsRemainEnabled(string verb) {
		var result = Parse(verb, "--all", "--render", "--fault-tolerant", "--force");
		Assert.That(result.Tag, Is.EqualTo(ParserResultType.Parsed));
		Assert.That(GetBoolean(result.Value, "render"), Is.True);
		Assert.That(GetBoolean(result.Value, "fault-tolerant"), Is.True);
		Assert.That(((Program.PullRepositoryCommandArguments)result.Value).Force, Is.True);
	}

	[Test]
	public void ConfiguredOptionTerminatorPreservesTheParsersValueHandling() {
		using var parser = new Parser(settings => {
			settings.HelpWriter = null;
			settings.EnableDashDash = true;
		});
		var result = Program.ParseCommandLineArguments(["pull", "--all", "--", "--render", "false", "--fault-tolerant", "false"], parser);
		Assert.That(result.Tag, Is.EqualTo(ParserResultType.Parsed));
		Assert.That(GetBoolean(result.Value, "render"), Is.True);
		Assert.That(GetBoolean(result.Value, "fault-tolerant"), Is.True);
	}

	private static IEnumerable<TestCaseData> ExplicitBooleanCases() {
		foreach (var (verb, option) in BooleanOptions())
			foreach (var enabled in new[] { false, true })
				foreach (var inline in new[] { false, true })
					yield return new TestCaseData(verb, option, enabled, inline);
	}

	private static IEnumerable<TestCaseData> InvalidBooleanCases() {
		foreach (var (verb, option) in BooleanOptions())
			foreach (var inline in new[] { false, true })
				yield return new TestCaseData(verb, option, inline);
	}

	private static (string Verb, string Option)[] BooleanOptions()
		=> [("pull", "render"), ("pull", "fault-tolerant"), ("sync", "render"), ("sync", "fault-tolerant"), ("render", "fault-tolerant")];

	private static ParserResult<object> Parse(params string[] arguments) {
		using var parser = new Parser(settings => settings.HelpWriter = null);
		return Program.ParseCommandLineArguments(arguments, parser);
	}

	private static bool GetBoolean(object arguments, string option)
		=> arguments switch {
			Program.PullRepositoryCommandArguments pull => option == "render" ? pull.Render : pull.FaultTolerant,
			Program.RenderCommandArguments render => render.FaultTolerant,
			_ => throw new ArgumentException("Expected pull, sync or render arguments.", nameof(arguments))
		};
}
