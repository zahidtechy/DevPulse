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
- For production, use Azure Key Vault and enable authentication (Azure AD / JWT)

## Next Phases

1. KPI dashboard charts and scheduled report delivery
2. Authentication (Azure AD / JWT) hardening for production

## License

MIT
