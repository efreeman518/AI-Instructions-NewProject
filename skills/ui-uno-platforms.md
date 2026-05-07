# Uno Platform UI — Platform-Specific Rules

Platform-specific build, deploy, and debugging rules for Uno (WASM, Android), plus CI requirements. Loaded during Phase 5c when an Uno UI project is in scope and a specific target needs attention.

Companion files:
- [ui-uno.md](ui-uno.md) — index + decision table
- [ui-uno-shell.md](ui-uno-shell.md) — project setup, app hosting, shell control
- [ui-uno-mvux.md](ui-uno-mvux.md) — MVUX models, routing, XAML, business services, auth

---

## WASM Debugging Ladder

When a Uno WASM build or runtime failure occurs, follow this fixed validation order before applying broader hosting rewrites:

1. **Root document:** Does the WASM host page (`index.html`) load at all? Check for 404/500 on the base URL.
2. **Package/static assets:** Are CSS, images, and app-specific static files served? Check browser network tab for 404s.
3. **`/_framework` assets:** Do `dotnet.wasm`, `blazor.boot.json` / `uno-boot.json`, and framework DLLs load? Missing `/_framework` files indicate a build or publish issue, not a routing issue.
4. **Generated bootstrap/config:** Are `appsettings.json`, `AppManifest.js`, and generated host files present and correct? Do not rewrite these unless a specific file is confirmed missing or malformed.
5. **Browser console:** Check for JS errors, CORS failures, or WASM instantiation errors. These narrow the fault to runtime init vs asset serving.

Do not apply broad hosting or routing rewrites before completing this sequence.

## WASM Host Launch Requirements

These apply to `WasmAppHost` (the dev host launched by `dotnet run` in Uno WASM projects).

### AppManifest.js — Required Bootstrap File

`Uno.UI.js` does `define(["./AppManifest.js"])` via RequireJS at startup. If the file does not exist the splash screen never clears — no JS error is visible.

Every Uno WASM project MUST contain:

```
Platforms/WebAssembly/WasmScripts/AppManifest.js
```

Minimal content:

```js
var UnoAppManifest = {
    displayName: "{AppName}",
    splashScreenColor: "transparent"
};
```

Add this file during initial scaffold. Do not leave it absent and rely on the build to generate it — it is not generated automatically.

### Working Directory Sensitivity

`WasmAppHost` resolves the hashed `package_<hash>/` directory relative to CWD. It only produces the correct `index.html` and static-asset paths when run **from the Uno project directory**, not from the solution root or a parent directory.

Always run:

```powershell
Set-Location 'src\UI\{Project}.Uno'
dotnet run
```

Never use `dotnet run --project <path>` from an unrelated working directory — the static asset paths in the output will be wrong and all `package_<hash>/*` requests will 404.

### Port Exclusion on Windows (Hyper-V / WSL)

Windows reserves port ranges for Hyper-V and WSL (shown as PID 4 owning ports in `Listen` state). These ports cannot be bound by user-space processes — attempts fail silently or with error 10013.

Diagnose before changing launchSettings:

```powershell
netsh int ipv4 show excludedportrange protocol=tcp | Select-String '5555[0-9]'
```

If a port used in `launchSettings.json` is listed, change it to a port confirmed absent from the exclusion list.

**Known-bad port**: `55552` is routinely in the excluded range on Hyper-V/Docker Desktop hosts.
**Standard local Uno endpoints**: HTTPS `https://localhost:7069`, HTTP `http://localhost:5189`.

When scaffolding a new Uno project's `launchSettings.json`, use:

```json
"applicationUrl": "https://localhost:7069;http://localhost:5189"
```

Also update the Gateway `CorsSettings.AllowedOrigins` to include both `https://localhost:7069` and `http://localhost:5189`.

### Freeing a Stuck Dev Port on Windows (from bash/Git Bash)

When `dotnet run` fails with `AddressInUseException` because a previous `WasmAppHost` process is still holding the port (crash, orphaned debugger, terminated IDE), find and kill it. In Git Bash / MSYS bash, `taskkill` requires **double-slash** flags:

```bash
# Find PID holding the port
netstat -ano | grep :7069
# Kill (bash: // not /)
taskkill //F //PID <pid>
```

A `TIME_WAIT` entry on the client side is harmless — it's a closed socket awaiting TCP drain and will clear on its own. Only `LISTENING` entries block a new bind.

Do **not** change the launch port to work around a stuck process — find and kill it instead. Rotating ports invalidates CORS config, Playwright baseURL, and bookmark URLs.

### Post-Rebuild Browser Refresh

