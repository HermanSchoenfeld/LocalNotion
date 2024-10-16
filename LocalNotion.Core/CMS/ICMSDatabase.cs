namespace LocalNotion.Core;

public interface ICMSDatabase {

	IEnumerable<CMSContentNode> CMSContent { get; }

}
	