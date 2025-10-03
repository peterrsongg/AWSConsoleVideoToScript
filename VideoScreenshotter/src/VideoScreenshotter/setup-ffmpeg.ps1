# Download FFmpeg binaries for Linux x64
$ffmpegUrl = "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz"
$tempFile = "ffmpeg-static.tar.xz"

# Create ffmpeg directory
New-Item -ItemType Directory -Force -Path "ffmpeg"

# Download and extract
Invoke-WebRequest -Uri $ffmpegUrl -OutFile $tempFile
tar -xf $tempFile --strip-components=1 -C ffmpeg ffmpeg-*-amd64-static/ffmpeg ffmpeg-*-amd64-static/ffprobe

# Cleanup
Remove-Item $tempFile

Write-Host "FFmpeg binaries downloaded to ffmpeg/ folder"