# Security Review: Windows Failure Intelligence (WFI)

## Scope

Pre-fix WFI application source audit

- Scan mode: repository
- Target kind: directory_snapshot
- Target ID: target_sha256_f9cdae600fc6cde8b175de8125b075a34cb974c9b85bf97eadc128f84990fe89
- Snapshot digest: codex-security-snapshot/v1:sha256:4ed1e25209960d35c9f80950bd301f7a7e822f02c9df8c53464e112f294bb76c
- Inventory strategy: directory
- Included paths: .
- Excluded paths: none

Limitations and exclusions:
- Vendor source and generated output excluded; no live corporate systems tested.
- Excluded \*\*/node_modules/\*\*: Vendor source not audited.
- Excluded \*\*/bin/\*\*, \*\*/obj/\*\*, artifacts/\*\*, \*\*/.data/\*\*: Generated outputs and runtime state excluded from source review.

### Scan Summary

| Field | Value |
| --- | --- |
| Scan outcome | completed |
| Reportable findings | 2 |
| Severity mix | medium: 2 |
| Confidence mix | high: 2 |
| Coverage | partial |
| Validation mode | offline source review |

Canonical artifacts: `scan-manifest.json`, `findings.json`, and `coverage.json`. This report is a deterministic projection of those files.

## Threat Model

WFI v0.1 is an on-prem ASP.NET Core API and React console. LDAPS login produces role/domain cookie claims; analysts upload files to disk, EF stores queue jobs, and a background worker parses files into JSON evidence. src/WFI.Api/Program.cs:82-149; src/WFI.Infrastructure/AnalysisWorker.cs:24-55.

### Assets

- AD credentials transiently supplied to LDAPS (src/WFI.Infrastructure/LdapAuthenticator.cs:36-49).
- Raw log files under Storage:Path and derived evidence in Jobs.ResultJson (src/WFI.Api/Program.cs:125-138).
- Cookie signing key ring in DataProtection:Path, database records and audit history (src/WFI.Api/Program.cs:26-34).

### Trust Boundaries

- Anonymous browser to LDAP bind: CSRF, IP rate limiter, configured domain, LDAPS and AD groups (src/WFI.Api/Program.cs:73-91; src/WFI.Infrastructure/LdapAuthenticator.cs:40-69).
- Authenticated Analyst/Admin upload to filesystem/parser; extension/size/ZIP path/DTD checks constrain data (src/WFI.Api/Program.cs:115-149; src/WFI.Core/LogAnalyzer.cs:20-84).
- Viewer to domain-scoped result; query filters Domain, React renders evidence as text (src/WFI.Api/Program.cs:108-114; src/wfi-web/src/main.tsx:51).
- Worker to shared database job ownership uses lease token conditioned updates (src/WFI.Infrastructure/AnalysisWorker.cs:29-49).

### Attacker Capabilities

- Anonymous network client may submit login attempts, but needs valid AD group membership to get application access.
- Analyst may supply malicious log bytes and filenames; Viewer can read all evidence within its domain, not another domain.
- Administrator controls deployment configuration and filesystem ACLs; compromise of this privileged authority is not assumed.

### Security Objectives

- Preserve domain/RBAC isolation and credentials; do not execute uploaded files.
- Bound input processing CPU/memory/storage and preserve queue availability.
- User requests LB operation and 40,000-device capacity; neither is established by this pre-change snapshot.

### Assumptions

- Production uses PostgreSQL and HTTPS; development can use SQLite and explicit loopback demo (src/WFI.Infrastructure/WfiDb.cs:25-28; src/WFI.Api/Program.cs:47-56,92-98).
- API reads local JSON then environment overrides; worker uses default host configuration and requires identical shared storage paths (src/WFI.Api/Program.cs:19; src/WFI.Worker/Program.cs:2-4).
- At-rest encryption, ACLs, backup, real LDAPS certificate/group behavior and perimeter firewall are deployment responsibilities, not verified remotely.
- Snapshot is pre-fix v0.1. Installed vendor dependencies/build output/runtime .data are excluded from source review; partial broad coverage will be explicit.

## Findings

