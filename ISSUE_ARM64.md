# ARM64 Multi-Arch Build Issue with Ando

## Summary

When using `Docker.Build` with `WithPlatforms("linux/amd64", "linux/arm64")` and `WithPush()`, the ARM64 build fails during .NET's crossgen2 ReadyToRun compilation step due to QEMU emulation issues.

## Environment

- Ando version: 0.9.63
- Host: linux/amd64 (Arch Linux)
- Docker: 29.1.3 with buildx 0.30.1
- .NET SDK: 9.0 (Alpine-based image)
- Build mode: `--dind` (Docker-in-Docker)

## Build Script

```csharp
Docker.Build("./Dockerfile", o => o
    .WithPlatforms("linux/amd64", "linux/arm64")
    .WithTag($"ghcr.io/aduggleby/selfmx:{project.Version}")
    .WithTag("ghcr.io/aduggleby/selfmx:latest")
    .WithContext(".")
    .WithPush());
```

## Dockerfile (relevant section)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS backend-build
# ...
RUN dotnet publish src/SelfMX.Api/SelfMX.Api.csproj \
    -c Release \
    -o /app/publish \
    -r linux-musl-x64 \
    --self-contained false \
    -p:PublishReadyToRun=true
```

## Error

The ARM64 build fails during the `dotnet publish` step with crossgen2:

```
#49 [linux/arm64 backend-build 7/7] RUN dotnet publish src/SelfMX.Api/SelfMX.Api.csproj ...
#49 122.5   SelfMX.Api -> /src/src/SelfMX.Api/bin/Release/net9.0/linux-musl-x64/SelfMX.Api.dll
#49 168.6 /usr/share/dotnet/sdk/9.0.310/Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.CrossGen.targets(470,5): error MSB6006: "/root/.nuget/packages/microsoft.netcore.app.crossgen2.linux-musl-arm64/9.0.12/tools/crossgen2" exited with code 139. [/src/src/SelfMX.Api/SelfMX.Api.csproj]
```

Exit code 139 = SIGSEGV (segmentation fault), which indicates a crash in the QEMU-emulated crossgen2 process.

## Root Cause

When Docker buildx builds ARM64 images on an AMD64 host, it uses QEMU user-mode emulation. The .NET crossgen2 tool (used for ReadyToRun/AOT compilation) is CPU-intensive and doesn't work reliably under QEMU emulation.

This is a known issue:
- https://github.com/dotnet/runtime/issues/67073
- https://github.com/dotnet/sdk/issues/22292

## Workarounds

### 1. Disable ReadyToRun (Current Solution)

Remove `-p:PublishReadyToRun=true` from the Dockerfile:

```dockerfile
RUN dotnet publish src/SelfMX.Api/SelfMX.Api.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false
```

This works but loses the startup performance benefits of ReadyToRun.

### 2. Single-Arch Build (Current Solution)

Remove multi-arch and just build for amd64:

```csharp
Docker.Build("./Dockerfile", o => o
    .WithTag($"ghcr.io/aduggleby/selfmx:{project.Version}")
    .WithTag("ghcr.io/aduggleby/selfmx:latest")
    .WithContext(".")
    .WithPush());
```

### 3. Native ARM64 Builder (Ideal Solution)

Use a native ARM64 builder node instead of QEMU emulation. This requires:
- An ARM64 machine (e.g., AWS Graviton, Apple Silicon, Ampere)
- Docker buildx configured with a remote builder

## Feature Request for Ando

It would be helpful if Ando could:

1. **Detect crossgen2/ReadyToRun failures** and provide a helpful error message suggesting to disable ReadyToRun for cross-architecture builds.

2. **Support native multi-arch builders** - Allow configuring remote ARM64 builders for `WithPlatforms()` builds:

   ```csharp
   Docker.Build("./Dockerfile", o => o
       .WithPlatforms("linux/amd64", "linux/arm64")
       .WithArmBuilder("ssh://arm64-builder")  // or similar
       .WithPush());
   ```

3. **Provide a fallback option** - If ARM64 build fails, optionally continue with just amd64:

   ```csharp
   Docker.Build("./Dockerfile", o => o
       .WithPlatforms("linux/amd64", "linux/arm64")
       .WithFallbackToAvailable()  // Continue if one platform fails
       .WithPush());
   ```

## References

- [.NET Runtime Issue #67073](https://github.com/dotnet/runtime/issues/67073) - crossgen2 crashes under QEMU
- [.NET SDK Issue #22292](https://github.com/dotnet/sdk/issues/22292) - ARM64 cross-compilation issues
- [Docker Buildx Multi-Platform](https://docs.docker.com/build/building/multi-platform/)
- [QEMU User Mode Emulation Limitations](https://wiki.qemu.org/Documentation/Platforms/ARM)
