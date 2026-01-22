# feat: Add Ando Build File for CI/CD

## Overview

Create an Ando build file (`build.csando`) for SelfMX that automates the complete build, test, and deployment pipeline. The build will compile the .NET backend, build the React frontend, run all tests, and push Docker images to GitHub Container Registry (ghcr.io).

## Problem Statement

Currently, SelfMX lacks a standardized build automation system. Building, testing, and deploying requires manual steps or custom scripts. Ando provides a C#-based build system that runs in isolated Docker containers, ensuring reproducible builds across environments.

## Proposed Solution

Create a `build.csando` file that:
1. Builds and tests the .NET 10 backend
2. Builds the React/Vite frontend
3. Builds a Docker image combining both
4. Pushes the image to GitHub Container Registry (with `push` profile)

## Technical Approach

### Build File Structure

The build will follow the pattern established in the Ando project's own `build.csando`:

```
build.csando
├── Profile Definition (push)
├── Project References
├── .NET Build & Test
├── Frontend Build
├── Docker Image Build (push profile)
└── GitHub Container Registry Push (push profile)
```

### Key Implementation Details

**Project References:**
- Backend: `./src/SelfMX.Api/SelfMX.Api.csproj`
- Tests: `./tests/SelfMX.Api.Tests/SelfMX.Api.Tests.csproj`
- Frontend: `./client` directory

**Docker Image:**
- Tag format: `selfmx:{version}` where version is read from csproj
- Push to: `ghcr.io/aduggleby/selfmx:{version}`

**Build Profiles:**
- Default: Build and test only (CI validation)
- `push`: Full pipeline including Docker build and push to ghcr.io

## Acceptance Criteria

### Functional Requirements
- [x] `ando` command builds .NET project successfully
- [x] `ando` command runs all xUnit tests
- [x] `ando` command builds React frontend with npm
- [x] `ando -p push` builds Docker image using existing Dockerfile
- [x] `ando -p push` pushes image to ghcr.io/aduggleby/selfmx
- [x] `ando -p push` tags git repo with version and pushes tags

### Non-Functional Requirements
- [x] Build works in clean environment (no local dependencies)
- [x] Frontend build output is available for Docker context

## MVP

### build.csando

```csharp
// =============================================================================
// build.csando - SelfMX Build and Release Script
//
// Profiles:
// - (default): Build backend, frontend, and run tests
// - push: Also builds Docker image and pushes to GitHub Container Registry
//
// Usage:
//   ando              # Build and test only
//   ando -p push      # Build, test, and push to ghcr.io
//   ando --dind -p push  # Required for Docker operations
//
// Output:
// - selfmx:{version} Docker image (push profile only)
// =============================================================================

// Define profiles
var push = DefineProfile("push");

var project = Dotnet.Project("./src/SelfMX.Api/SelfMX.Api.csproj");
var testProject = Dotnet.Project("./tests/SelfMX.Api.Tests/SelfMX.Api.Tests.csproj");
var frontend = Directory("./client");

// Read version from csproj (default to 1.0.0 if not found)
var csprojPath = Path.Combine(Environment.CurrentDirectory, "src/SelfMX.Api/SelfMX.Api.csproj");
var version = "1.0.0";
if (File.Exists(csprojPath))
{
    var csprojContent = File.ReadAllText(csprojPath);
    var versionMatch = System.Text.RegularExpressions.Regex.Match(csprojContent, @"<Version>(\d+\.\d+\.\d+)</Version>");
    if (versionMatch.Success)
        version = versionMatch.Groups[1].Value;
}
Log.Info($"Building SelfMX version: {version}");

// Install .NET SDK
Dotnet.SdkInstall();

// Restore and build backend
Log.Info("Building .NET backend...");
Dotnet.Restore(project);
Dotnet.Build(project);

// Run backend tests
Log.Info("Running tests...");
Dotnet.Test(testProject);

// Build frontend
Log.Info("Building React frontend...");
Node.Install();
Npm.Ci(frontend);
Npm.Run(frontend, "build");

// Push profile: Build and push Docker image
if (push)
{
    Log.Info("Building Docker image...");

    // Install Docker CLI (needed for --dind mode)
    Docker.Install();

    // Build Docker image using existing Dockerfile
    Docker.Build("./Dockerfile", o => o
        .WithTag($"selfmx:{version}")
        .WithTag("selfmx:latest")
        .WithContext("."));

    // Push to GitHub Container Registry
    Log.Info("Pushing to GitHub Container Registry...");
    GitHub.PushImage("selfmx", o => o
        .WithTag(version)
        .WithOwner("aduggleby"));

    GitHub.PushImage("selfmx", o => o
        .WithTag("latest")
        .WithOwner("aduggleby"));

    // Tag and push git
    Git.Tag($"v{version}", o => o.WithSkipIfExists());
    Git.Push();
    Git.PushTags();
}

Log.Info("Build complete!");
```

## Usage Examples

```bash
# Local development - build and test
ando

# CI/CD - full pipeline with Docker
ando --dind -p push

# Verify build script without executing
ando verify
```

## Dependencies & Prerequisites

- Ando CLI installed (`dotnet tool install -g ando`)
- Docker running (for push profile with `--dind` flag)
- `GITHUB_TOKEN` environment variable set (for ghcr.io push)
- Version tag in `src/SelfMX.Api/SelfMX.Api.csproj` (optional, defaults to 1.0.0)

## CLAUDE.md Update

Add the following to the Build & Test Commands section:

```markdown
### Ando Build System

```bash
ando                      # Build backend, frontend, run tests
ando -p push --dind       # Build + push Docker image to ghcr.io
ando verify               # Validate build script
ando clean                # Remove build artifacts
```

Find Ando documentation at https://andobuild.com
```

## References

- Ando documentation: https://andobuild.com
- Reference build file: `/home/alex/Source/ando/build.csando`
- Existing Dockerfile: `/home/alex/Source/selfmx/Dockerfile`
- SelfMX API project: `/home/alex/Source/selfmx/src/SelfMX.Api/SelfMX.Api.csproj`
