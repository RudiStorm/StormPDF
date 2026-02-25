# Ghostscript Runtime Binaries

StormPDF uses Ghostscript for `Optimize (Lossy)`.

## Expected Windows paths

- `tools/ghostscript/win/gswin64c.exe`
- `tools/ghostscript/win/gswin32c.exe` (optional fallback)

At runtime, StormPDF checks bundled binaries first, then falls back to Ghostscript on `PATH`.

## Auto-download during Windows build

When building the Windows target, if `tools/ghostscript/win/gswin64c.exe` is missing, the build tries to:

1. detect `winget`
2. install Ghostscript (`ArtifexSoftware.GhostScript`)
3. copy the latest `gswin64c.exe` into `tools/ghostscript/win/`

If this step cannot complete, the build continues and prints a warning with the manual path.
