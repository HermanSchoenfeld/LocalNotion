// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Notion.Client;

namespace LocalNotion.Core {

	public class ParentObject {

		public ParentObject(IParentOfBlock parent) : this((object)parent) {
		}

		public ParentObject(IParentOfComment parent) : this((object)parent) {
		}

		public ParentObject(IParentOfDatabase parent) : this((object)parent) {
		}

		public ParentObject(IParentOfDatasource parent) : this((object)parent) {
		}

		public ParentObject(IParentOfPage parent) : this((object)parent) {
		}

		private ParentObject(object notionParentObj) {
			(Type, Id) = notionParentObj switch {
				DatabaseParent db => (ParentType.DatabaseId, db.DatabaseId),
				PageParent page => (ParentType.PageId, page.PageId),
				WorkspaceParent => (ParentType.Workspace, null),
				BlockParent block => (ParentType.BlockId, block.BlockId),
				DatasourceParent ds => (ParentType.DatasourceId, ds.DataSourceId),
				_ => (ParentType.Unknown, null)
			};
		}

		public ParentType Type { get; }

		public bool HasId => Id is not null;

		public string Id { get; }

		public enum ParentType {
			Unknown,
			DatabaseId,
			PageId,
			Workspace,
			BlockId,
			DatasourceId
		}
	}
}
