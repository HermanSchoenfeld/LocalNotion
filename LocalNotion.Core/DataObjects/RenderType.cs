using System.Runtime.Serialization;
using Sphere10.Framework;

namespace LocalNotion.Core;

public enum RenderType {

	[EnumMember(Value = "file")]
	File,

	[EnumMember(Value = "html")]
	[FileExtension(".html")]
	HTML,

	[EnumMember(Value = "pdf")]
	[FileExtension(".pdf")]
	PDF,
}
