# Publishing Khorshid to the Microsoft Store

Khorshid ships to the Store as an **MSIX** package. The Store signs it for you, so you don't need a code-signing certificate, and users get automatic updates and no SmartScreen warning.

## 1. One-time setup

1. **Developer account.** Sign up at [Partner Center](https://partner.microsoft.com/dashboard/registration) as an *Individual* developer. Check the page for the current registration fee.
2. **Reserve the name.** In Partner Center choose **Apps and games → New product → MSIX or PWA app** and reserve a name, for example `Khorshid` or `Khorshid - تقویم شمسی` if that one is taken.
3. **Copy the identity.** Open the new app → **Product management → Product identity** and note these three values:
   - `Package/Identity/Name` (looks like `12345FarzadFarazmand.Khorshid`)
   - `Package/Identity/Publisher` (looks like `CN=1A2B3C4D-…`)
   - `Package/Properties/PublisherDisplayName`
4. **Packaging tools.** `build-msix.ps1` needs `makeappx.exe` and `makepri.exe`. Either install the Windows SDK, or unpack the small NuGet build-tools package into the git-ignored `.tools` folder:

   ```powershell
   Invoke-WebRequest https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools -OutFile buildtools.zip
   Expand-Archive buildtools.zip .tools\buildtools; Remove-Item buildtools.zip
   ```

## 2. Build the package

From the repository root, with your values from step 3:

```powershell
.\packaging\build-msix.ps1 `
    -IdentityName 'YOUR.IDENTITY.NAME' `
    -Publisher 'CN=YOUR-PUBLISHER-ID' `
    -PublisherDisplayName 'YOUR PUBLISHER DISPLAY NAME' `
    -DisplayName 'Khorshid' `
    -Version 1.0.0.0
```

This writes `dist\Khorshid_1.0.0.0.msix`. `DisplayName` must be the name you reserved. Each new Store submission needs a higher version (`1.0.1.0`, `1.1.0.0`, …); keep the last part `0`, because the Store reserves it.

## 3. Submit

In Partner Center, open the app and **Start submission**:

| Section | What to enter |
| --- | --- |
| **Pricing and availability** | Free; markets of your choice |
| **Properties** | Category *Productivity* (or *Utilities & tools*). Privacy policy: not required, because the app collects no data |
| **Age ratings** | Complete the questionnaire; Khorshid has no user content, chat, ads or purchases, so it rates for everyone |
| **Packages** | Upload `dist\Khorshid_<version>.msix` (unsigned is fine) |
| **Store listings** | Add **English (United States)** and **Persian (Iran)**; text is in [`store-listing.md`](store-listing.md). Screenshots: `docs/screenshot-light.png` and `docs/screenshot-dark.png` (1920×1080). Store logo: `assets/logo.png` |
| **Submission options → restricted capabilities** | Explain `runFullTrust`: *"Khorshid is a classic Win32 (.NET Framework) desktop app packaged with MSIX. It needs full trust to show its system tray icon and desktop widget."* |

Certification usually takes from a few hours to three business days.

## Testing the package locally (optional)

Windows only installs signed packages, so for a local test:

```powershell
.\packaging\build-msix.ps1 -Sign     # self-signed with CN=Khorshid Dev
```

Then, **once and as administrator**, trust the test certificate and install:

```powershell
Import-Certificate -FilePath dist\Khorshid-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage dist\Khorshid_1.0.0.0.msix
```

Or turn on **Settings → System → For developers → Developer Mode** and register the unpacked folder without signing:

```powershell
Add-AppxPackage -Register dist\msix\AppxManifest.xml
```

Uninstall the test version from **Settings → Apps** before installing the Store version.

## How the Store version differs

- **Start with Windows** uses the package's `StartupTask` (declared in `AppxManifest.xml`, off by default). In the packaged app, the tray menu item opens **Settings → Apps → Startup**, where the user switches it on. The plain exe uses the registry Run key instead.
- **Settings** in `%APPDATA%\Khorshid` are redirected into the package's private storage and removed on uninstall.
