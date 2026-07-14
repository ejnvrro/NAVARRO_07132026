# FileProcessorApi

A secure RESTful web service built with ASP.NET Core (.NET 10) that processes uploaded CSV and JSON files, tracks every processing attempt in a persistent SQLite database, and reports on processed files per client. Endpoints are protected by hashed, multi-client API key authentication implemented as custom middleware.

## Features

- CSV processing: average of a configurable numeric column
- JSON processing: filtering an array of objects by a condition expression
- Extensible processor design: each file format is a strategy behind a common `IFileProcessor` interface
- API key authentication with SHA-256 hashed keys, constant-time comparison, and multiple named clients
- Per-client identification on every tracked record
- Persistent file tracking in SQLite (survives application and container restarts)
- Reporting endpoint with success/failure counts and per-file details
- Health check endpoint with a live database probe
- Structured logging and consistent error handling
- Unit test suite (xUnit) covering validators, processors, and tracking
- Dockerized with a multi-stage build, named volume, and container health check

## Tech Stack

| Component | Choice |
|---|---|
| Framework | ASP.NET Core Web API (.NET 10) |
| CSV parsing | CsvHelper |
| JSON parsing | System.Text.Json |
| Persistence | EF Core with SQLite |
| API documentation | Swashbuckle (Swagger UI) |
| Testing | xUnit with in-memory SQLite |
| Containerization | Docker, Docker Compose |

## API Endpoints

All endpoints under `/api` require the `X-Api-Key` header. `/health` is intentionally open (see Authentication).

### POST /api/files/process

Uploads a file and processes it according to its extension.

| Parameter | Location | Type | Required | Description |
|---|---|---|---|---|
| `file` | multipart/form-data | file | Yes | The .csv or .json file to process (max 10 MB) |
| `parameter` | query string | string | Depends | CSV: column to average, defaults to `Amount`. JSON: filter expression, required |
| `X-Api-Key` | header | string | Yes | API key |

**CSV example**

```bash
curl -X POST "http://localhost:8080/api/files/process?parameter=Amount" \
  -H "X-Api-Key: super-secret-key-123" \
  -F "file=@Test.csv"
```

```json
{
  "fileName": "Test.csv",
  "processor": "csv-average",
  "recordCount": 3,
  "result": { "column": "Amount", "average": 133.3333 },
  "processingTimeMs": 12
}
```

**JSON example**

Filter expressions take the form `field>value`, `field<value`, or `field=value`. Numeric comparisons apply when the target property is numeric; string properties support equality only.

```bash
curl -X POST "http://localhost:8080/api/files/process?parameter=Amount>100" \
  -H "X-Api-Key: super-secret-key-123" \
  -F "file=@Test.json"
```

```json
{
  "fileName": "Test.json",
  "processor": "json-filter",
  "recordCount": 3,
  "result": {
    "filter": "Amount>100",
    "matchCount": 1,
    "items": [ { "Name": "Bob", "Amount": 250.5 } ]
  },
  "processingTimeMs": 8
}
```

**Error responses**

| Status | Cause |
|---|---|
| 400 | No file, file too large, unsupported extension, missing CSV column, invalid or missing JSON filter, invalid JSON, no numeric values |
| 401 | Missing or invalid API key |
| 500 | Unexpected server error |

Failed attempts are tracked in the report alongside successful ones, with the error message recorded.

### GET /api/files/report

Returns all processing attempts, newest first, with per-client attribution.

```bash
curl "http://localhost:8080/api/files/report" -H "X-Api-Key: super-secret-key-123"
```

```json
{
  "totalFilesProcessed": 2,
  "successfulFiles": 1,
  "failedFiles": 1,
  "files": [
    {
      "id": 2,
      "fileName": "Test.json",
      "clientName": "reviewer",
      "processedAtUtc": "2026-07-14T11:02:41Z",
      "processingTimeMs": 8,
      "recordCount": 3,
      "success": true,
      "errorMessage": null
    },
    {
      "id": 1,
      "fileName": "broken.csv",
      "clientName": "demo-client",
      "processedAtUtc": "2026-07-14T10:58:03Z",
      "processingTimeMs": 0,
      "recordCount": 0,
      "success": false,
      "errorMessage": "Column 'Amount' was not found in the CSV header."
    }
  ]
}
```

### GET /health

Health check probing both process liveness and database connectivity. Returns `Healthy` (200) or `Unhealthy` (503). Used by the Docker health check; orchestrators and load balancers probe this endpoint without credentials, which is why it sits outside the API key wall.

## Authentication

Requests to `/api/*` must include a valid key in the `X-Api-Key` header, validated by custom middleware before any controller runs.

Security design:

- **Hashed storage**: configuration contains only SHA-256 hashes of the keys, never plaintext. Reading the config or the image does not reveal usable credentials.
- **Constant-time comparison**: hashes are compared with `CryptographicOperations.FixedTimeEquals`, which always compares every byte. An ordinary equality check exits at the first mismatch, so response timing leaks how much of the value matched, enabling timing attacks.
- **Multiple named clients**: each key belongs to a named client, and the client name is stamped onto every tracked record, so the report shows who processed what.

Clients are configured in `appsettings.json`:

```json
"ApiClients": [
  { "Name": "demo-client", "KeyHash": "<sha256 hex of the key>" },
  { "Name": "reviewer", "KeyHash": "<sha256 hex of the key>" }
]
```

Two demo keys are configured for review purposes:

| Client | Key |
|---|---|
| demo-client | `super-secret-key-123` |
| reviewer | `reviewer-key-456` |

Publishing demo keys in a README is obviously not a production practice; it is done here so the service runs out of the box. In production, keys would be issued per client through a secret store, and only their hashes would ever reach configuration.

