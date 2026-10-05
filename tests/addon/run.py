"""Run the addon regression suite without launching World of Warcraft.

Install the optional test dependency with `python -m pip install lupa`, then run
`python tests/addon/run.py`. Lupa's Lua 5.1 runtime matches the addon language.
"""

from pathlib import Path
import sys

TESTS = Path(__file__).resolve().parent
REPOSITORY = TESTS.parent.parent
VENDORED_TOOLS = REPOSITORY / "dist" / "test-tools"
if VENDORED_TOOLS.is_dir():
    sys.path.insert(0, str(VENDORED_TOOLS))

try:
    from lupa.lua51 import LuaRuntime
except ImportError:
    raise SystemExit("Addon tests require Lupa: python -m pip install lupa")


def main() -> None:
    runtime = LuaRuntime(unpack_returned_tuples=True)
    runtime.globals().ADDON_ROOT = (REPOSITORY / "addon" / "Claudgar").as_posix()
    runtime.globals().TEST_ROOT = TESTS.as_posix()
    runtime.execute((TESTS / "RegressionTests.lua").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
