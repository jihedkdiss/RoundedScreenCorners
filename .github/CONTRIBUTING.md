# How to contribute
Any help towards improving Rounded Screen Corners is greatly appreciated — whether it's new features, bug fixes, or translations!

## Developing
To start developing, load the repository into Visual Studio 2022 (or your IDE of choice). Make sure that Rounded Screen Corners is closed (it's a single-instance app), and start writing!

Make sure to create a new [fork](https://github.com/jihedkdiss/RoundedScreenCorners/fork) of this repository when starting. You can then edit any code in your fork (using branches if needed).

### Build
```
dotnet build RoundedScreenCorners/RoundedScreenCorners.csproj /p:Platform=x64
```

### Build configurations
- **Debug** — local development
- **Release** — Microsoft Store upload bundle (`.msixupload`). Microsoft signs this on Partner Center, so we don't sign locally.
- **GitHub Release** — sideloadable bundle (`.msixbundle` + `.cer`) signed with the self-signed certificate whose public `.cer` is distributed with the installer.

The `GITHUB_RELEASE` compile-time symbol is defined for the `GitHub Release` configuration so code can branch on the distribution channel if ever needed (`#if GITHUB_RELEASE`).

### Signing certificate (GitHub Release only)

The `.pfx` is the **private signing key** and must never be committed. GitHub Actions reads it from the repository secret `RSC_SIGNING_PFX_BASE64` (and `RSC_SIGNING_PFX_PASSWORD` if it is password-protected). The public `RoundedScreenCorners_TemporaryKey.cer` is safe to distribute; the installer places it in the trust store so Windows can verify the sideloaded bundle.

The certificate's thumbprint is pinned in `RoundedScreenCorners.csproj` so releases use the same publisher certificate. Keep the private PFX backed up securely and update the GitHub Actions secret when rotating it.

To generate a new signing certificate (for example, after a key exposure or expiry):
```powershell
cd RoundedScreenCorners
.\generate_signing_cert.ps1
```

Update the `<PackageCertificateThumbprint>` in `RoundedScreenCorners.csproj` to the new thumbprint printed by the script. Store the new PFX as the GitHub Actions secret and announce the certificate change in release notes. Never commit the generated `.pfx`.

### Submitting changes
Once you're ready with your changes, submit a [pull request](https://github.com/jihedkdiss/RoundedScreenCorners/compare) describing what's changed, and tag any related issues.

AI usage: AI tools may be used to assist with programming, provided that you design the solution yourself and carefully review the generated code for issues and redundancy. Heavy reliance on AI without a clear understanding of what's going on with your changes will not be merged.

## Marketing assets

Screenshots and promotional artwork live in [`docs/images/`](../docs/images/). They serve GitHub, the project website, and the Microsoft Store listing.

**Microsoft Store requirements** (Partner Center → App submission → Store listings → Screenshots):
- **Minimum** 1366×768; **maximum** 2160×1440
- 16:9 aspect ratio strongly recommended
- PNG, JPEG, or JPG; under 2 MB each
- At least one required, up to ten per device family