Generating a hash for a new key (PowerShell):

```powershell
$key = "my-new-key"
[System.Convert]::ToHexString(
  [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($key)))
```

## File Tracking and Persistence

Every processing attempt (success or failure) is stored in a SQLite database through EF Core, behind an `IFileTrackingService` interface. The database file lives in `data/tracking.db` under the application content root.

In Docker, `/app/data` is mounted to a named volume, so the report survives container destruction and recreation, not just restarts. To verify: process a file, `docker compose down`, `docker compose up -d`, and the record is still in the report.

The schema is created at startup with `EnsureCreated()`. This is a deliberate simplification for a single-table service: it cannot apply incremental schema changes, so a schema change requires recreating the database (`docker compose down -v`). EF Core migrations would be the production path.

## Running Locally

Requirements: .NET 10 SDK

```bash
dotnet run
```

Swagger UI: `http://localhost:5299/swagger`. Click Authorize, enter one of the demo keys, and all requests from the UI include the header. A `FileProcessorApi.http` file with ready-made requests is included and runs directly from Visual Studio.

Sample inputs are included in the repo:

`Test.csv`

```csv
Name,Amount
Alice,100
Bob,250.5
Carol,49.5
```

`Test.json`

```json
[
  { "Name": "Alice", "Amount": 100 },
  { "Name": "Bob", "Amount": 250.5 },
  { "Name": "Carol", "Amount": 49.5 }
]
```

## Running with Docker

Requirements: Docker Desktop

```bash
docker compose up -d --build
```

The service is available at `http://localhost:8080/swagger`. Container status shows `(healthy)` in `docker ps` once the health probe passes.

Useful commands:

```bash
docker compose logs -f      # follow application logs
docker compose down         # stop and remove the container (data volume survives)
docker compose down -v      # also remove the volume (resets the database)
```

Without Compose:

```bash
docker build -t fileprocessor-api .
docker run -d --name fp -p 8080:8080 -v fileprocessor-data:/app/data fileprocessor-api
```

The Dockerfile uses a multi-stage build: the SDK image compiles and publishes, and the final image is the slim ASP.NET runtime image, keeping it small and free of build tooling. The image contains only key hashes, never keys, so it is safe to distribute.

## Running Tests

```bash
dotnet test
```

The suite covers:

- `ApiKeyValidator`: correct key, wrong key, missing key, multiple clients, empty configuration
- `CsvFileProcessor`: correct averages, missing column, non-numeric rows skipped, no numeric data, default column fallback
- `JsonFileProcessor`: all three filter operators, string equality, invalid filters, invalid JSON, non-array root
- `SqliteFileTrackingService`: tracking and reporting, success/failure counting, empty database

Tracking tests run against real in-memory SQLite (a live connection, not the EF in-memory provider), so they exercise actual SQL and the real schema. Tests target services directly rather than spinning up the host; integration tests through `WebApplicationFactory` would be the next layer.

## Project Structure

```
FileProcessorApi/
├── Controllers/
│   └── FilesController.cs              # Upload and report endpoints
├── Data/
│   └── FileTrackingDbContext.cs        # EF Core context and schema
├── Middleware/
│   └── ApiKeyMiddleware.cs             # API key enforcement, client stamping
├── Models/
│   ├── ApiClient.cs
│   ├── FileProcessingResult.cs
│   ├── FileReport.cs
│   └── ProcessedFileRecord.cs
├── Services/
│   ├── ApiKeyValidator.cs              # Hash validation, constant-time compare
│   ├── CsvFileProcessor.cs             # IFileProcessor: column average
│   ├── JsonFileProcessor.cs            # IFileProcessor: condition filter
│   ├── IFileProcessor.cs
│   └── SqliteFileTrackingService.cs    # Persistent tracking
├── Dockerfile
├── docker-compose.yml
├── FileProcessorApi.http               # Example requests
├── Test.csv / Test.json                # Sample inputs
└── ReadMe.md

FileProcessorApi.Tests/
├── ApiKeyValidatorTests.cs
├── CsvFileProcessorTests.cs
├── JsonFileProcessorTests.cs
├── SqliteFileTrackingServiceTests.cs
└── TestHelpers.cs
```

## Design Decisions

- **Strategy pattern for file formats**: each format implements `IFileProcessor` and registers in DI; the controller resolves the right one by file extension. Adding a third format is one class and one registration line, with no controller changes.
- **Configurable processing parameter**: the spec allows a fixed column or condition; it is exposed as a query parameter instead so the service is reusable across files. CSV defaults to `Amount`, so the simplest call works with no configuration, while JSON requires an explicit filter and says so clearly when it is missing.
- **Middleware for authentication**: keeps the security concern out of controllers and applies uniformly to every `/api` route. Validation logic lives in an injectable `IApiKeyValidator` service, which keeps the middleware thin and the logic unit-testable.
- **Interface boundary for tracking**: the tracking implementation was swapped from in-memory to SQLite mid-project without touching the controller, which is exactly what the interface was for.
- **Non-tracking reads**: report queries use `AsNoTracking()` since results are read-only.
- **Failures are first-class data**: rejected files are recorded with their error message, making the report useful for diagnosing client problems, not just counting successes.
- **Graceful row handling**: non-numeric CSV rows are skipped with a warning rather than failing the file, since real-world CSVs are rarely clean; structural problems still fail fast with a 400.
- **EnsureCreated over migrations**: a conscious simplification, with the tradeoff documented above.
- **Swagger enabled in all environments**: deliberate for reviewability of this challenge; it would be gated to non-production environments in a real deployment.
