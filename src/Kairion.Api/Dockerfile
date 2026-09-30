# Multi-stage build for Kairion.Api. The build stage compiles the API; the
# runtime stage runs on the smaller ASP.NET 10 runtime image so the published
# image is small and the secrets are not baked into any intermediate layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project files first so the restore layer is cached.
COPY Kairion.slnx Directory.Build.props global.json ./
COPY src/Kairion.Domain/Kairion.Domain.csproj src/Kairion.Domain/
COPY src/Kairion.Application/Kairion.Application.csproj src/Kairion.Application/
COPY src/Kairion.Infrastructure/Kairion.Infrastructure.csproj src/Kairion.Infrastructure/
COPY src/Kairion.Api/Kairion.Api.csproj src/Kairion.Api/
COPY tests/Kairion.UnitTests/Kairion.UnitTests.csproj tests/Kairion.UnitTests/
COPY tests/Kairion.IntegrationTests/Kairion.IntegrationTests.csproj tests/Kairion.IntegrationTests/
RUN dotnet restore Kairion.slnx

# Copy the source and publish a self-contained release build.
COPY src/ src/
COPY tests/ tests/
RUN dotnet publish src/Kairion.Api/Kairion.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kairion.Api.dll"]