After any rebuild, `WasmAppHost` serves a new `package_<hash>/` directory. The old hash is instantly stale. Always open a **new browser tab** to the HTTPS origin — never reload an existing tab. Existing tabs will 404 all their `package_*` asset requests until a full address-bar navigation occurs.

---

## Playwright Testing Against Uno WASM

These rules apply to `Test.PlaywrightUI` tests that drive a running Uno WASM host.

### Boot Once Per Describe Block

WASM cold-start takes 20–45 seconds. Never use the default `{ page }` Playwright fixture for Uno tests — it boots a new page per test and compounds wall-clock time.

Use `test.describe.configure({ mode: "serial" })` with a shared `BrowserContext`/`Page` created in `beforeAll`. Call `waitForApp(sharedPage)` once in `beforeAll`, then reuse `sharedPage` across all tests in the block. Re-call `waitForApp` after in-test navigation — it re-checks without a full re-boot.

```typescript
test.describe("EntityCrud", () => {
  test.describe.configure({ mode: "serial" });

  test.beforeAll(async ({ browser }) => {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    sharedPage = await context.newPage();
    await sharedPage.goto("https://localhost:7069");
    await waitForApp(sharedPage);
  });
});
```

`test.use({ viewport })` does **not** apply to a `beforeAll`-owned context. Pass viewport directly:

```typescript
context = await browser.newContext({ viewport: { width: 390, height: 844 }, ignoreHTTPSErrors: true });
```

Set `--timeout=120000` on any suite containing Uno WASM cold-start:

```json
"test:full": "npx playwright test --retries=0 --max-failures=4 --timeout=120000"
```

### Coordinate-Click for Invisible Elements

Standard Playwright visibility checks and `.click()` fail on Uno's canvas/shadow DOM. Use `getBoundingClientRect()` via `page.evaluate()` and `page.mouse.click()`. Retry in a loop since elements render asynchronously:

```typescript
for (let attempt = 0; attempt < 20; attempt++) {
  const coords = await page.evaluate(() => {
    for (const p of Array.from(document.querySelectorAll("p"))) {
      const txt = (p.textContent ?? "").trim();
      if (!txt.startsWith("E2E-")) continue; // filter by known prefix to avoid overlapping elements
      const r = p.getBoundingClientRect();
      if (r.width > 0 && r.height > 0 && r.y > 0 && r.x > 0)
        return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
    }
    return null;
  });
  if (coords) { await page.mouse.click(coords.x, coords.y); break; }
  await page.waitForTimeout(500);
}
```

Filter by a known text prefix (e.g. `"E2E-"`) to avoid hitting status chips or other `<p>` elements that overlap the target.

### Slow Router After Many Navigations

After several in-session navigations, the WASM router can lag. Increase assertion timeouts for pages loaded later in the shared-page lifecycle (use 60 s after 3+ prior navigations).

---

## Generated Code Intervention Rule

For generator-driven stacks (Uno, Kiota, Resizetizer, and similar toolchains):

- **Preserve generated conventions by default.** Do not rewrite generated bootstrap, host plumbing, or build targets unless a specific symptom proves the generated assumption is wrong.
- **Patch minimally.** Fix only the smallest confirmed incompatibility. One targeted MSBuild property override or one config fixup — not a full rewrite of the generated file.
- **Document the justification.** Every patch to generated code must carry an inline comment citing the exact symptom (e.g., `<!-- Workaround: Resizetizer 1.12.1 manifest-path bug -->`).

If you cannot identify the specific failing assumption, do not modify generated code — escalate to the engineer.

## Environment Detection Rule

When distinguishing browser, Electron, desktop-webview, or similar runtime environments, prefer **capability or runtime-object checks** over raw user-agent string matching. User-agent strings are unreliable in embedded browsers, IDE preview panes, and WebView2 hosts.

Example: check for `window.__TAURI__` or `navigator.userAgentData` rather than parsing `navigator.userAgent`.

---

## .NET for Android — Build & Deploy Rules

