-- Run from any working directory using Lua 5.1.
local script = arg[0]:gsub("\\", "/")
TEST_ROOT = script:match("^(.*)/[^/]+$") or "."
ADDON_ROOT = TEST_ROOT .. "/../../addon/Claudgar"
dofile(TEST_ROOT .. "/RegressionTests.lua")
