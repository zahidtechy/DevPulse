# Datavanched Developer Work Platform

**Datavanched** developer work reporting is built with **.NET 10**, **Blazor WebAssembly**, and **Clean Architecture**. It integrates multiple **ClickUp** workspaces via API tokens for unified monthly developer performance dashboards and stakeholder exports.

## Solution Structure

```
DevPulse/
├── src/
│   ├── DevPulse.Domain/           # Entities, enums (core business model)
│   ├── DevPulse.Shared/           # DTOs, Result pattern, API contracts
│   ├── DevPulse.Application/      # Use cases, interfaces, application services
│   ├── DevPulse.Infrastructure/   # EF Core, ClickUp API client, repositories
│   ├── DevPulse.Server/           # ASP.NET Core Web API + Blazor host
│   └── DevPulse.Client/           # Blazor WebAssembly UI
└── DevPulse.sln
```

## Architecture & Design Patterns

| Pattern | Usage |
|---------|--------|
| **Clean Architecture** | Domain → Application → Infrastructure → Presentation |
| **Repository Pattern** | `IClickUpAccountRepository` abstracts data access |
| **Result Pattern** | Explicit success/failure without exceptions for business rules |
| **Options / DI** | Constructor injection throughout |
| **Typed HttpClient** | `ClickUpApiClient` with token-per-request for multi-account support |
| **Data Protection** | Encrypts ClickUp access tokens at rest |

## Phase 1 Features (Implemented)

- Register multiple ClickUp API tokens (one per workspace)
- Validate token against workspace on create/update
- Test connection per account
- List workspace members
- Query filtered tasks by month and assignees
- Blazor UI for account management

## Phase 2 Features (Implemented)

- Developer registry with cross-workspace ClickUp user ID mappings
- Sync developers automatically from connected ClickUp workspaces (by email)
- Unified developer-centric monthly task reports across all workspaces
- Productivity summary: task counts, workspace breakdown, average completion time
- Blazor UI for developer management and report generation
- Seeded demo developers, workspaces, and task data for management previews (startup seed)

## Getting Started

### Prerequisites

- .NET 10 SDK
- ClickUp Personal Access Token(s)
- ClickUp Workspace ID(s)

### Run the application

```bash
cd e:\Clickup
dotnet run --project src/DevPulse.Server
```

Open the URL shown in the terminal (typically `http://localhost:5080`).

> **Important:** Run `DevPulse.Server`, not `DevPulse.Client`. This is a hosted Blazor WASM app — the server hosts both the API and the UI.

### Get your ClickUp Workspace ID

```bash
curl -H "Authorization: YOUR_TOKEN" https://api.clickup.com/api/v2/team
```

Use the `id` field from the response as the **Workspace ID** when adding an account.

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/clickup/accounts` | List all configured accounts |
| POST | `/api/clickup/accounts` | Add and validate a new account |
| GET | `/api/clickup/accounts/{id}` | Get account by ID |
| PUT | `/api/clickup/accounts/{id}` | Update account |
| DELETE | `/api/clickup/accounts/{id}` | Delete account |
| GET | `/api/clickup/accounts/{id}/test` | Test token connection |
| GET | `/api/clickup/accounts/{id}/members` | List workspace members |
| GET | `/api/clickup/accounts/{id}/workspaces` | List authorized workspaces |
| POST | `/api/clickup/accounts/{id}/tasks/query` | Query filtered tasks |
| GET | `/api/developers` | List all developers |
| POST | `/api/developers` | Create a developer |
| GET | `/api/developers/{id}` | Get developer by ID |
| PUT | `/api/developers/{id}` | Update developer |
| DELETE | `/api/developers/{id}` | Delete developer |
| POST | `/api/developers/{id}/mappings` | Add ClickUp workspace mapping |
| POST | `/api/developers/sync` | Sync developers from all ClickUp accounts |
| POST | `/api/reports/developer-tasks` | Generate unified developer task report |

### Example: Create account

```json
POST /api/clickup/accounts
{
  "name": "Client A Production",
  "workspaceId": "9012345678",
  "accessToken": "pk_your_clickup_token"
}
```

## Security Notes

- API tokens are **never** sent to the Blazor client after storage
- Tokens are encrypted using ASP.NET Core **Data Protection**
- Protected API calls use short-lived JWT bearer access tokens
- Browser refresh tokens are rotated, stored as SHA-256 hashes, and transported only in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie
- The hosted Blazor client and API are same-origin; browser CORS is intentionally disabled
- Access tokens are signed with **HS256** using a symmetric key supplied through user secrets (Development) or an IIS app-pool environment variable (Production). The key is never committed. See [JWT Signing Configuration](#jwt-signing-configuration)
- Certificate-based **RS256** signing remains available as a selectable mode and is the preferred option for future production use

## JWT Signing Configuration

Access tokens are currently signed with **HS256** using a symmetric Base64 key. No signing secret is committed to source control. `src/DevPulse.Server/appsettings.json` contains the signing mode and an empty key placeholder only.

### Signing modes

| `Jwt:SigningMode` | Key source | Algorithm | Use |
|---|---|---|---|
| `Symmetric` | `Jwt:SigningKey` (Base64, at least 32 bytes) | `HS256` | Current Development and Production |
| `Certificate` | `Jwt:SigningCertificateThumbprint` in the configured Windows certificate store | `RS256` | Recommended for future Production |
| `DevelopmentEphemeral` | Generated in memory at startup | `HS256` | Local Development only |
| `Auto` | Certificate if a thumbprint is set, otherwise symmetric if a key is set, otherwise ephemeral in Development | `HS256` / `RS256` | Backward compatibility |

`appsettings.json` sets `SigningMode` to `Symmetric` and leaves `SigningKey` empty.

- In **Development**, an empty `SigningKey` falls back to a random in-memory key and logs a warning. Tokens issued before a restart become invalid.
- Outside Development, the API fails at startup when `SigningKey` is missing, is not valid Base64, or is shorter than 32 bytes.
- Only the active key's algorithm is accepted during validation (`ValidAlgorithms`), which prevents algorithm-confusion attacks.

### Generate a key

Run in PowerShell and store the output securely:

```powershell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

