# Stage 1: Build the ASP.NET Core application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Build arguments for GitHub token
ARG GITHUB_TOKEN
ARG GITHUB_USERNAME

# Make build args available as environment variables for RUN commands
ENV GITHUB_TOKEN=${GITHUB_TOKEN}
ENV GITHUB_USERNAME=${GITHUB_USERNAME}

# Copy nuget.config first
COPY BMIRussian_ru/nuget.config ./nuget.config

# Configure NuGet authentication for GitHub Packages if token is provided
# Method 1: Add to user-level config (for when not using --configfile)
RUN if [ -n "$GITHUB_TOKEN" ] && [ -n "$GITHUB_USERNAME" ]; then \
        dotnet nuget remove source github 2>/dev/null || true; \
        dotnet nuget add source https://nuget.pkg.github.com/sibvic/index.json \
            --name github \
            --username "$GITHUB_USERNAME" \
            --password "$GITHUB_TOKEN" \
            --store-password-in-clear-text; \
    else \
        echo "Warning: GITHUB_TOKEN or GITHUB_USERNAME not provided. Private packages may fail to restore."; \
    fi

# Method 2: Create a nuget.config with embedded credentials for use with --configfile
RUN if [ -n "$GITHUB_TOKEN" ] && [ -n "$GITHUB_USERNAME" ]; then \
        printf '<?xml version="1.0" encoding="utf-8"?>\n<configuration>\n  <packageSources>\n    <clear />\n    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />\n    <add key="github" value="https://nuget.pkg.github.com/sibvic/index.json" />\n  </packageSources>\n  <packageSourceCredentials>\n    <github>\n      <add key="Username" value="%s" />\n      <add key="ClearTextPassword" value="%s" />\n    </github>\n  </packageSourceCredentials>\n</configuration>\n' "$GITHUB_USERNAME" "$GITHUB_TOKEN" > ./nuget.config; \
    fi

# Copy project file and restore dependencies
COPY BMIRussian_ru/BMIRussian_ru.csproj BMIRussian_ru/
RUN dotnet restore BMIRussian_ru/BMIRussian_ru.csproj --configfile ./nuget.config

# Copy all files and build
COPY BMIRussian_ru/ BMIRussian_ru/
WORKDIR /src/BMIRussian_ru
RUN dotnet build BMIRussian_ru.csproj -c Release -o /app/build

# Publish the application
RUN dotnet publish BMIRussian_ru.csproj -c Release -o /app/publish

# Stage 2: Run the application
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install yt-dlp for video import by URL (YouTube, VK)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o /usr/local/bin/yt-dlp \
    && chmod +x /usr/local/bin/yt-dlp \
    && apt-get purge -y curl \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Expose port
EXPOSE 5128

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5128
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "BMIRussian_ru.dll"]

