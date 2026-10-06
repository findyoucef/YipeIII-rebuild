#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
mountain_evidence=monogame/evidence/2026-10-05-mountain-form
mountain_work=build/mountain-form-headless
mkdir -p "$mountain_work"
cp "$mountain_evidence"/cpu-source/* "$mountain_work/"
for mountain_art in terrain meadow town; do
    ffmpeg -v error -i "$mountain_evidence/input/$mountain_art.png" -f rawvideo -pix_fmt rgba -y "$mountain_work/$mountain_art.rgba"
done
ffmpeg -v error -i monogame/Yipe.Desktop/Content/Art/Retro/World/desert.png -f rawvideo -pix_fmt rgba -y "$mountain_work/desert-source.rgba"
for mountain_id in 001 006 017 018 021 022 023 024; do
    ffmpeg -v error -i "$mountain_evidence/before/map-$mountain_id.png" -f rawvideo -pix_fmt rgba -y "$mountain_work/base-$mountain_id.rgba"
done
DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 dotnet run --project "$mountain_work" -c Release --disable-build-servers -p:UseSharedCompilation=false
for mountain_id in 001 006 017 018 021 022 023 024; do
    ffmpeg -v error -f rawvideo -pixel_format rgba -video_size 416x384 -i "$mountain_work/preview-$mountain_id.rgba" -frames:v 1 -y "$mountain_work/preview-$mountain_id.png"
done
