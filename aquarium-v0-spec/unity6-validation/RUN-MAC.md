# Mac commands for the Unity 6 candidate

Editor: `/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity`

Use the independent copy exported from this commit. Do not point this editor at the old Mac tree, at `unity/`, or at the earlier `d125bfd` tree. Do not pass `-quit`. A non-zero exit means stop.

The launcher below starts one process group and, on timeout, signals only that PID and its descendants. It does not kill other Unity processes. If Unity never writes the result file, the wrapper log says `not_run`. That includes package resolution, compile, and license failure. Do not read a missing result as a pass, and do not treat the manifest pin as proof the packages were resolved.

`-projectPath` does not change the player save directory. Validation mode refuses to fall back to it. The save root must be a new child of an existing directory, outside the project and outside the real persistent-data directory after symlink resolution. The test creates only that new child. It does not create missing parents and does not delete an existing directory. The report directory must already exist; the test does not create it.

Replace `COMMIT`, `COPY`, and `OUT` below. `COMMIT` is the git SHA of this exported tree. `COPY` is the unzipped `unity6-validation` directory.

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity
COPY=/absolute/path/to/unity6-validation
OUT=/absolute/path/to/fresh-output
COMMIT=replace-with-the-export-commit
mkdir -p "$OUT/compile" "$OUT/proxy" "$OUT/save-parent"
```

1. Compile probe. Until `result.json` exists, package resolution and compilation stay `not_run`. Reaching the entry only means this process compiled; it does not import a GLB or open a save.

```sh
python3 /absolute/path/to/aquarium-v0-spec/tools/unity6_bounded_launch.py \
  --timeout-seconds 1800 \
  --result "$OUT/compile/result.json" \
  --wrapper-log "$OUT/compile/wrapper.log" \
  -- "$UNITY" -batchmode -nographics \
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
python3 /absolute/path/to/aquarium-v0-spec/tools/unity6_bounded_launch.py \
  --timeout-seconds 1800 \
  --result "$OUT/proxy/result.json" \
  --wrapper-log "$OUT/proxy/wrapper.log" \
  -- "$UNITY" -batchmode -nographics \
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
mkdir -p "$OUT/save" "$OUT/save-parent"
SAVE="$OUT/save-parent/run-$COMMIT"
test ! -e "$SAVE"
python3 /absolute/path/to/aquarium-v0-spec/tools/unity6_bounded_launch.py \
  --timeout-seconds 1800 \
  --result "$OUT/save/result.json" \
  --wrapper-log "$OUT/save/wrapper.log" \
  -- "$UNITY" -batchmode -nographics \
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
