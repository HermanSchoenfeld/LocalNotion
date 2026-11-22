# Upstream integration and local fixes

This vendored client integrates `notion-dotnet/notion-sdk-net` main at commit
`fbbf95c826ec34c4a6c9be47a20dd5948c8f6053`, fetched on 12 November 2026.
The upstream project remains MIT licensed. Its native `TabBlock`,
`TabBlockRequest` and paragraph icon support are included directly; tabs are
Notion block types, not a CMS Theme or Tag.

## Preserve the HTTP-client lifetime fix

The local fix originating in `e61e205` prevents SDK-created `HttpClient` instances
from being retained without disposal. `RestClient.SendAsync` creates a client
for one request (including any opt-in retry attempts) and disposes it when that
request completes or fails. The constructor does not allocate an SDK-owned
client. Do not replace this implementation with upstream's stored default client.

Upstream also supports a caller-supplied `ClientOptions.HttpClient`. Such a
client remains caller-owned and must not be disposed by the SDK. These two
ownership cases are deliberately distinct. Retry remains opt-in.

LocalNotion's `NotionClientLifetimeTests` exercise real loopback keep-alive
connections over repeated successful and failed requests. The server leaves
the connection open, so EOF proves disposal by the SDK. A separate test verifies
that an injected client remains usable until its caller disposes it.

## Other preserved changes

- Page/DataSource query discriminators and page-cover deserialization use the
  equivalent corrected upstream mappings, including its new string constants.
- String-valued property configuration and relation `Type` fields must not use
  `StringEnumConverter`. The local relation fix and explanatory comment remain.
- LocalNotion's built-in icon model retains `NotionIcon` and extension data.
  The equivalent upstream class is not duplicated in `CustomEmojiPageIcon.cs`.
- Local dependency versions are retained. `Microsoft.Extensions.Http` is added
  at the matching version for upstream's new HTTP-client factory integration.
- LocalNotion consumers use the upstream extensible discriminator values while
  preserving their existing resource qualification and metadata spelling.

The source integration is not a public NuGet release, application release or
proof that the installed Local Notion executable contains this revision.
