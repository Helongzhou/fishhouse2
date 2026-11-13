# Mac commands for the Unity 6 candidate

Editor: `/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity`

Use the independent copy exported from the commit you were given. Do not point this editor at the old Mac tree or at `unity/`. Do not pass `-quit`; each entry calls `EditorApplication.Exit` itself. A non-zero exit means stop. Later commands are not a continuation of a failed process.

`-projectPath` does not change the player save directory. The save test refuses `~/Library/Application Support/Aquarium Journal` and any path inside the project. Choose a parent that already exists, and a final directory name that does not exist yet. The test creates only that final directory. It does not create missing parents and it does not delete a directory it did not just create. `AtomicSaveStore` may delete the `.tmp` file for the save it itself writes inside that directory.

Replace `COMMIT`, `COPY`, and `OUT` below. `COMMIT` is the git SHA of this exported tree. `COPY` is the unzipped `unity6-validation` directory.

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity
COPY=/absolute/path/to/unity6-validation
OUT=/absolute/path/to/fresh-output
COMMIT=replace-with-the-export-commit
mkdir -p "$OUT/compile" "$OUT/proxy" "$OUT/save-parent"
```

1. Package resolution is whatever the editor does before scripts compile. This command records that the compile entry was reached. It does not import a GLB and it does not open a save.

```sh
"$UNITY" -batchmode -nographics \
  -projectPath "$COPY" \
  -executeMethod Aquarium.Validation.TechProxyCandidateValidation.CompileProbe \
  -aquariumCommit "$COMMIT" \
  -aquariumReport "$OUT/compile/result.json" \
  -aquariumLog "$OUT/compile/editor.log" \
  -logFile "$OUT/compile/editor.log"
echo compile_exit:$?
```

Stop if that exit is not 0. `result.json` lists every later stage as `not_run`.

2. Proxy checks. `proxy_on_structure` is not a screenshot. `visual_screenshot` stays `not_run`.

```sh
"$UNITY" -batchmode -nographics \
  -projectPath "$COPY" \
  -executeMethod Aquarium.Validation.TechProxyCandidateValidation.RunProxyChecks \
  -aquariumCommit "$COMMIT" \
  -aquariumReport "$OUT/proxy/result.json" \
  -aquariumLog "$OUT/proxy/editor.log" \
  -logFile "$OUT/proxy/editor.log"
echo proxy_exit:$?
```

Stop if that exit is not 0.

3. Isolated save. `SAVE` must not already exist.

```sh
mkdir -p "$OUT/save"
SAVE="$OUT/save-parent/run-$COMMIT"
test ! -e "$SAVE"
"$UNITY" -batchmode -nographics \
  -projectPath "$COPY" \
  -executeMethod Aquarium.Validation.TechProxyCandidateValidation.RunIsolatedSave \
  -aquariumCommit "$COMMIT" \
  -aquariumSaveRoot "$SAVE" \
  -aquariumReport "$OUT/save/result.json" \
  -aquariumLog "$OUT/save/editor.log" \
  -logFile "$OUT/save/editor.log"
echo save_exit:$?
```

`$OUT/save` is only the report directory and must exist before Unity starts. `$SAVE` itself must not exist; the test creates that one directory and no parents.

A missing result file together with a non-zero exit means that stage did not finish. Do not treat a source file's existence as a pass. BatchMode does not prove the rendered picture; a person can open `Assets/_Game/TechProxies/TechProxyValidation.unity` outside these commands and record a screenshot separately.
