WFI development preview: Windows endpoint diagnostics, local Ollama analysis, controlled remediation, pilot campaigns and BigFix central verification.

Downloads:
- WFI-Server-Setup.exe: interactive administrator setup. Prepare IIS, .NET 10 Hosting Bundle, PostgreSQL, TLS and a protected local configuration first. This installer does not install PostgreSQL/Ollama or apply production migrations.
- WFI-Agent-Setup.exe: Windows x64 client installer with documented silent deployment options.
- WFI-0.6.0-preview.2-pilot.zip: deployment bundle, SQL and installation documentation.
- SHA256SUMS.txt: integrity hashes, not publisher authentication.

The EXEs are not Authenticode-signed. Use an isolated pilot environment. Real AD/BigFix integration, Windows VM installation and HA/40k endpoint capacity acceptance remain open. See docs/verification-current.md and docs/implementation-roadmap.md; this is not a zero-defect or production certification claim.

Agent robustness: strict HTTPS origin, response/state size limits, journal consistency, protocol fallback and 429 recovery tests. Audit entries are now domain-scoped; legacy unassigned audit rows remain in the database but are hidden from the tenant web list. Apply AuditDomainScope via the reviewed SQL migration before accepting traffic.
