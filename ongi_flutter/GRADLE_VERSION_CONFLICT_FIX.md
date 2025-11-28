# Gradle Plugin Version Conflict Fix

## Problem
When building the Flutter app with Unity integration, you may encounter this error:

```
Error resolving plugin [id: 'com.android.application', version: '7.4.2', apply: false]
> The request for this plugin could not be satisfied because the plugin is already on the classpath with a different version (8.2.1).
```

## Root Cause
The issue occurs when the `unityLibrary` module has its own `settings.gradle` file that declares plugin versions different from the main Flutter project:

- **Main Flutter project** (`android/settings.gradle`): Uses AGP 8.2.1
- **Unity Library** (`android/unityLibrary/settings.gradle`): Was trying to use AGP 7.4.2

Gradle doesn't allow multiple versions of the same plugin on the classpath, causing a conflict.

## Solution
The `unityLibrary/settings.gradle` file has been renamed to `settings.gradle.bak`. This allows the Unity module to inherit plugin versions from the parent project's `settings.gradle` instead of declaring its own conflicting versions.

### File Status
- ❌ `android/unityLibrary/settings.gradle` - Should NOT exist
- ✅ `android/unityLibrary/settings.gradle.bak` - Backup of removed file
- ✅ `android/settings.gradle` - Main settings file (AGP 8.2.1)

## Verification
Check that the fix is applied:

```bash
# Linux/Mac
ls -la android/unityLibrary/settings.gradle*

# Windows
dir android\unityLibrary\settings.gradle*
```

You should see only `settings.gradle.bak`, NOT `settings.gradle`.

## If the Error Persists

1. **Clean build cache:**
   ```bash
   flutter clean
   cd android
   ./gradlew clean  # or gradlew.bat clean on Windows
   cd ..
   ```

2. **Verify no settings.gradle exists:**
   ```bash
   # It should show "file not found" or similar
   cat android/unityLibrary/settings.gradle  # Linux/Mac
   type android\unityLibrary\settings.gradle  # Windows
   ```

3. **Check for stale Unity exports:**
   If you re-export from Unity, make sure the Unity export doesn't recreate the `settings.gradle` file. If it does, delete it again.

4. **Rebuild:**
   ```bash
   flutter build apk --debug
   ```

## Related Commits
- `8aa9d48` - Fix Gradle plugin version conflict by removing unityLibrary settings.gradle

## Additional Notes
- The Unity Library module now uses `apply plugin: 'com.android.library'` directly in its `build.gradle`
- Plugin versions are centrally managed in the main `android/settings.gradle`
- This approach follows Gradle's recommended practices for multi-module projects