| Finding | Severity | Confidence | Detailed write-up |
| --- | --- | --- | --- |
| [Authorization redaction leaves bearer credentials in evidence](#finding-1) | medium | high | inline below |
| [Nested XML Events cause uncancellable quadratic traversal](#finding-2) | medium | high | inline below |

### Confidence Scale

| Label | Meaning |
| --- | --- |
| high | Direct evidence supports the finding with no material unresolved blocker. |
| medium | Evidence supports a plausible issue, but material runtime or reachability proof remains. |
| low | Evidence is incomplete and the item is retained only for explicit follow-up. |

<a id="finding-1"></a>

### [1] Authorization redaction leaves bearer credentials in evidence

| Field | Value |
| --- | --- |
| Severity | medium |
| Confidence | high |
| Confidence rationale | Regex capture stops at whitespace; source persists masked output directly. |
| Category | Incomplete sensitive data redaction |
| CWE | CWE-200 |
| Affected lines | src/WFI.Core/LogAnalyzer.cs:14-15, src/WFI.Core/LogAnalyzer.cs:57 |

#### Summary

The masking expression consumes only the first whitespace-delimited word. Authorization: Bearer EXAMPLE_CREDENTIAL retains EXAMPLE_CREDENTIAL in persisted evidence visible to domain viewers.

#### Root Cause

Masking treats scheme-based authorization as a single token and truncates before masking.

**Single-token replacement** — `src/WFI.Core/LogAnalyzer.cs:14`

Only Bearer is matched; the following credential is retained.

```
private static readonly Regex Secret = new(@"(?i)\b(password|pwd|token|authorization|secret)\s*[:=]\s*([^\s<;]+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
```

#### Validation

Validation outcomes are recorded below.

Validation method: Source review and deterministic regex reasoning

- **Status:** validated

Counterevidence and remaining uncertainty:
- Results require authentication and remain domain-scoped; raw logs are outside webroot.

Limitations:
- No real credentials used.

#### Dataflow

LogAnalyzer.cs:57 -\> AnalysisWorker ResultJson -\> Program.cs:108-114.

#### Reachability

Credential-bearing uploaded log matches rule -\\\> partial masking -\\\> ResultJson -\\\> domain Viewer.

Preconditions:
- Uploaded evidence contains a real authorization credential and matches an analysis rule.

#### Severity

**Medium** — Requires credentials present in uploaded logs; domain-wide evidence readers can obtain the retained secret.

Additional runtime or deployment evidence could raise or lower this severity.

#### Remediation

Mask complete authorization scheme and credential, quoted secrets and structured values before truncation; add synthetic regressions.

<a id="finding-2"></a>

### [2] Nested XML Events cause uncancellable quadratic traversal

| Field | Value |
| --- | --- |
| Severity | medium |
| Confidence | high |
| Confidence rationale | Direct source data flow and repeated descendant enumeration confirmed independently and by parent. |
| Category | Unbounded parser computational complexity |
| CWE | CWE-400 |
| Affected lines | src/WFI.Core/LogAnalyzer.cs:34-37 |

#### Summary

An authenticated Analyst can upload nested Event elements without Provider or EventID. Each Event scans all descendants twice, monopolizing a worker despite its cancellation deadline.

#### Root Cause

Unbounded XML depth combined with descendant searches inside a descendant loop.

**Repeated descendant traversal** — `src/WFI.Core/LogAnalyzer.cs:34`

Each matching element then enumerates descendants for Provider and EventID; nested Event input produces quadratic traversal with no cancellation check.

```
foreach (var evt in doc.Descendants().Where(e => e.Name.LocalName == "Event"))
```

#### Validation

Validation outcomes are recorded below.

Validation method: Independent offline source review and parent confirmation

- **Status:** validated

Counterevidence and remaining uncertainty:
- DTD prohibited and document capped at 20 MB; these do not bound nested traversal complexity.

Limitations:
- No production exploitation or timing benchmark run in this baseline scan.

#### Dataflow

Program.cs:115-149 stores uploaded XML; AnalysisWorker calls LogAnalyzer; LogAnalyzer.cs:34-37 recursively scans descendants.

#### Reachability

Analyst upload -\\\> raw file -\\\> background XML parsing -\\\> sustained worker CPU.

Preconditions:
- Valid Analyst/Admin login and CSRF token.

#### Severity

**Medium** — Requires an authorized Analyst; availability impact can affect the shared analysis queue.

Additional runtime or deployment evidence could raise or lower this severity.

**Impact assessment:** Queue availability degraded; expired leases may cause repeated processing.

#### Remediation

Reject excessive XML depth and nested Event elements; parse once with bounded streaming state and cancellation checks.

## Reviewed Surfaces

| Surface | Risk Area | Outcome | Notes |
| --- | --- | --- | --- |
| Application source security boundaries | not recorded | Reported | Reviewed LDAP filter escaping, strict TLS, RBAC/domain queries, cookie CSRF, ZIP constraints, worker leases and React text rendering. Two parser findings validated. |

## Open Questions And Follow Up

- Actual AD/LDAPS, production PostgreSQL, shared storage, load balancer and 40000-client load testing require deployment environment.
  - Follow-up prompt: Review deferred unit environment-validation and close its stated proof gap.
