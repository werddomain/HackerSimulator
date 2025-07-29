# Authentication Session Persistence

This document describes how the authentication service persists user sessions and allows the session timeout to be configured.

## Purpose
- Maintain login state without a backend by storing a JWT-like token in `sessionStorage`.
- Refresh the token on user activity to keep the session alive.
- Allow users to adjust the session lifetime via the Settings application.

## Architecture
- **AuthService** – Generates and validates tokens. Values for `tokenExpirationMinutes` and `tokenRefreshThresholdMinutes` are loaded from `authsettingsapp` user settings.
- **auth-activity-tracker.js** – Tracks mouse, keyboard and navigation events and calls back to `AuthService` for timed refresh.
- **AuthSettingsApp** – Placeholder application so session settings can be edited through the `Settings` app.

## Usage
1. Open the `Settings` application and select **Auth Settings**.
2. Add or modify `tokenExpirationMinutes` and `tokenRefreshThresholdMinutes`.
3. On next login, `AuthService` will apply these values when creating and refreshing tokens.

## Key Decisions
- Token data is stored as a Base64 encoded JSON string acting as a lightweight JWT.
- Session settings are user specific and stored under `/home/{user}/.config/authsettingsapp.config`.
- Activity tracking avoids unnecessary refreshes using a configurable threshold.

## Task List
- [x] Register `SettingsService` and inject service provider into `AuthService`.
- [x] Load token expiration and refresh threshold from user settings.
- [x] Use loaded values when generating and refreshing tokens.
- [x] Provide `AuthSettingsApp` for editing session options.
- [x] Document the feature in this file.
