using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum LocalNotionResourceType {
	[EnumMember(Value = "file")]
	File,

	[EnumMember(Value = "page")]
	Page,

	[EnumMember(Value = "database")]
	Database,

	[EnumMember(Value = "workspace")]
	Workspace
}
