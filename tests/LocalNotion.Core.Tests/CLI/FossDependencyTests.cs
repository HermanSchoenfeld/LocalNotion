// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LocalNotion.CLI;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sphere10.Framework;
using Sphere10.Framework.Application;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class FossDependencyTests {

	[Test]
	public void FrameworkReferencesUseTheApplicationBuildConfiguration() {
		var expected = typeof(LocalNotionRepository).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration;
		Assert.That(typeof(Guard).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration, Is.EqualTo(expected));
		Assert.That(typeof(ModuleConfigurationBase).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration, Is.EqualTo(expected));
	}

	[Test]
	public void CliCompositionKeepsSettingsWithoutEncryptedUsageTracking() {
		var services = new ServiceCollection();
		new LocalNotion.CLI.ModuleConfiguration().RegisterComponents(services);
		Assert.That(services.Any(x => x.ServiceType == typeof(ISettingsServices)), Is.True);
		Assert.That(services.Any(x => x.ServiceType == typeof(IProductUsageServices)), Is.False);
		Assert.That(services.Any(x => x.ImplementationType == typeof(ProductUsageInformationTokenResolver)), Is.False);
		Assert.That(services.Any(x => x.ImplementationType == typeof(IncrementUsageByOneInitializer)), Is.False);
		using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
	}

	[Test]
	public void RuntimeDependencyGraphContainsNoEnterpriseOrDrmImplementation() {
		var pending = new Queue<Assembly>();
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		pending.Enqueue(typeof(Program).Assembly);
		pending.Enqueue(typeof(LocalNotionRepository).Assembly);
		while (pending.TryDequeue(out var assembly)) {
			if (!visited.Add(assembly.FullName))
				continue;
			Assert.That(assembly.GetName().Name, Does.Not.Contain("Enterprise").IgnoreCase);
			Assert.That(assembly.GetName().Name, Does.Not.Contain("DRM").IgnoreCase);
			if (assembly.GetName().Name.StartsWith("Sphere10.", StringComparison.Ordinal)) {
				Assert.That(assembly.GetTypes().Select(x => x.FullName), Has.None.Contains(".DRM."), assembly.FullName);
			}
			foreach (var reference in assembly.GetReferencedAssemblies())
				pending.Enqueue(Assembly.Load(reference));
		}
	}

	[Test]
	public void ApplicationHasNoEmbeddedProductLicenseOrSharedSecret() {
		var attributes = typeof(Program).Assembly.GetCustomAttributesData().Select(x => x.AttributeType.Name).ToArray();
		Assert.That(attributes, Does.Not.Contain("AssemblyProductApiAttribute"));
		Assert.That(attributes, Does.Not.Contain("AssemblyProductLicenseAttribute"));
		Assert.That(attributes, Does.Not.Contain("AssemblyApplicationSecretAttribute"));
	}
}
