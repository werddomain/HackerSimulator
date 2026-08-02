# Canonical Settings System

## Purpose

Provide Linux-like, human-editable settings files without creating two sources
of truth. The settings service owns canonical typed documents, while the virtual
filesystem projects those same records at paths such as
`/etc/hackeros/file-associations.json`.

## Architecture

`HackerOs.Simulation.Abstractions` defines settings documents, read/write
results, whole-document validators, optimistic revisions, the canonical service,
and filesystem projection interfaces.

`HackerOs.Platform.Core` currently provides:

- `InMemorySettingsDocumentService`, a headless reference implementation;
- `SettingsFileProjection`, which delegates file operations to the canonical
  service rather than copying settings; and
- `FileAssociationSettingsValidator`, which validates human-edited JSON before
  commit.

Browser IndexedDB persistence will replace the in-memory repository behind the
same contracts. It must not change authorization or projection semantics.

ADR 0011 defines structured settings keys for app/user, app/device,
app/user/device, app/roaming-user, and OS/admin documents. Filesystem paths remain deterministic
projections, while the structured key owns scope, app, user, installation, and
document identity.

Default app paths are:

```text
/home/{userId}/.config/apps/{appId}/settings.config
/home/{userId}/.config/apps/{appId}/roaming/settings.config
/var/lib/hackeros/devices/{installationId}/apps/{appId}/settings.config
/var/lib/hackeros/devices/{installationId}/users/{userId}/apps/{appId}/settings.config
```

Protected documents remain under `/etc/hackeros/`, including the existing
file-association path. Only roaming scope is sync-eligible by default; device
scope never roams.

Ordinary `.config` documents accept blank lines, `#` comment lines, root
`key=value` pairs, and optional `[GroupName]` sections. The settings schema owns
valid groups, keys, types, defaults, and migrations. Malformed syntax, duplicate
sections or keys, unknown keys, and invalid values reject the complete write.
`/etc/hackeros/file-associations.json` remains the explicit strict-JSON exception.

## Authorization

Every read or write requires both:

1. the exact capability configured by the document definition; and
2. sufficient effective authority under `System > Administrator > User`.

Normal user-driven system-app UI uses the user's authority. Only explicit,
audited OS work sets `IsSystemOperation`, which raises effective authority to
System. System operations still require the exact capability and never bypass
policy.

For the file-association document:

- normal users may read only when granted `file-associations.read`;
- writes require `file-associations.write`; and
- writes additionally require Administrator or System authority.

## Filesystem usage

An editor first reads a `SettingsDocumentSnapshot`, including its revision. It
then writes a complete candidate document and the expected revision through
`ISettingsFileProjection.WriteFileAsync`.

The write is committed only when:

- capability and authority checks pass;
- the expected revision is current; and
- whole-document syntax and schema validation passes.

Invalid or conflicting writes leave the prior content and revision untouched. A
successful write increments the revision and emits an audited change event with
app ID, user ID, and effective authority.

## Key decisions

- Canonical settings are not duplicated into ordinary file records.
- File reads and writes use the same settings service as settings UI.
- Settings files are complete human-editable `.config` documents by default;
  registered protected schemas may define an explicit format such as association
  JSON.
- Optimistic revisions prevent silent overwrite of concurrent edits.
- The capability check is never bypassed, including for System authority.
- IndexedDB remains an infrastructure detail for a later phase.

## Task list

- [x] Define canonical document and projection contracts.
- [x] Enforce capability plus authority on reads and writes.
- [x] Validate initial settings at service construction.
- [x] Validate complete file-association documents before replacement.
- [x] Add revision conflict detection.
- [x] Emit audited change events after successful commit.
- [x] Test direct service and filesystem projection consistency.
- [x] Define app/user, app/device, and roaming path definitions.
- [x] Define local app/user/device scope for per-device user window geometry.
- [x] Add schema-driven app settings validators.
- [ ] Add a persistent IndexedDB repository implementation.
- [ ] Add a full virtual filesystem router that mounts the settings projection.