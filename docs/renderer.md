# Rendering in LocalNotion

LocalNotion consumes the MIT-licensed `Sphere10.Framework.Web.VisualRenderer` library from Sphere10 Framework. Workspace builds use `../Framework`; NuGet builds use the same exact clean Framework version for all four Framework packages.

LocalNotion.Core/Rendering owns conversion of Notion objects and repository
metadata into the renderer's visual model. LocalNotion also owns repository
paths, persistence, links, synchronization, and optional HTML post-processing.
The renderer accepts visual objects and returns HTML/text and assets without
access to Notion credentials or repositories.

The public rendering API uses the Sphere10.Framework.Web.VisualRenderer namespace. Refer to
the authorized package's README for standalone usage. See
[theme overrides](renderer-themes.md) for LocalNotion customization.

## Licensing and distribution

Sphere10.Framework.Web.VisualRenderer is MIT-licensed, as declared in its project
and included LICENSE.txt. Third-party components and assets retain their
own terms. Preserve those notices when redistributing the package's output.

LocalNotion remains licensed under the [GNU GPL](../LICENSE). The historical
[COPYING.EXCEPTION](../COPYING.EXCEPTION) remains preserved; it grants permissions
only for the copyrights described in that file and does not replace third-party
licenses. The current renderer's MIT license is independent of that exception.

Keep LocalNotion's LICENSE, COPYRIGHT and COPYING.EXCEPTION with distributions.
The CLI project copies them to build and publish output as separate files,
including when publishing a single executable. The renderer's NuGet package
separately supplies its LICENSE.txt and THIRD-PARTY-NOTICES.md under
licenses/Sphere10.Framework.Web.VisualRenderer/, with dependency notices in its third-party
subfolder. Preserve that directory in installers, containers and archives.
Do not replace the renderer license with LocalNotion's GPL license.

## Building

The renderer belongs to the Framework solution. Set `Sphere10FrameworkRoot` to
select another clean Framework checkout. It uses the same configuration as LocalNotion.

```powershell
dotnet build LocalNotion.sln -c Release -p:BuildNumber=0 -p:BuildRevision=0
```

For package mode, make the matching clean Framework package set available through
a configured NuGet feed, including `Sphere10.Framework.Web.VisualRenderer`:

```powershell
dotnet build LocalNotion.sln -c Release -p:UseSphere10FrameworkSource=false -p:Sphere10FrameworkVersion=<exact-clean-version> -p:BuildNumber=0 -p:BuildRevision=0
```

The renderer shares Framework's version. The former `UseSphere10VisualRendererSource`,
`Sphere10VisualRendererRoot` and `Sphere10VisualRendererVersion` switches have been
replaced by the standard Framework reference configuration. Source and package builds
retain MIT and third-party notices. CMS presets remain owned by LocalNotion.Core;
site-specific themes remain external overlays.

Native CI and Docker use the one immutable `build/framework-source.ref` commit.
That pin must identify a published Framework revision containing the renderer before
a release can succeed. A local build or locally packed NuGet file does not establish
public package availability or update installed executables.
