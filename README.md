# AI Code Review Agent

Paste a GitHub pull request URL and get structured review findings — quality, security,
testing and maintainability — each with severity, confidence and a recommended fix.

## Stack

| Layer | Technology |
| --- | --- |
| API | .NET 10 Web API (controllers, OpenAPI) |
| Orchestration | Microsoft Semantic Kernel |
| LLM | Azure OpenAI (chat completion, JSON mode) |
| Tool layer | `GitHubRepositoryPlugin` kernel functions (PR metadata, changed files, file patch) |
| Repo access | GitHub REST API v3 (anonymous; optional PAT) |
| Web | React 19 + TypeScript + Vite + Tailwind CSS v4 |
| Containers | Dockerfiles + docker compose (nginx serves the SPA and proxies `/api`) |
| Storage | In-memory (last 50 reviews) |

## Project layout

```
CodeReviewAgent/
├─ src/CodeReviewAgent.Api/       .NET 10 Web API
│  ├─ Controllers/ReviewsController.cs
│  ├─ Services/                   GitHub client, SK engine, heuristic engine, URL parser
│  ├─ Models/                     PR context and review result contracts
│  └─ Options/                    GitHub and Azure OpenAI settings
├─ src/web/                       React + TypeScript SPA
├─ tests/CodeReviewAgent.Api.Tests/
├─ docker-compose.yml
└─ NuGet.config                   pins restore to nuget.org
```

## Review engines

1. **Semantic Kernel engine** (`semantic-kernel:<deployment>`) — used when Azure OpenAI is
   configured. The kernel registers the GitHub plugin so the model can pull extra context
   (`get_pull_request_metadata`, `list_changed_files`, `get_file_patch`) while reviewing, and
   returns a strict JSON payload.
2. **Heuristic engine** (`heuristic-rules`) — deterministic rule scan used when Azure OpenAI is
   not configured, or as an automatic fallback if the LLM call fails. Detects hard-coded
   credentials, SQL string concatenation, dynamic `eval`/`exec`, empty catch blocks, disabled TLS
   verification, ad-hoc console logging, TODO/FIXME markers, oversized file changes and missing
   test coverage.

## Configuration

`src/CodeReviewAgent.Api/appsettings.json`, environment variables or user secrets:

| Setting | Env var | Notes |
| --- | --- | --- |
| `GitHub:Token` | `GitHub__Token` | Optional. Anonymous = 60 requests/hour, public repos only |
| `GitHub:MaxFiles` | `GitHub__MaxFiles` | Default 30, max 100 |
| `GitHub:MaxPatchCharsPerFile` | `GitHub__MaxPatchCharsPerFile` | Default 8000 |
| `AzureOpenAI:Endpoint` | `AzureOpenAI__Endpoint` | e.g. `https://my-aoai.openai.azure.com/` |
| `AzureOpenAI:ApiKey` | `AzureOpenAI__ApiKey` | Keep out of source control |
| `AzureOpenAI:DeploymentName` | `AzureOpenAI__DeploymentName` | Default `gpt-4o` |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0` | Defaults allow `localhost:5173` |

Local development without committing secrets:

```powershell
cd src\CodeReviewAgent.Api
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://my-aoai.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<key>"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
```

## Run locally

```powershell
# API on http://localhost:5080
dotnet run --project src\CodeReviewAgent.Api

# SPA on http://localhost:5173 (proxies /api to the API)
cd src\web
npm install
npm run dev
```

## Run with Docker

```powershell
copy .env.example .env   # then fill in the values you have
docker compose up --build
```

- Web UI: http://localhost:8080
- API: http://localhost:5080 (health at `/health`)

## Tests

```powershell
dotnet test
```

19 tests cover PR URL parsing, heuristic rules, severity ordering and API behaviour
(health endpoint, invalid URL handling, unknown review id).

## API

### `POST /api/reviews`

```json
{ "pullRequestUrl": "https://github.com/owner/repo/pull/123", "maxFiles": 30 }
```

Response (abridged):

```json
{
  "reviewId": "b1c2...",
  "summary": "…",
  "recommendation": "request_changes",
  "overallConfidence": 78,
  "categoryScores": [{ "category": "Security", "score": 60, "summary": "1 issue(s) detected." }],
  "findings": [
    {
      "id": "3fa21b90",
      "file": "src/Config.cs",
      "line": 42,
      "category": "Security",
      "severity": "Critical",
      "title": "Possible hard-coded credential",
      "description": "…",
      "recommendedFix": "Move the value to configuration or a secret store.",
      "suggestedCode": null,
      "confidence": 75
    }
  ],
  "severityCounts": { "Critical": 1 },
  "engine": "semantic-kernel:gpt-4o"
}
```

Errors are returned as RFC 7807 problem details: `400` for a malformed PR URL, `404` when the
pull request is missing or private, `403` when the anonymous GitHub rate limit is exhausted.

### Other endpoints

- `GET /api/reviews/{reviewId}` — fetch a stored review
- `GET /api/reviews?take=10` — most recent reviews
- `GET /health` — liveness

## Notes and limits

- Anonymous GitHub access covers public repositories only and is rate limited to 60
  requests/hour; set `GitHub:Token` to raise this and to review private repositories.
- Reviews are held in memory and lost on restart (last 50 retained).
- Posting review comments back to the pull request is not enabled, since it requires a
  write-scoped token.
