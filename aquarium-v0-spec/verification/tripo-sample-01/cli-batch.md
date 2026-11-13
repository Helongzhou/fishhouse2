# tripo batch run

Bulk production from a YAML manifest with concurrency, retries, and resume.

```bash
tripo batch run assets.yaml [--concurrency 2] [--retries 1] [--fresh]
```

## Manifest

```yaml
concurrency: 2
defaults:
  for: game-mobile
  then: "texture,convert:fbx"
  out: ./assets
jobs:
  - input: "a bronze sword"
  - input: "a wooden shield"
    name: shield            # names become @references and state keys
  - input: cat.png
    for: print
    then: "convert:stl,flatten_bottom=true"
  - inputs: [hero-front.png, hero-back.png]
    name: hero
    params: { texture_quality: detailed }
```

## Cost

- Every job is a full `make` that runs without a plan card or confirmation.
- All seven presets (`game-mobile` `game-pc` `film` `print` `ar-web` `anim` `toy`) generate with P2: 100–130 credits per job for the generation step alone (`print` skips textures: 100); the example's default `game-mobile` is at least 110 per job, before chain steps.
- Before running, dry-run one job per preset/params combination with the matching `make` flags (`tripo make "a bronze sword" --for game-mobile --then texture,convert:fbx --dry-run --json`), multiply `generation_credits_estimate` by the number of jobs that share it (`null` means not estimated, never zero), relay the total to the human and get confirmation.

## Behavior

- State is saved next to the manifest (`assets.yaml.state.json`) after every job and as soon as each paid task is created. Re-running the same command **skips succeeded jobs** (resume). `--fresh` starts over.
- A job is retried (up to `--retries` times) only when the failed attempt left no paid task behind: it failed before anything was submitted, or its only task failed server-side (failed/cancelled tasks are refunded).
- Anything else stops the job as `needs_attention`, and it is **never resubmitted automatically**, not even on re-run: a create request whose response was lost (the task may exist and be billed), a timeout, a failed artifact download, or a failure after an earlier task of the job succeeded.
  - Its state entry records `tasks` (every task created for the job, in order), `error`, and `suggestion` (the next step, e.g. `tripo task get <id> --download`, or `tripo make <task_id> --then ...` to continue from the last successful task).
- To resubmit a `needs_attention` job on purpose, delete its entry from the state file, or use `--fresh` to rerun everything.
- Exit code 6 if any job stays `failed` or `needs_attention`.
- Final stdout JSON: `{"total","success","failed","needs_attention","state_file","jobs":{...}}`.

