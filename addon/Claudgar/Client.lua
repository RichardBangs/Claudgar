local _, ns = ...
ns.Client = {}
ns.enabled = false

local ok, version, build, _, interface = pcall(GetBuildInfo)
-- Forever currently exposes the Mainline API family. WOW_PROJECT_ID is not a
-- dependable discriminator. Camelot TOC routing and the 1.60+ interface family
-- distinguish it from Retail, Era and the expansion Classic clients.
if ok and type(version) == "string" and type(interface) == "number"
    and version:match("^1%.") and interface >= 16000 and interface < 20000 then
    ns.enabled = true
    ns.Client.info = {
        flavor = "forever",
        version = version,
        build = tostring(build),
        interface = interface,
        locale = GetLocale(),
    }
end
