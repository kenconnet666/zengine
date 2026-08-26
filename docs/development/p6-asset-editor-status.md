# P6 Asset and Editor Status

Status: P6A content-addressed asset pipeline in progress.

## Implemented

- Stable strongly typed `AssetId<T>` derived from a logical name, separate from immutable SHA-256 content revisions.
- Versioned importer registry with reversible registration.
- Import context with explicit dependency tracking.
- Hash input includes importer id/version, source, dependency contents and cooked bytes.
- Content-addressed cache with atomic data/manifest writes.
- Typed asset handle generation swap only after a successful complete import.
- Content-aware polling monitor that detects source or dependency changes and leaves the current generation unchanged for identical output.
