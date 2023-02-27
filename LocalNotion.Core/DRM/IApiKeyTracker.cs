namespace LocalNotion.Core;

public interface IApiKeyTracker {

	HashSet<string> UsedApiKeys { get; set; }

	
}
