# Actual Player input outcome

Build04 passed the recorded keyboard paths F9/F5/Space/L/F and English readability at 3840x2160 and 1280x800. Evidence: `analysis-01/review.json`, snapshots 01-06, screenshots 09-12 and 15, and final Player logs. The 1280 final capture shows the loaded paused/light-on 14-animal / 90-coin state. This does not complete the 22-animal / 280-coin route.

Coordinate mouse interactions, Chinese switching/layout, wheel zoom, right-button orbit and glass approach remain unaccepted. CUA reported `noWindowsAvailable`; AX and F9 could succeed while the Player was absent from the WindowServer on-screen set. Event delivery was not established, so these are pending acceptance, not established product failures. `input-environment-diagnostic.json` retains the facts and limitations. Screenshot filename `07-zh-paused.png` is not proof of Chinese.

Cleanup completed: five save paths restored to their original absent states; four acceptance saves retained in `after/` with verified hashes; no Editor/Player processes. Two relevant preference plists contain no explicit aquarium.language key and did not change during cleanup; actual UI/log locale stayed English. See `restore-verification.json`.

Final logs are `player-1280-final.log` and `player-1280-stdout-final.log`; hashes and exit notes are in `player-log-final-state.json`. Preserve historical 4K IMK mach-port and final 1280 native shutdown messages; do not claim strictly error-free Player logs. Next minimal path: `next-manual03-retest.md`.
