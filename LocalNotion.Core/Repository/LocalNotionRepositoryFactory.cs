// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sphere10.Framework;
using LocalNotion.Core;

namespace LocalNotion {
	public class LocalNotionRepositoryFactory {

		private static readonly SynchronizedDictionary<string, ILocalNotionRepository> _registeredRepositories;

		static LocalNotionRepositoryFactory() {
			_registeredRepositories = new SynchronizedDictionary<string, ILocalNotionRepository>();
		}

		public static ILocalNotionRepository Get(string registryFilename) {
			if (_registeredRepositories.TryGetValue(registryFilename, out var repo))
				return repo;
			using (_registeredRepositories.EnterWriteScope()) {
				if (_registeredRepositories.TryGetValue(registryFilename, out repo))
					return repo;

				_registeredRepositories[registryFilename] = LocalNotionRepository.Open(registryFilename, SystemLog.Logger).ResultSafe();
				return _registeredRepositories[registryFilename];
			}
		}
	}
}
