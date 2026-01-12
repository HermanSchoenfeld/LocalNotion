// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public class SynchronizedLocalNotionRepository : LocalNotionRepositoryDecorator, ISynchronizedObject {
	private readonly SynchronizedObject _syncObj;

	public SynchronizedLocalNotionRepository(ILocalNotionRepository internalRepository) 
		: base(internalRepository) {
		_syncObj = new SynchronizedObject();
	}

	public ISynchronizedObject ParentSyncObject { 
		get => _syncObj.ParentSyncObject; 
		set => _syncObj.ParentSyncObject = value; 
	}

	public ReaderWriterLockSlim ThreadLock => _syncObj.ThreadLock;

	public override int Version {
		get {
			using (EnterReadScope())
				return InternalRepository.Version;
		}
	}
	
	public override ILogger Logger {
		get {
			using (EnterReadScope())
				return InternalRepository.Logger;
		}
	}

	public override string[] DefaultThemes {
		get {
			using (EnterReadScope())
				return InternalRepository.DefaultThemes;
		}
	}
	
	public override IPathResolver Paths {
		get {
			using (EnterReadScope())
				return InternalRepository.Paths;
		}
	}

	public override string DefaultNotionApiKey {
		get {
			using (EnterReadScope())
				return InternalRepository.DefaultNotionApiKey;
		}
	}

	public override IEnumerable<string> Objects {
		get {
			using (EnterReadScope())
				return InternalRepository.Objects;
		}
	}
	
	public override IEnumerable<string> Graphs {
		get {
			using (EnterReadScope())
				return InternalRepository.Graphs;
		}
	}

	public override IEnumerable<LocalNotionResource> Resources {
		get {
			using (EnterReadScope())
				return base.Resources;
		}
	}

	public override bool RequiresLoad { 
		get {
			using (EnterReadScope())
				return InternalRepository.RequiresLoad;
		}
	}

	public override bool RequiresSave { 
		get {
			using (EnterReadScope())
				return InternalRepository.RequiresSave;
		}
	}

	public override async Task LoadAsync() {
		using (EnterWriteScope())
			await InternalRepository.LoadAsync();
	}

	public override async Task SaveAsync() {
		using (EnterWriteScope())
			await InternalRepository.SaveAsync();
	}

	public override async Task ClearAsync() {
		using (EnterWriteScope())
			await InternalRepository.ClearAsync();
	}

	public override async Task CleanAsync() {
		using (EnterWriteScope())
			await InternalRepository.CleanAsync();
	}

	public override bool ContainsObject(string objectID) {
		using (EnterReadScope()) 
			return base.ContainsObject(objectID);
	}

	public override bool TryGetObject(string objectID, out IObject @object) {
		using (EnterReadScope()) 
			return base.TryGetObject(objectID, out @object);
	}

	public override void AddObject(IObject @object) {
		using (EnterWriteScope()) 
			base.AddObject(@object);
	}

	public override void UpdateObject(IObject @object) {
		using (EnterWriteScope())
			base.UpdateObject(@object);
	}

	public override void RemoveObject(string objectID) {
		using (EnterWriteScope()) 
			base.RemoveObject(objectID);
	}

	public override bool ContainsResource(string resourceID) {
		using (EnterReadScope())
			return base.ContainsResource(resourceID);
	}

	public override bool TryFindRenderBySlug(string slug, out CachedSlug result) {
		using (EnterReadScope())
			return base.TryFindRenderBySlug(slug, out result);
	}

	public override bool TryGetResource(string resourceID, out LocalNotionResource localNotionResource) {
		using (EnterReadScope()) 
			return base.TryGetResource(resourceID, out localNotionResource);
	}

	public override bool ContainsResourceRender(string resourceID, RenderType renderType) {
		using (EnterReadScope())
			return base.ContainsResourceRender(resourceID, renderType);
	}

	public override void AddResource(LocalNotionResource resource) {
		using (EnterWriteScope())
			base.AddResource(resource);
	}

	public override void UpdateResource(LocalNotionResource resource) {
		using (EnterWriteScope())
			base.UpdateResource(resource);
	}

	public override void RemoveResource(string resourceID, bool removeChildren) {
		using (EnterWriteScope())
			base.RemoveResource(resourceID, removeChildren);
	}

	public override bool ContainsResourceGraph(string objectID) {
		using (EnterReadScope())
			return base.ContainsResourceGraph(objectID);
	}

	public override bool TryGetResourceGraph(string resourceID, out NotionObjectGraph page) {
		using (EnterReadScope()) 
			return TryGetResourceGraph(resourceID, out page);
	}

	public override void AddResourceGraph(NotionObjectGraph pageGraph) {
		using (EnterWriteScope()) 
			base.AddResourceGraph(pageGraph);
	}

	public override void RemoveResourceGraph(string resourceID) {
		using (EnterWriteScope()) 
			base.RemoveResourceGraph(resourceID);
	}

	public override string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile) {
		using (EnterWriteScope())
			return base.ImportResourceRender(resourceID, renderType, renderedFile);
	}

	public override void RemoveResourceRender(string resourceID, RenderType renderType) {
		using (EnterWriteScope()) 
			base.RemoveResourceRender(resourceID, renderType);
	}

	public override string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) {
		using (EnterReadScope()) 
			return base.CalculateRenderSlug(resource, render, renderedFilename);
	}

	public IDisposable EnterReadScope() => _syncObj.EnterReadScope();

	public IDisposable EnterWriteScope() => _syncObj.EnterWriteScope();

}