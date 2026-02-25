# qpdf Runtime Binaries

StormPDF uses `qpdf` as an external executable on Windows for merge and delete-page operations.

## Expected path

- Windows: `tools/qpdf/win/qpdf.exe`

## Current repository state

- Windows binary is bundled at `tools/qpdf/win/qpdf.exe` (downloaded from the official qpdf release).

macOS uses the native PDFKit engine and does not require `qpdf`.
