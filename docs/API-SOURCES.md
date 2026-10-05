# Implementation sources

Sources were reviewed on 5 October 2026. Runtime/game verification is deliberately
left to the user; documented APIs and a successful C# build do not establish that
the untested addon works on a particular beta build.

## Forever client

See [FOREVER-API.md](FOREVER-API.md) for the exact Blizzard-authored Forever UI
source files, API decisions, collector details, and beta limitations.

- [Blizzard Forever Beta announcement](https://worldofwarcraft.blizzard.com/en-us/news/24304160/)
  establishes the separate Forever Beta client.
- [Blizzard-authored UI sources, Forever branch](https://github.com/Gethe/wow-ui-source/tree/forever)
  are the API implementation reference. The branch's Camelot files matter;
  Retail/Classic recipes are not treated as proof of Forever support.
- [Forever simulator developer repository](https://github.com/ProfetGit/forever-sim)
  identifies the 1.60 client family and its shared beta product slot.
- [ArcUI developer repository](https://github.com/ArcSim/ArcUI/blob/main/CLAUDE.md)
  points Forever API work at the Forever UI source branch.
- [WoW API source tooling](https://github.com/Nighthawk42/wow_api_mcp)
  documents version/flavor discrimination for the shared Classic beta slot.

The current shared installation directory is `_classic_beta_`; that label alone
is insufficient. Detection requires an active `wow_classic_beta` product row
with the Forever 1.60 family in `.build.info` and the beta executable. The addon
uses its Camelot-specific TOC and independently checks the Forever version.
No exact beta patch/build number is pinned. A future client family or product
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
