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
# The Api project carries extra diagnostics that Domain does not need, because on 2026-07-28
# it failed here with:
#
#   MSB4181: The "VSTestTask" task returned false but did not log an error.
#   Build FAILED.  0 Warning(s)  0 Error(s)
#
# "Returned false but did not log an error" is not one failure — it is three, and the default
# output cannot tell them apart:
#
#   1. ZERO TESTS DISCOVERED, now fatal because of TreatNoTestsAsError below. vstest reports
#      this as a *warning* and exits non-zero, so MSBuild has nothing to log as an error.
#   2. THE TEST HOST CRASHED — a process abort takes the results with it, so there is no
#      failing test to name. Plausible here: every Api class fixture boots a real ASP.NET host
#      via WebApplicationFactory, and a throw in that constructor kills the host, not a test.
#   3. vstest.console failed to start at all (missing runtimeconfig, bad runsettings).
#
# So: `--blame-crash` names the last test the host was executing before it died, which
# separates (2) from the others outright, and `console;verbosity=detailed` surfaces the
# "No test is available" line that MSBuild's own verbosity swallows, which separates (1).
# Neither changes what is executed — they only make the failure legible. Do not "clean these
# up" until this project has been green for a while; the cost of a re-run to re-learn which of
# the three it was is far higher than the log noise.
RUN dotnet test tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj \
      -c Release --no-build --verbosity normal \
      --logger "console;verbosity=detailed" \
      --blame-crash --blame-hang-timeout 5m \
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
