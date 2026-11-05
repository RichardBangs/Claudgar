# Implementation sources

Forever release compatibility was reviewed on 5 November 2026. Runtime/game
verification is left to the user; source review and automated tests do not
establish that the addon works in a running release client.

## Forever client

See [FOREVER-API.md](FOREVER-API.md) for the exact Blizzard-authored Forever UI
source files, API decisions, collector details, and runtime limitations.

- [Blizzard's Forever launch announcement](https://worldofwarcraft.blizzard.com/en-us/news/24303862/world-of-warcraft-forever-whats-next)
  announces the global release on 4 November 2026.
- [Blizzard-authored UI sources, Forever branch](https://github.com/Gethe/wow-ui-source/tree/forever)
  are the API implementation reference. The branch's Camelot files matter;
  Retail/Classic recipes are not treated as proof of Forever support.
- [Pinned Forever client version](https://github.com/Gethe/wow-ui-source/blob/eac30af597e5aaa43a60f6675ef5fdcea100375f/version.txt)
  records `1.60.1.70790`, the release source baseline reviewed on 5 November.

Read-only inspection of the installed Blizzard `.build.info` on 5 November
confirmed active product `wowf`, version `1.60.1.70790`, and the `_forever_`
client directory. Detection requires that release product, a Forever 1.60
version, and its executable; a folder name alone is insufficient. The old
`_classic_beta_` / `wow_classic_beta` installation is not a release source.
An existing beta selection can migrate to a verified release client under the
same World of Warcraft root; beta character exports are not copied or merged.
The addon retains its Camelot-specific TOC and independently checks the Forever
version. No exact patch/build number is pinned. A future client family or product
rename requires an explicit detection update.

## Windows distribution

[Microsoft single-file deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
supports self-contained C# applications. The publish script bundles the .NET,
Windows Desktop, ASP.NET Core, and MCP dependencies into a Windows x64 executable.
Native libraries extract to the normal per-user temporary cache on launch.
The user does not install .NET or a web server. This is a portable application,
not a Windows service or an installer requiring administrator privileges.

## Codex and MCP

- [Official Codex MCP documentation](https://learn.chatgpt.com/docs/extend/mcp)
  describes user `config.toml`, Streamable HTTP, and client restart after setup.
- [Official Codex skills documentation](https://learn.chatgpt.com/docs/build-skills)
  describes user `.agents/skills` and `SKILL.md` instructions.
- [Official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
  supplies the HTTP protocol implementation. The app uses
  `ModelContextProtocol.AspNetCore` 2.2.0, bundled at publish time.
- [SDK stateless HTTP guidance](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/stateless/stateless.html)
  describes current and older handshake client support without maintaining
  application session state. The app binds only to IPv4 loopback and validates
  Host/Origin before dispatching requests.
