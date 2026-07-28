# syntax=docker/dockerfile:1
# Multi-stage build. Build context is ./backend
# Stages: restore+build → test (optional) → slim runtime image.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so restore is cached independently of source changes.
COPY RecruitOps.sln ./
COPY src/Domain/RecruitOps.Domain.csproj                       src/Domain/
COPY src/Application/RecruitOps.Application.csproj             src/Application/
COPY src/Infrastructure/RecruitOps.Infrastructure.csproj       src/Infrastructure/
COPY src/Api/RecruitOps.Api.csproj                             src/Api/
COPY tests/RecruitOps.Domain.Tests/RecruitOps.Domain.Tests.csproj tests/RecruitOps.Domain.Tests/
COPY tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj       tests/RecruitOps.Api.Tests/
RUN dotnet restore RecruitOps.sln

COPY . .
RUN dotnet build RecruitOps.sln -c Release --no-restore

# Run the test suite inside the image:
#   docker build --target test -t recruitops-test ./backend
FROM build AS test
RUN dotnet test RecruitOps.sln -c Release --no-build --verbosity normal

FROM build AS publish
RUN dotnet publish src/Api/RecruitOps.Api.csproj -c Release -o /app --no-build

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# APP_UID is provided by the Microsoft base image — run as non-root.
USER $APP_UID

ENTRYPOINT ["dotnet", "RecruitOps.Api.dll"]
