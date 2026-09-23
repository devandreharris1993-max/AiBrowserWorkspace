# syntax=docker/dockerfile:1
#
# AiBrowserWorkspace is a desktop GUI app (Avalonia + an embedded CEF browser), not a
# service, so the container doesn't "serve" anything on its own — it needs an X11
# display to draw its window into. This image expects the HOST's X server, forwarded
# in at `docker run` time (see the run command below); it does not bundle a display
# server or VNC of its own.
#
# Build:
#   docker build -t aibrowserworkspace .
#
# Run (Linux host, X11 passthrough):
#   xhost +local:docker   # one-time per session: let container clients reach the display
#   docker run --rm -it \
#     -e DISPLAY=$DISPLAY \
#     -v /tmp/.X11-unix:/tmp/.X11-unix:rw \
#     -v aibrowserworkspace-cache:/root/.local/share/AiBrowserWorkspace \
#     aibrowserworkspace
#
# The named volume keeps the CEF profile (cookies/login state for chatgpt.com and
# claude.ai — see App.axaml.cs's shared WebView cache path) across container restarts,
# so you don't have to re-sign-in to ChatGPT/Claude on every run.

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, against just the project files, so source-only edits don't invalidate
# the NuGet restore layer (the CEF/Avalonia redistributables are the expensive part).
COPY AiBrowserWorkspace.sln ./
COPY AiBrowserWorkspace/AiBrowserWorkspace.csproj AiBrowserWorkspace/
RUN dotnet restore AiBrowserWorkspace/AiBrowserWorkspace.csproj

COPY AiBrowserWorkspace/ AiBrowserWorkspace/
RUN dotnet publish AiBrowserWorkspace/AiBrowserWorkspace.csproj \
        -c Release \
        -o /app/publish \
        --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

# X11 client libraries (for Avalonia's software-rendered X11 backend — see
# Program.cs's X11RenderingMode.Software, chosen to avoid depending on a GPU driver)
# plus the shared libraries the embedded CEF/Chromium browser dlopens at startup.
# CEF itself — libcef.so, its .pak/locale data, and even its own self-contained .NET
# runtime — ships inside the app's CefGlueBrowserProcess/ folder (a NuGet-restored
# asset), so nothing Chromium-specific needs installing beyond these OS libraries.
RUN apt-get update && apt-get install -y --no-install-recommends \
        libx11-6 libxext6 libxrender1 libxrandr2 libxfixes3 libxcomposite1 \
        libxdamage1 libxkbcommon0 libxshmfence1 libice6 libsm6 \
        libgtk-3-0 libglib2.0-0 libpango-1.0-0 libpangocairo-1.0-0 libcairo2 \
        libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libgbm1 \
        libasound2 libnss3 libnspr4 \
        libfontconfig1 fonts-liberation \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# dotnet publish sets the exec bit on Linux apphosts, but make it explicit for the
# two native entry points (the app itself and CEF's out-of-process browser host) in
# case the build context or a COPY step ever loses it.
RUN chmod +x ./AiBrowserWorkspace ./CefGlueBrowserProcess/Xilium.CefGlue.BrowserProcess

# Where WebView.Settings.CachePath (App.axaml.cs) resolves to for the container's
# $HOME (root, since no USER is set — X11-in-Docker is simplest run as root; see the
# run command above for mounting a persistent volume here).
VOLUME ["/root/.local/share/AiBrowserWorkspace"]

ENTRYPOINT ["./AiBrowserWorkspace"]
