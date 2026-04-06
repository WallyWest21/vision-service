# Vision Service MAUI Client

A standalone Windows desktop application built with .NET 8 MAUI that exercises every endpoint of the **VisionService** microservice.

## Pages

| Tab | Endpoints covered |
|-----|-------------------|
| **Health** | `GET /health` |
| **YOLO** | `POST /api/v1/detect`, `/detect/batch`, `/segment`, `/classify`, `/pose` |
| **Qwen-VL** | `POST /api/v1/ask`, `/caption`, `/ocr`, `/analyze`, `/compare`, `/describe/detailed` |
| **Pipeline** | `POST /api/v1/pipeline/detect-and-describe`, `/safety-check`, `/inventory`, `/scene` |
| **Admin** | `GET /api/v1/admin/keys`, `POST /api/v1/admin/keys` |
| **Settings** | Configure service URL and API key (persisted via `Preferences`) |

## Prerequisites

- Windows 10 (17763 / October 2018 Update) or later
- .NET 10 SDK with MAUI workload: `dotnet workload install maui-windows`
- VisionService running (Docker Compose or local):
  ```
  docker compose up
  ```
  Default URL: `http://localhost:5100`

## Build & Run

```powershell
cd maui-client
dotnet build -f net10.0-windows10.0.19041.0
dotnet run  -f net10.0-windows10.0.19041.0
```

Or open in Visual Studio 2022+ and select the `MauiClient (Windows Machine)` launch profile.

## Configuration

On first launch, open the **Settings** tab and enter:
- **Service URL** – e.g. `http://localhost:5100`
- **API Key** – the key value from `appsettings.json` `Auth:ApiKeys[0].Key`

Click **Save**; the values are persisted in `Preferences` across restarts.
