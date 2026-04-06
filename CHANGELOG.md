# Changelog

All notable changes to tykit are documented here.

## [0.3.0] - 2026-04-06

### Added
- `set-property` now accepts JSON arrays/objects for `Vector2/3/4`, `Vector2Int/3Int`, `Quaternion`, `Color`, `Rect`, `Bounds` — e.g. `"value": [112.26, -256.55]` or `"value": {"x":1,"y":2}`. Previously these types errored with `InvalidCastException`.
- `set-name` command for renaming GameObjects (previously impossible via `set-property` since `name` isn't a component).
- `hierarchy` command supports `id`/`path`/`name` parameters to traverse a specific subtree instead of only scene roots.
- `inspect` command now returns a `children` array with each child's `name`, `instanceId`, and `active` state (previously only `childCount` was exposed).
- `array-size` command — read or set the size of a serialized array/list property.
- `array-insert` command — insert a new element at an index (or append), optionally assigning a value.
- `array-delete` command — delete an element from a serialized array/list.
- `set-text` command — set `text` on `TMP_Text`/`TextMeshProUGUI`/`Text` without needing to know the serialized property name. Supports `inChildren` for nested text.
- `get-properties` now supports `structured: true` — returns native JSON types (numbers, arrays, nested objects) for `Vector/Color/Quaternion/Rect/Bounds/ObjectReference` instead of the legacy stringified format.

### Changed
- `set-property` `value` parameter now accepts native JSON types (int/float/bool/array/object) instead of string-only. String scalars still work for backward compatibility.

## [0.2.0] - 2026-03-29

### Changed
- Renamed `EvalServer` to `TykitServer` (class, file, log tags)
- Renamed `eval_server.json` to `tykit.json` (port info file in `Temp/`)
- Log tags changed from `[EvalServer]` to `[tykit]`
- Repository set to public and configured as automated mirror from [quick-question](https://github.com/tykisgod/quick-question)

### Migration
If you have scripts referencing `Temp/eval_server.json`, update them to `Temp/tykit.json`.

## [0.1.0] - 2026-03-15

### Added
- Initial release as UPM package
- HTTP server with `[InitializeOnLoad]` auto-start
- Deterministic port from project path hash (range 8090-8153)
- Commands: status, compile-status, console, find, inspect, create, destroy, set-transform, add-component, set-property, play, stop, pause, save-scene, refresh, clear-console, run-tests, get-test-result, batch
- `/ping` endpoint (responds from listener thread, no main thread needed)
- `Scripts~/unity-eval.sh` CLI tool
- `Scripts~/e2e-test.sh` end-to-end test runner
