using System.Runtime.Serialization;
using Hydrogen;

namespace LocalNotion;

public enum RenderOutput {
	[EnumMember(Value = "html")]
	[FileExtension(".html")]
	HTML,

	[EnumMember(Value = "pdf")]
	[FileExtension(".pdf")]
	PDF,
}
