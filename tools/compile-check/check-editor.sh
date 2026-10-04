#!/usr/bin/env bash
# Full type-check of the Editor assembly without Unity. The reference UnityEditor
# (2018.1) predates two members the level editor uses, so the sources are copied
# and those two names rewritten to shims (stubs/EditorShims.cs) first. Only for
# checking — nothing here touches Assets/.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
src="$here/../../Assets/_Project/Scripts/Editor"
tmp="$here/obj/editor-src"
rm -rf "$tmp"; mkdir -p "$tmp"
cp -r "$src/." "$tmp/"
find "$tmp" -name '*.cs' -exec sed -i -e 's/\brootVisualElement\b/this.RootVisualElementShim()/g' -e 's/\.painter2D\b/.Painter2DShim()/g' {} +
dotnet build "$here/Editor.Check.csproj" -nologo -v q -clp:ErrorsOnly "$@"
