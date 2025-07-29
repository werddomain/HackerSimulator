# Analysis Plan: Application Discovery for Multiple Application Types

## Current State
- The application architecture uses `AppAttribute` to mark classes as applications
- `ApplicationType` enum defines four types of applications:
  - WindowedApplication (default)
  - CommandLineTool
  - SystemService
  - SystemApplication
- `ApplicationDiscoveryService` scans assemblies for classes with `AppAttribute`
- Applications are registered with `ApplicationManager` during discovery
- Application metadata is stored in the `ApplicationRegistry`
- Manifests are saved to `/usr/share/applications` directory

## Existing Limitations
- While `AppAttribute` has a `Type` property that supports all application types, the discovery and registration process doesn't handle them differently
- There's no categorization or filtering by application type in the registry
- Type-specific metadata isn't fully utilized during registration
- The current discovery mechanism doesn't account for the differences between window applications, service applications, and command-line applications

## Proposed Changes

### 1. Update ApplicationDiscoveryService
- Enhance discovery to categorize applications by type
- Add type-specific validation during discovery
- Add logging for application types

### 2. Update ApplicationRegistry
- Add methods to get applications by type
- Enhance search to include type-based filtering
- Update the statistics tracking to include application type

### 3. Create Type-Specific Application Collections
- Create collections for different application types
- Add methods to get all window applications, services, and commands

## Implementation Strategy

1. Update `ApplicationDiscoveryService.DiscoverApplicationsAsync()` to categorize applications by type
2. Add type-specific application counts to logging
3. Add new methods to `IApplicationRegistry` for type-based filtering
4. Implement these methods in `ApplicationRegistry`
5. Update any UI components that list applications to support type filtering

## Testing Strategy
- Test discovery of each application type
- Test filtering by application type
- Test launching applications of each type
- Verify that type-specific metadata is correctly stored and retrieved
