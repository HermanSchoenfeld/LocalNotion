using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;

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
