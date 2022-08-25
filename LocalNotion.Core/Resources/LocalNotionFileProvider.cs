using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace LocalNotion.Core;


//public abstract class FileSystemObjectListing {
//	public string Name { get; init; }

//	public string Path { get; init; }

//}

//public class FolderListing {

//	public FileSystemObjectListing[] Items { get; init; }

//	public IEnumerable<FolderListing> Folders => Items.Where(x => x is FolderListing).Cast<FolderListing>();

//	public IEnumerable<FileListing> Files => Items.Where(x => x is FileListing).Cast<FileListing>();
//}

//public class FileListing : FileSystemObjectListing  {

//}

//public class LocalNotionFileProvider : SynchronizedObject,  IFileProvider {
//	private readonly FileSystemWatcher _repoFolderWatcher;
//	private readonly string _localNotionRegistryFile;

//	public LocalNotionFileProvider(string localNotionRegistryFile) {
//		_localNotionRegistryFile = localNotionRegistryFile;
//		_repoFolderWatcher = new FileSystemWatcher(Path.GetDirectoryName(_localNotionRegistryFile));
//		_repoFolderWatcher.Filter = Path.GetFileName(_localNotionRegistryFile);
//		_repoFolderWatcher.Created += Refresh();
//		_repoFolderWatcher.Changed += Refresh();
//	}

//	private void Refresh() {
//		using (EnterWriteScope()) {
//			var registry = Tools.Json.ReadFromFile<LocalNotionRegistry>(_localNotionRegistryFile);

//			foreach(var resource in registry.Resources) {
////				resource.Slug
//			}
//		}
//	}

//	public IFileInfo GetFileInfo(string subpath) {
//		using (EnterReadScope()) {
//		}
//	}

//	public IDirectoryContents GetDirectoryContents(string subpath) {
//		using (EnterReadScope()) {
//		}
//	}

//	public IChangeToken Watch(string filter) {
//		throw new NotSupportedException();
//	}

//	public class FileInfo : IFileInfo {

//		public bool Exists { get; }

//		public long Length { get; }

//		public string PhysicalPath { get; }

//		public string Name { get; }

//		public DateTimeOffset LastModified { get; }

//		public bool IsDirectory { get; }

//		public Stream CreateReadStream() {
//			throw new NotImplementedException();
//		}

//	}

//}

