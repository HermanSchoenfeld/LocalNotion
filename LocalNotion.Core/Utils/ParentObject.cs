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
