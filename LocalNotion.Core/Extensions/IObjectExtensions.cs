using Hydrogen;
using Notion.Client;


namespace LocalNotion.Core;

public static class IObjectExtensions {

	public static string GetTitle(this IObject obj) 
		=> obj switch {
			Page page => page.GetTitle(),
			Database database => database.GetTitle(),
			_ => throw new NotSupportedException($"{obj.GetType()}")
		};

	public static DateTime? GetLastEditedDate(this IObject obj) 
		=> obj switch {
			Page page => page.LastEditedTime,
			Database database => database.LastEditedTime,
			Block block => block.LastEditedTime,
			_ => default
		};

	public static bool HasFileAttachment(this IObject block)
		=> block.GetType().IsIn(typeof(AudioBlock), typeof(FileBlock), typeof(ImageBlock), typeof(PDFBlock), typeof(VideoBlock), typeof(CalloutBlock)) && (block.GetFileAttachmentOrDefault() != null);

	public static FileObject GetFileAttachment(this IObject @object) 
		=> @object.GetFileAttachmentOrDefault() ?? throw new InvalidOperationException("Object type does not have file attachment property");

	public static FileObject GetFileAttachmentOrDefault(this IObject block) 
		=> TypeSwitch<FileObject>.For(block,
			TypeSwitch<FileObject>.Case<AudioBlock>(x => x.Audio),
			TypeSwitch<FileObject>.Case<FileBlock>(x => x.File),
			TypeSwitch<FileObject>.Case<ImageBlock>(x => x.Image),
			TypeSwitch<FileObject>.Case<PDFBlock>(x => x.PDF),
			TypeSwitch<FileObject>.Case<VideoBlock>(vb => vb.Video),
			TypeSwitch<FileObject>.Case<CalloutBlock>(cb => cb.Callout.Icon as FileObject),
			TypeSwitch<FileObject>.Default(default)
		);

	public static IParent GetParent(this IObject obj) 
		=> TryGetParent(obj, out var parent) ? parent : throw new InvalidOperationException($"{nameof(IObject)} of type {obj.GetType().Name} does not have a parent");

	public static bool TryGetParent(this IObject obj, out IParent parent) {
		switch (obj) {
			case Comment comment:
				parent = comment.Parent;
				return true;

			case Database database:
				parent = database.Parent;
				return true;

			case IBlock block:
				parent = block.Parent;
				return true;

			case Page page:
				parent = page.Parent;
				return true;

			case PartialUser partialUser:
			case User user:
			default:
				parent = null;
				break;
		}
		return false;
	}


}