These rules apply when targeting any `<tfm>-android` Uno, MAUI, or bare .NET for Android target (where `<tfm>` is the project's pinned .NET TFM).

### Android SDK Discovery (Windows)

Before writing any `emulator`, `adb`, or SDK tool command, resolve the actual SDK root:

1. Check `ANDROID_HOME` / `ANDROID_SDK_ROOT` env vars.
2. If unset, check `C:\Program Files (x86)\Android\android-sdk` (Android Studio default) and `%LOCALAPPDATA%\Android\Sdk` (standalone SDK manager default).
3. Verify `emulator\emulator.exe` and `platform-tools\adb.exe` exist at the resolved path.
4. Set `ANDROID_HOME` explicitly in the shell session before invoking any SDK tools.

Do not assume SDK tools are on `PATH`.

### Embedded Assemblies for Sideloading

When building for manual ADB sideloading (`dotnet build` + `adb install`), always set in the Android TFM `PropertyGroup`:

```xml
<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>
```

The default Debug mode uses **Fast Deployment**, which expects the .NET tooling to push managed assemblies to the device separately after install. A bare APK installed without that push crashes immediately with _"No assemblies found … Assuming this is part of Fast Deployment"_. Lock this property into the project file permanently for any project that supports manual sideloading — do not rely on a command-line override.

### Emulator Host Networking

Apps running on the Android emulator that call local backend services must use `10.0.2.2` in place of `localhost` / `127.0.0.1`. Gate this with a compile-time check so WASM/desktop builds continue to use `localhost`:

```csharp
#if __ANDROID__
    const string LocalHost = "10.0.2.2";
#else
    const string LocalHost = "localhost";
#endif
```

Quick validation from emulator shell (no running service required):
```bash
adb shell "echo TEST | nc 10.0.2.2 <PORT>"
```

### Activity Class Name Discovery

.NET for Android generates a CRC-based Java class name for activities (e.g., `crc64<hash>.MainActivity`) that differs from the C# class name. Do not guess it from source.

When launching via `adb shell am start -n`, first discover the registered activity:

```bash
adb shell dumpsys package <package-id> | grep -A 3 "MAIN"
```

Use the class name from the output — the generated name cannot be predicted from C# source alone.

---

## Known Build Issues / Workarounds

### Resizetizer File Naming Rules

Uno.Resizetizer requires asset filenames to be **lowercase**, containing only alphanumeric characters or underscores, and starting/ending with a letter. Files like `SplashScreen.svg` or `my-icon.png` will fail the build.

### UnoSplashScreen WASM Build Failure (Resizetizer 1.12.1)

**Symptom:** Adding `<UnoSplashScreen Include="Assets\splashscreen.svg" />` causes `GenerateWasmSplashAssets` to fail silently on WASM. Even without `UnoSplashScreen`, ShellTask may crash with `DirectoryNotFoundException` on clean builds.

**Root cause:** Resizetizer line 529 constructs a fallback PWA manifest path using `GetFileName($(WasmPWAManifestFile))`. When `WasmPWAManifestFile` is unset, `GetFileName("")` returns empty, producing a bare directory path (`unoresizetizer\`). MSBuild `Exists()` returns true for directories, so `UnoResizetizerPwaManifest` gets set to a directory. ShellTask then calls `File.ReadAllText` on that directory and crashes.

**Workaround:** Add this target to the UI `.csproj`:

```xml
<!-- Workaround: Resizetizer 1.12.1 sets WasmPWAManifestFile to a directory
     path when no UnoSplashScreen is configured. Clear it so ShellTask doesn't
     call File.ReadAllText on a directory. -->
<Target Name="_FixWasmPwaManifestPath"
        BeforeTargets="GenerateUnoWasmAssets"
        AfterTargets="ProcessResizedImagesWasm"
        Condition="$(TargetFramework.Contains('browserwasm'))">
  <PropertyGroup>
    <WasmPWAManifestFile Condition="'$(WasmPWAManifestFile)' != '' AND !$([System.IO.File]::Exists('$(WasmPWAManifestFile)'))"></WasmPWAManifestFile>
  </PropertyGroup>
</Target>
```

**Important:** Do NOT place standalone splash screen asset files (`splashscreen.png`, `splashscreen.svg`) in Assets without a corresponding `<UnoSplashScreen>` item — the resizetizer will produce duplicate static web asset conflicts.

### ExtendedSplashScreen vs UnoSplashScreen

- `UnoSplashScreen` = native splash (Android/iOS) generated by Resizetizer. Broken on WASM in 1.12.1.
- `ExtendedSplashScreen` = Uno Toolkit XAML control in `ShellControl.xaml` for the in-app loading screen. This is the recommended WASM splash approach.
- The "Didn't find UnoSplashScreen" warning from Resizetizer is **harmless** — it just means no native splash is configured.

## CI Requirements

When the solution includes a Uno WASM project, CI workflows must install the `wasm-tools` workload before build:

```yaml
- name: Install required workloads
  run: dotnet workload install wasm-tools
```

Without this, the build fails with `UNOWA0001: Native WebAssembly assets were detected, but the wasm-tools workload could not be located.`

Add this step after `actions/setup-dotnet@v4` and before `dotnet restore`. See [cicd.md](cicd.md) for the full CI template.
