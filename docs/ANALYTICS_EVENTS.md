# Analytics Event Dictionary

Phase 1 uses the local, privacy-safe analytics implementation. It logs event names to Unity output and sends nothing over the network.

| Event | Trigger | Current properties |
|---|---|---|
| `app_opened` | Composition root initialized | `environment` |
| `daily_viewed` | Home route shown | challenge ID/version, puzzle type, environment |
| `daily_started` | BEGIN opens the puzzle host | challenge ID/version, puzzle type, environment |
| `puzzle_started` | Spatial host shown | challenge ID/version, puzzle type, environment |

Properties never contain puzzle solutions, private keys, signed transactions, or personal data. Later phases will add the remaining events from the build specification behind the same interface.

