# Read-only input environment diagnostic

Sampled 2026-11-06T05:03:00.722875+00:00. Player PID `53279`; exact argv is in `player-1280-invocation.json`. No window/input action, process lifecycle action, source/settings change, or save change was performed.

WindowServer all-windows enumeration contains 6 Player windows; on-screen-only enumeration contains 0. Earlier in this same turn the counts were 6 / 0, with main window 45042 at (0,30,640,432) points; Player logs report 1280x800 pixels. Earlier on-screen enumeration included a 1920x1080 CUALockScreenGuardian window at layer 2147483629. AX window availability and on-screen filtering differ; this does not establish a product mouse bug. Only game and related Guardian window metadata is retained in JSON.

CGSession returned OnConsole=true and LoginDone=true, with no ScreenIsLocked field. These values do not determine lock state or the game's current desktop. Guardian presence does not itself prove macOS lock state.

Root-reported CUA error fragments are exactly `SCStreamError -3812` for Raise and `noWindowsAvailable` for coordinate click, including after successful AX click(0). Full exception objects were not supplied or reconstructed. Root also reported F9, screenshots and AX click(0) succeeding; coordinate event delivery was not demonstrated.

The product uses legacy UnityEngine.Input with activeInputHandler=0; Mouse X/Y axes exist. Keys and camera polling share Update, which returns early when unfocused. Observed F9 success therefore cannot support a claim of persistent focus loss. Camera input requires x>312 physical pixels; Language is an OnGUI GUILayout.Button, independent of GetMouseButton and not disabled by pause. The scene uses AquariumGameEntry. No inspected source/settings mismatch was found.

Current 1280 logs show no error/exception or focus/input failure, but have no per-event diagnostics and cannot prove mouse arrival. Historical 4K log line 36 contains `error messaging the mach port for IMKCFRunLoopWakeUpReliable`, adjacent to exit cleanup; no causal relationship with clicking is established. Do not describe the historical4K log as strictly error-free.

The JSON contains timestamped process and related-window metadata, selected logs with hashes, source locations and interpretation limits. Mouse acceptance remains pending until the game is on the current desktop and coordinate event delivery is demonstrated.
