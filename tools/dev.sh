#!/bin/bash
# Build and check the project with Unity's bundled C# compiler, without opening (or closing) the Editor.
#
#   tools/dev.sh compile   compile Core, View and the Editor tools; prints errors only
#   tools/dev.sh test      build Core + EditMode tests into one exe and run them on Unity's .NET
#   tools/dev.sh wiki      write docs/gamepedia.html without statue images (use the Unity menu
#                          Tools > Export Gamepedia for the illustrated version)
#   tools/dev.sh sim [N]   play N seeds per draft and fight with bots (default 50); writes tools/out/balance-report.md
#
# Output goes to tools/out/ (git-ignored). Needs Git Bash on Windows and the project's Unity version.
set -u
# Windows-style path (C:/...): the Windows compiler can't read Git Bash's /c/... form.
R="$(cd "$(dirname "$0")/.." && (pwd -W 2>/dev/null || pwd))"
VERSION=$(sed -n 's/^m_EditorVersion: //p' "$R/ProjectSettings/ProjectVersion.txt" | tr -d '\r')
E="C:/Program Files/Unity/Hub/Editor/$VERSION/Editor/Data"
OUT="$R/tools/out"
DOTNET="$E/NetCoreRuntime/dotnet.exe"
CSC="$E/DotNetSdkRoslyn/csc.dll"
NS="$E/NetStandard/ref/2.1.0/netstandard.dll"
RT=$(ls -d "$E"/NetCoreRuntime/shared/Microsoft.NETCore.App/* | head -1)
mkdir -p "$OUT"

csc() { # rsp-file -> compiles, hides noise warnings, returns the compiler's exit code
  "$DOTNET" "$CSC" "@$1" 2>&1 | grep -vE "CS1701|CS1702|CS2023|CS8012|CS1685" | grep -v "^$"
  return "${PIPESTATUS[0]}"
}

# Reference list for a plain .NET exe (Core-only code: tests and the text gamepedia).
runtime_refs() {
  for f in "$RT"/System*.dll "$RT"/mscorlib.dll "$RT"/netstandard.dll "$RT"/Microsoft.CSharp.dll; do
    case "$(basename "$f")" in System.IO.Compression.Native.dll|System.Native.dll) ;; *) echo "-r:\"$f\"" ;; esac
  done
}

runtimeconfig() {
  echo "{\"runtimeOptions\":{\"tfm\":\"net6.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"$(basename "$RT")\"}}}" > "$1"
}

case "${1:-}" in
  compile)
    status=0
    { echo "-nologo -nostdlib -t:library -out:\"$OUT/ArmyRoguelike.Core.dll\" -r:\"$NS\""
      for f in "$R"/Assets/Scripts/Core/*.cs; do echo "\"$f\""; done; } > "$OUT/core.rsp"
    echo "== Core"; csc "$OUT/core.rsp" || status=1
    { echo "-nologo -nostdlib -t:library -out:\"$OUT/ArmyRoguelike.View.dll\" -r:\"$NS\" -r:\"$OUT/ArmyRoguelike.Core.dll\""
      echo "-r:\"$R/Library/ScriptAssemblies/Unity.InputSystem.dll\""
      for f in "$E"/Managed/UnityEngine/UnityEngine.*.dll; do echo "-r:\"$f\""; done
      for f in "$R"/Assets/Scripts/View/*.cs; do echo "\"$f\""; done; } > "$OUT/view.rsp"
    echo "== View"; csc "$OUT/view.rsp" || status=1
    { echo "-nologo -nostdlib -t:library -out:\"$OUT/Editor.dll\" -r:\"$NS\" -r:\"$OUT/ArmyRoguelike.Core.dll\" -r:\"$OUT/ArmyRoguelike.View.dll\""
      for f in "$E"/Managed/UnityEngine/*.dll; do echo "-r:\"$f\""; done
      for f in "$R"/Assets/Editor/*.cs; do echo "\"$f\""; done; } > "$OUT/editor.rsp"
    echo "== Editor"; csc "$OUT/editor.rsp" || status=1
    exit $status
    ;;
  test)
    NUNIT=$(ls "$R"/Library/PackageCache/com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll | head -1)
    cp "$NUNIT" "$OUT/"
    { echo "-nologo -nostdlib -noconfig -t:exe -out:\"$OUT/Tests.exe\" -r:\"$OUT/nunit.framework.dll\""
      runtime_refs
      for f in "$R"/Assets/Scripts/Core/*.cs "$R"/Assets/Tests/EditMode/*.cs "$R/tools/TestRunner.cs"; do echo "\"$f\""; done; } > "$OUT/tests.rsp"
    csc "$OUT/tests.rsp" || { echo "BUILD FAILED"; exit 2; }
    runtimeconfig "$OUT/Tests.runtimeconfig.json"
    "$DOTNET" "$OUT/Tests.exe"
    ;;
  wiki)
    { echo "-nologo -nostdlib -noconfig -t:exe -out:\"$OUT/Wiki.exe\""
      runtime_refs
      for f in "$R"/Assets/Scripts/Core/*.cs "$R/Assets/Editor/GamepediaWriter.cs" "$R/tools/GamepediaTextExport.cs"; do echo "\"$f\""; done; } > "$OUT/wiki.rsp"
    csc "$OUT/wiki.rsp" || { echo "BUILD FAILED"; exit 2; }
    runtimeconfig "$OUT/Wiki.runtimeconfig.json"
    "$DOTNET" "$OUT/Wiki.exe" "$R/docs/gamepedia.html" && echo "wrote docs/gamepedia.html (text only)"
    ;;
  sim)
    { echo "-nologo -nostdlib -noconfig -optimize+ -t:exe -out:\"$OUT/BalanceSim.exe\""
      runtime_refs
      # SIM_CORE: a patched copy of Core, to try a number change without touching the game.
      for f in "${SIM_CORE:-$R/Assets/Scripts/Core}"/*.cs "$R"/tools/balance/*.cs; do echo "\"$f\""; done; } > "$OUT/sim.rsp"
    csc "$OUT/sim.rsp" || { echo "BUILD FAILED"; exit 2; }
    runtimeconfig "$OUT/BalanceSim.runtimeconfig.json"
    case "${3:-}" in
      smart) "$DOTNET" "$OUT/BalanceSim.exe" "$OUT/balance-smart.md" "${2:-5}" smart ;;
      fights) "$DOTNET" "$OUT/BalanceSim.exe" "$OUT/balance-fights.md" "${2:-10}" fights ${4:+"$4"} ;;
      *) "$DOTNET" "$OUT/BalanceSim.exe" "$OUT/balance-report.md" "${2:-50}" ;;
    esac
    ;;
  *)
    sed -n '2,11p' "$0"
    exit 1
    ;;
esac
