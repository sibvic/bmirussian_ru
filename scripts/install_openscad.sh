#!/bin/sh
# Installs OpenSCAD (needed by bmirussian_stl_worker to render STL files).
set -e

apt-get update
apt-get install -y openscad
openscad --version