A 32-byte key produces a 256-bit HS256 secret.

### Local development

Use .NET user secrets so the key stays out of source control:

```powershell
dotnet user-secrets init --project src/DevPulse.Server
dotnet user-secrets set "Jwt:SigningKeyId" "devpulse-local" --project src/DevPulse.Server
dotnet user-secrets set "Jwt:SigningKey" "<BASE64_KEY>" --project src/DevPulse.Server
```

`Jwt:SigningMode` is already `Symmetric` in `appsettings.json`. Restart the server after changing the key. If you skip this step, Development uses the ephemeral key.

### Production

> Configure the key **before** deploying this change. Production intentionally fails at startup when `SigningMode` is `Symmetric` and `SigningKey` is missing.

Set the key on the **DevPulse IIS app pool**, not at machine level, so other applications on the shared server are unaffected:

```powershell
$key = '<BASE64_KEY>'

& "$env:windir\system32\inetsrv\appcmd.exe" set config `
  -section:system.applicationHost/applicationPools `
  /+"[name='DevPulsePool'].environmentVariables.[name='Jwt__SigningKey',value='$key']" `
  /commit:apphost

& "$env:windir\system32\inetsrv\appcmd.exe" set config `
  -section:system.applicationHost/applicationPools `
  /+"[name='DevPulsePool'].environmentVariables.[name='Jwt__SigningKeyId',value='devpulse-v1']" `
  /commit:apphost
```

Then recycle only the DevPulse app pool:

```powershell
Import-Module WebAdministration
Restart-WebAppPool -Name 'DevPulsePool'
```

Do **not** place the key in `appsettings.json` or `appsettings.Production.json`. The deployment workflow replaces the site directory, and committed JSON is visible to anyone with repository access. Environment variables override JSON configuration: `Jwt__SigningKey` maps to `Jwt:SigningKey`.

### Key rotation

Changing `Jwt:SigningKey` invalidates existing **access tokens only**. Access tokens are short-lived (10 minutes by default). Browser refresh tokens are opaque values hashed in the database and are unaffected, so clients recover automatically through `/api/auth/refresh`. Changing `Jwt:SigningKeyId` changes the `kid` header but does not, by itself, invalidate existing tokens.

### Certificate signing for future production

The certificate implementation is preserved behind `IJwtSigningKeyProvider`. To switch later:

1. Provision an RSA certificate with a private key in `LocalMachine/My`.
2. Grant the DevPulse app-pool identity read access to the private key.
3. Set `Jwt__SigningMode=Certificate` and `Jwt__SigningCertificateThumbprint=<THUMBPRINT>` on the DevPulse app pool.
4. Recycle only the DevPulse app pool.

Tokens will then be signed with **RS256**. Certificate signing is preferred for production because the private key stays in the Windows certificate store instead of being readable as a configuration value. See `src/DevPulse.Infrastructure/Security/CertificateJwtSigningKeyProvider.cs`.

Because `appsettings.json` sets `SigningMode` to `Symmetric`, an existing `Jwt__SigningCertificateThumbprint` environment variable is ignored until `Jwt__SigningMode` is explicitly set to `Certificate` (or `Auto`, which prefers the certificate when a thumbprint is present).

> Anyone who knows the symmetric key can create valid access tokens. Treat it as a production secret, restrict who can read the app-pool configuration, and rotate it immediately if it is exposed.

## Next Phases

1. KPI dashboard charts and scheduled report delivery
2. Production observability and operational hardening

## License

MIT
