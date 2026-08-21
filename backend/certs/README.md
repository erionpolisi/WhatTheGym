# Extra CA certificates (local only)

If your machine sits behind a TLS-intercepting proxy (e.g. Zscaler), Docker
builds cannot reach nuget.org because the interception CA is not trusted
inside build containers.

Fix: export the intercepting root CA as PEM `.crt` files into this folder;
the Dockerfile installs everything here via `update-ca-certificates`.

PowerShell example:

```powershell
Get-ChildItem Cert:\LocalMachine\Root | Where-Object Subject -like '*Zscaler*' | ForEach-Object {
  "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($_.RawData, 'InsertLineBreaks') + "`n-----END CERTIFICATE-----" |
    Set-Content -Encoding ascii "backend/certs/$($_.Thumbprint).crt"
}
```

`.crt` files in this folder are gitignored and must never be committed.
On CI and machines without interception this folder stays empty and is a no-op.
