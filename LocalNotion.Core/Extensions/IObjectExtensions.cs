using Hydrogen;
using Notion.Client;


namespace LocalNotion.Core;

public static class IObjectExtensions {

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

	
}

