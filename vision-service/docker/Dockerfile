FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the server-side project files for layer caching.
# The solution file is intentionally excluded — it references the MAUI client
# which is not part of the Docker build context.
COPY src/VisionService/VisionService.csproj src/VisionService/
COPY src/VisionService.Tests/VisionService.Tests.csproj src/VisionService.Tests/
RUN dotnet restore src/VisionService/VisionService.csproj

# Copy everything and build
COPY . .
RUN dotnet publish src/VisionService/VisionService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser -d /app appuser
RUN mkdir -p /data/images && chown -R appuser:appuser /data /app

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser
EXPOSE 5100

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5100/health || exit 1

ENTRYPOINT ["dotnet", "VisionService.dll"]
