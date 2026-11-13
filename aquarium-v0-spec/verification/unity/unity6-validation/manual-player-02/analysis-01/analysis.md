# Recorded manual-player snapshot analysis

Read-only recorded JSON comparison. No live-save discovery, Unity Restore execution, input injection, screenshot interpretation or physical-input certification.

| Snapshot | Real seconds | Ecology days | Paused | Light | Coins | Food portions | Receipts | Fish / shrimp | Behavior |
|---|---:|---:|---|---|---:|---:|---:|---|---|
| fixture | 721.0000000000016 | 5.5083333333341 | False | False | 90 | 12 | 23 | 8 / 6 | Forage 2, Graze 2, Roam 6, SeekFood 3, Shoal 1 |
| pause_f5_a | 721.1666666666681 | 5.509722222222989 | True | False | 90 | 12 | 23 | 8 / 6 | Forage 2, Graze 2, Roam 6, SeekFood 3, Shoal 1 |
| pause_f5_b | 721.1666666666681 | 5.509722222222989 | True | False | 90 | 12 | 23 | 8 / 6 | Forage 2, Graze 2, Roam 6, SeekFood 3, Shoal 1 |
| paused_after_f_l | 721.1666666666681 | 5.509722222222989 | True | False | 90 | 12 | 23 | 8 / 6 | Forage 2, Graze 2, Roam 6, SeekFood 3, Shoal 1 |
| resumed_light | 721.2333333333347 | 5.510277777778545 | True | True | 90 | 12 | 24 | 8 / 6 | Forage 2, Graze 2, Roam 6, SeekFood 3, Shoal 1 |
| resumed_feed | 763.399999999963 | 5.861666666667489 | True | True | 90 | 0 | 25 | 8 / 6 | Forage 3, Graze 4, Roam 4, Shoal 3 |
| loaded_paused | 763.399999999963 | 5.861666666667489 | True | True | 90 | 0 | 25 | 8 / 6 | Forage 3, Graze 4, Roam 4, Shoal 3 |

| Check | Before → after | State result |
|---|---|---|
| paused_exact | pause_f5_a → pause_f5_b | PASS |
| paused_commands | pause_f5_b → paused_after_f_l | PASS |
| resume_light | paused_after_f_l → resumed_light | PASS |
| resume_feed | resumed_light → resumed_feed | PASS |
| restore_paused | resumed_feed → loaded_paused | PASS |

Physical input and screenshot findings remain a separate coordinator review.

Recorded artifacts:

- [fixture](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/unity6-batch-04/stocked-with-food-and-checkpoint.json>), SHA256 523cb867ae4b53d73cad4cb5498e0fda6abbad80c988cff5785f0124cc19807a
- [pause_f5_a](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/snapshots/01-paused.json>), SHA256 42139ce1725e7853d13e5f959b34c5fd1d79e9c3dd8db121e173718963d90052
- [pause_f5_b](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/snapshots/02-paused-later.json>), SHA256 42139ce1725e7853d13e5f959b34c5fd1d79e9c3dd8db121e173718963d90052
- [paused_after_f_l](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/snapshots/03-paused-commands.json>), SHA256 42139ce1725e7853d13e5f959b34c5fd1d79e9c3dd8db121e173718963d90052
- [resumed_light](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/snapshots/04-light-on.json>), SHA256 edb1095b7b74b4fc881e46977461109753c2d2420a8bd786e96e8f327d0ad092
- [resumed_feed](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/snapshots/05-fed-paused.json>), SHA256 52455db68d0eea8f52bc306648ad74acb7643d265927627dcc879159fd4f8c6a
- [loaded_paused](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/snapshots/06-restored-paused.json>), SHA256 dcfe29a5d3968b2798af85d2ec521115ef877a9e494dd6f4ba4e5b919bc2621a
- [09-light-on](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/09-light-on.png>): Coordinator reports Space resume, L turns light on, Space pauses, F5 records snapshot 04.
- [10-fed](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/10-fed.png>): Coordinator reports Space resume, F feeds, simulation runs, Space pauses and F5 records snapshot 05.
- [11-unsaved-light-off](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/11-unsaved-light-off.png>): Coordinator reports resumed L turns light off in memory; no F5 occurs before F9.
- [12-restored](</Users/zhouhelong/Documents/vibe coding/aquarium-v0-spec/verification/unity/unity6-validation/manual-player-02/12-restored.png>): Coordinator reports F9 restores primary snapshot 05 and F5 records snapshot 06.
