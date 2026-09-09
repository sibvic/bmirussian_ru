#!/bin/sh
# Installs/updates yt-dlp to /usr/local/bin/yt-dlp (needed by admin video import).
set -e

curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux -o /usr/local/bin/yt-dlp
chmod +x /usr/local/bin/yt-dlp
/usr/local/bin/yt-dlp --version
