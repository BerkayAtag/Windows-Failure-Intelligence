# Threat Model

WFI v0.1 is an on-prem ASP.NET Core API and React console. LDAPS login produces role/domain cookie claims; analysts upload files to disk, EF stores queue jobs, and a background worker parses files into JSON evidence. src/WFI.Api/Program.cs:82-149; src/WFI.Infrastructure/AnalysisWorker.cs:24-55.

## Assets

- AD credentials transiently supplied to LDAPS (src/WFI.Infrastructure/LdapAuthenticator.cs:36-49).
- Raw log files under Storage:Path and derived evidence in Jobs.ResultJson (src/WFI.Api/Program.cs:125-138).
- Cookie signing key ring in DataProtection:Path, database records and audit history (src/WFI.Api/Program.cs:26-34).

## Trust Boundaries

- Anonymous browser to LDAP bind: CSRF, IP rate limiter, configured domain, LDAPS and AD groups (src/WFI.Api/Program.cs:73-91; src/WFI.Infrastructure/LdapAuthenticator.cs:40-69).
- Authenticated Analyst/Admin upload to filesystem/parser; extension/size/ZIP path/DTD checks constrain data (src/WFI.Api/Program.cs:115-149; src/WFI.Core/LogAnalyzer.cs:20-84).
- Viewer to domain-scoped result; query filters Domain, React renders evidence as text (src/WFI.Api/Program.cs:108-114; src/wfi-web/src/main.tsx:51).
- Worker to shared database job ownership uses lease token conditioned updates (src/WFI.Infrastructure/AnalysisWorker.cs:29-49).

## Attacker Capabilities

- Anonymous network client may submit login attempts, but needs valid AD group membership to get application access.
- Analyst may supply malicious log bytes and filenames; Viewer can read all evidence within its domain, not another domain.
- Administrator controls deployment configuration and filesystem ACLs; compromise of this privileged authority is not assumed.

## Security Objectives

- Preserve domain/RBAC isolation and credentials; do not execute uploaded files.
- Bound input processing CPU/memory/storage and preserve queue availability.
- User requests LB operation and 40,000-device capacity; neither is established by this pre-change snapshot.

## Assumptions

- Production uses PostgreSQL and HTTPS; development can use SQLite and explicit loopback demo (src/WFI.Infrastructure/WfiDb.cs:25-28; src/WFI.Api/Program.cs:47-56,92-98).
- API reads local JSON then environment overrides; worker uses default host configuration and requires identical shared storage paths (src/WFI.Api/Program.cs:19; src/WFI.Worker/Program.cs:2-4).
- At-rest encryption, ACLs, backup, real LDAPS certificate/group behavior and perimeter firewall are deployment responsibilities, not verified remotely.
- Snapshot is pre-fix v0.1. Installed vendor dependencies/build output/runtime .data are excluded from source review; partial broad coverage will be explicit.

---

## Saved Model Context

- Source: scan
- Scan: 08a0b25e-bd9b-4179-84bd-f84f19b1b6a3
- Target: Windows Failure Intelligence (WFI)
- Snapshot: codex-security-snapshot/v1:sha256:4ed1e25209960d35c9f80950bd301f7a7e822f02c9df8c53464e112f294bb76c
- Result status: completed
- Model origin: not recorded
- Model scope: not recorded
- Scan scope: .
- Scan scope description: Pre-fix WFI application source audit
