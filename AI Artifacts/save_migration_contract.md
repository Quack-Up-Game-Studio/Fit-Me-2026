# Save migration contract

## Version identity

`MessagePackSaveObject<T>` stamps saves with `Application.version` and uses the same application version as its migration target. The target does not depend on the current `saveData` instance, its serialized asset state, or whether startup has just called `Reset()`.

The local archive load and cloud archive candidate deserialization share `TryDeserializeSaveData`. Cloud candidate deserialization does not itself apply the candidate to the live save object; selection/application remains the existing manager/cloud handler's responsibility.

Migration graph nodes use semantic version precedence. Two-part and three-part equivalents (such as `1.0` and `1.0.0`) connect, build metadata does not distinguish nodes, and prerelease identifiers remain significant. Resolver-declared source/target strings are not rewritten before `Migrate` or `Finalize` runs.

The graph uses FIFO breadth-first search. The first discovered target path is shortest; equal-length alternatives follow resolver configuration order. Null resolvers and malformed endpoint versions are skipped with a warning. A graph with no usable path reports migration failure.

## Compatibility policy

This checkpoint deliberately retains the existing permissive load policy:

- Failed path discovery or migration returning `false` logs warnings and returns successfully deserialized data without migration.
- An invalid stored version also retains deserialized data, with an explicit warning rather than a claim that versions match.
- An invalid application target logs an error and retains deserialized data without migration.
- Ordinary deserialization failure still returns `null` and leaves the existing in-memory data unchanged when loaded through `LoadFromBytes`.
- Successful fallback does not stamp the loaded object as migrated. A subsequent normal save still stamps the current application version, as before.

This policy does not guarantee schema compatibility, prevent information loss in an incompatible future schema, handle every exception thrown by arbitrary resolver implementations, or introduce a fresh-start UI. Missing resolver coverage is not a successful migration. Strict rejection, schema-version decoupling, format changes, and overwrite protection need a separate approved design.

## Preserved boundaries

No serializer option, attributed data contract, ZIP layout, separate-save location, or cloud account/conflict-selection policy changes in this checkpoint.

Android save readiness remains owned by the existing GPGS handler. Its cloud timeout begins after authentication. A manager-level forced-ready watchdog is not added here: authentication cancellation/timeout and late cloud candidate application must be designed together before that behavior changes.

## Verification boundary

EditMode tests cover startup reset migration, stale in-memory target independence, equivalent-version graph links, shortest-path selection, malformed resolver endpoints, local/cloud ZIP deserialization, and compatibility fallback. They do not prove Android device cloud authentication or gameplay startup timing.
