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
#
# ONE `RUN` PER TEST PROJECT, not `dotnet test RecruitOps.sln`. The solution-wide form spawns
# a separate vstest run per project on parallel MSBuild nodes and interleaves their stdout, so
# the log carries two "A total of 1 test files matched" lines and two summaries with nothing
# tying either count to an assembly. Run #3 (2026-07-28) read as "Total tests: 39" directly
# under `Starting: RecruitOps.Api.Tests` — which is Domain's summary, but is indistinguishable
# at a glance from the Api project contributing nothing. A count you cannot attribute is not a
# count, and this repo has been burned by exactly that ambiguity three times.
#
# Two RUN layers also mean two independent exit codes: an Api failure can no longer be read as
# a Domain success. Both live in the `test` stage, so `--no-cache-filter=test` still busts both.
#
# `RunConfiguration.TreatNoTestsAsError=true` is the load-bearing part. By default a project
# that discovers ZERO tests exits 0 and the build stays green — the precise failure mode that
# would let a whole assembly quietly stop running and never be noticed.
FROM build AS test
RUN dotnet test tests/RecruitOps.Domain.Tests/RecruitOps.Domain.Tests.csproj \
      -c Release --no-build --verbosity normal \
      -- RunConfiguration.TreatNoTestsAsError=true
# This project briefly carried `--logger "console;verbosity=detailed"` and `--blame-crash`, to
# triage an `MSB4181: the "VSTestTask" task returned false but did not log an error`. They did
# their job — the answer was mundane, 8 genuinely failing tests that MSBuild declined to
# summarise — and they are gone again because their output volume is a liability: BuildKit
# truncates a step's log at 1MiB by default, and a truncated log costs the run's *summary*,
# which is the one part anybody reads. CI raises that limit now, but the cheapest log is the
# one that was never written.
#
# If MSB4181 returns, the triage is: `No test is available` in the output means zero tests were
# discovered (fatal via TreatNoTestsAsError above); `test host process crashed` means the host
# aborted and took the results with it — likely a throw inside a `WebApplicationFactory` class
# fixture, which kills the host rather than failing a test. Re-add `--blame-crash` to name the
# test it died on.
RUN dotnet test tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj \
      -c Release --no-build --verbosity normal \
      -- RunConfiguration.TreatNoTestsAsError=true

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
