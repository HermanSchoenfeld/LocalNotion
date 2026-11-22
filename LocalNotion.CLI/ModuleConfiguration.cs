// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Sphere10.Framework;
using Sphere10.Framework.Application;

namespace LocalNotion.CLI;


public class ModuleConfiguration : Sphere10.Framework.Application.ModuleConfiguration {

	public override int Priority => int.MinValue; // last to execute

	public override void RegisterComponents(IServiceCollection serviceCollection) {
		base.RegisterComponents(serviceCollection);
		// LocalNotion does not track application usage or store encrypted quota data.
		// Keep the standard lifecycle/settings services without requiring an application secret.
		foreach (var registration in serviceCollection.Where(x =>
			x.ServiceType == typeof(IProductUsageServices) ||
			x.ImplementationType == typeof(StandardHelpServices) ||
			x.ImplementationType == typeof(ProductUsageInformationTokenResolver) ||
			x.ImplementationType == typeof(IncrementUsageByOneInitializer)).ToArray())
			serviceCollection.Remove(registration);

		if (!serviceCollection.HasImplementationFor<IUserInterfaceServices>())
			serviceCollection.AddSingleton<IUserInterfaceServices, ConsoleApplicationUserInterfaceServices>();

	}

}
