# FileProcessorApi

A secure RESTful web service built with ASP.NET Core (.NET 10) that processes uploaded CSV files, calculates a simple aggregate (average of a specified column), and tracks processed files for basic reporting. Endpoints are protected with API key authentication implemented as custom middleware.

## Features

- CSV file upload and processing (average of a numeric column)
- API key authentication via `X-Api-Key` request header, enforced by middleware
- In-memory tracking of all processed files (success and failure)
- Reporting endpoint with processing counts and per-file details
- Structured logging and consistent error handling
- Swagger UI with API key support for interactive testing
- Dockerized with a multi-stage build

## Tech Stack

| Component | Choice |
|---|---|
| Framework | ASP.NET Core Web API (.NET 10) |
| CSV parsing | CsvHelper |
| API documentation | Swashbuckle (Swagger UI) |
| Containerization | Docker (multi-stage build) |

## API Endpoints

All endpoints under `/api` require the `X-Api-Key` header.

### POST /api/files/process

Uploads a CSV file and returns the average of the specified column.

| Parameter | Location | Type | Required | Description |
|---|---|---|---|---|
| `file` | multipart/form-data | file | Yes | The CSV file to process (max 10 MB) |
| `column` | query string | string | No | Column to average. Defaults to `Amount` |
| `X-Api-Key` | header | string | Yes | API key |

**Example request**

```bash
curl -X POST "http://localhost:8080/api/files/process?column=Amount" \
  -H "X-Api-Key: super-secret-key-123" \
  -F "file=@Test.csv"
```

**Example response (200 OK)**

```json
{
  "fileName": "Test.csv",
  "column": "Amount",
  "rowCount": 3,
  "average": 133.3333,
  "processingTimeMs": 12
}
```

**Error responses**

| Status | Cause |
|---|---|
| 400 | No file uploaded, file too large, wrong extension, missing column, or no numeric values in the column |
| 401 | Missing or invalid API key |
| 500 | Unexpected server error |

Non-numeric values in the target column are skipped and logged as warnings rather than failing the whole file.

### GET /api/files/report

Returns a summary of all files processed since the service started.

**Example request**

```bash
curl "http://localhost:8080/api/files/report" \
  -H "X-Api-Key: super-secret-key-123"
```

**Example response (200 OK)**

```json
{
  "totalFilesProcessed": 2,
  "successfulFiles": 1,
  "failedFiles": 1,
  "files": [
    {
      "fileName": "Test.csv",
      "processedAtUtc": "2026-07-13T11:48:21Z",
      "processingTimeMs": 12,
      "rowCount": 3,
      "success": true,
      "errorMessage": null
    },
    {
      "fileName": "broken.csv",
      "processedAtUtc": "2026-07-13T11:50:03Z",
      "processingTimeMs": 0,
      "rowCount": 0,
      "success": false,
      "errorMessage": "Column 'Amount' was not found in the CSV header."
    }
  ]
}
```

## Authentication

Every request to `/api/*` must include the API key in the `X-Api-Key` header. The key is validated by custom middleware before the request reaches any controller. Requests with a missing or invalid key receive `401 Unauthorized` with a JSON error body.

The key is configured in `appsettings.json` under `ApiKey` and can be overridden with the `ApiKey` environment variable (used by the Docker run command below).

Note: the demo key is committed in `appsettings.json` so the service runs out of the box. In production this would come from environment variables or a secret store such as Azure Key Vault, and the comparison would use a hashed key.

## Running Locally

Requirements: .NET 10 SDK

```bash
dotnet run
```

Swagger UI is available at `http://localhost:5299/swagger`.

To authenticate in Swagger, click the Authorize button, enter the API key, and all requests from the UI will include the header. A ready-to-use `FileProcessorApi.http` file is also included with example requests, runnable directly from Visual Studio.

**Sample CSV** (`Test.csv` included in the repo):

```csv
Name,Amount
Alice,100
Bob,250.5
Carol,49.5
```

## Running with Docker

Requirements: Docker Desktop

```bash
docker build -t fileprocessor-api .
docker run -p 8080:8080 -e ApiKey=super-secret-key-123 fileprocessor-api
```

The service is then available at `http://localhost:8080/swagger`.

The Dockerfile uses a multi-stage build: the SDK image compiles and publishes the app, and the final image is the slim ASP.NET runtime image, keeping the image small and free of build tooling. Passing the API key with `-e` demonstrates configuration through environment variables instead of baking secrets into the image.

## Project Structure

```
FileProcessorApi/
├── Controllers/
│   └── FilesController.cs          # Upload and report endpoints
├── Middleware/
│   └── ApiKeyMiddleware.cs         # API key validation
├── Models/
│   ├── CsvProcessingResult.cs
│   ├── FileReport.cs
│   └── ProcessedFileRecord.cs
├── Services/
│   ├── CsvProcessingService.cs     # CSV parsing and aggregation
│   └── InMemoryFileTrackingService.cs  # Thread-safe processing log
├── Dockerfile
├── FileProcessorApi.http           # Example requests
├── Test.csv                        # Sample input
└── ReadMe.md
```

## Design Decisions

- **CSV over JSON**: chosen because the aggregate requirement (column average) maps naturally to tabular data, and CsvHelper handles parsing edge cases reliably.
- **Middleware for authentication**: keeps the security concern out of controllers entirely and applies uniformly to every `/api` route. Swagger routes are intentionally left open for easy local testing.
- **In-memory tracking with a thread-safe singleton**: a `ConcurrentQueue` behind an interface satisfies the tracking requirement without external dependencies. The interface boundary means swapping to a database-backed implementation later requires no controller changes.
- **Graceful row handling**: non-numeric rows are skipped and logged instead of rejecting the file, since real-world CSVs are rarely clean. Structural problems (missing column, no numeric data at all) still fail fast with a 400.
- **Failures are tracked too**: the report distinguishes successful and failed processing attempts, which is more useful operationally than a bare counter.

## What I Would Do With More Time

- Unit tests for the CSV service and middleware (xUnit)
- Persistent storage for tracking (SQLite or SQL Server) so the report survives restarts
- Constant-time comparison and hashed storage for the API key
- Support for multiple API keys with per-client identification in the report
- Health check endpoint (`/health`) for container orchestration
- JSON file support as a second processor behind a common interface