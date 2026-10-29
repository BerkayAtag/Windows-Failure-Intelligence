WFI development preview: Windows endpoint diagnostics, local Ollama analysis, controlled remediation, pilot campaigns and BigFix central verification.

Downloads:
- WFI-Server-Setup.exe: Next / Install / Finish wizard for a NEW single-server pilot. Automatically downloads hash-pinned prerequisites, enables IIS, installs Hosting Bundle, creates dedicated loopback PostgreSQL/Ollama services and prepares schema during installation. Internet required, several GB download. Set local:admin password during setup; configure LDAP later. Real Windows VM installation acceptance remains open.
- Client setup is included inside the server distribution and downloaded from the authenticated WFI console; no separate client EXE release asset.
- WFI-0.6.0-preview.4-pilot.zip: deployment bundle, SQL and installation documentation.
- SHA256SUMS.txt: integrity hashes, not publisher authentication.

The EXEs are not Authenticode-signed. Use an isolated pilot environment. Real AD/BigFix integration, Windows VM installation and HA/40k endpoint capacity acceptance remain open. See docs/verification-current.md and docs/implementation-roadmap.md; this is not a zero-defect or production certification claim.

Agent robustness: strict HTTPS origin, response/state size limits, journal consistency, protocol fallback and 429 recovery tests. Audit entries are now domain-scoped; legacy unassigned audit rows remain in the database but are hidden from the tenant web list. Apply AuditDomainScope via the reviewed SQL migration before accepting traffic.

Red/white light theme is the new-session default; dark theme remains available. Repair campaigns are labeled Onarım Otomasyonu. Both installers include a red WFI icon and register Publisher Berkay Atağ in Programs and Features. Registry publisher is not Authenticode signing. Removal preserves configuration, identity, evidence, keys and protected uninstall helpers. Reparse ancestors and overlapping application/evidence/key paths are rejected.
