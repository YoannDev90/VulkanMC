#!/bin/bash
set -e
mkdir -p pkg-deb/usr/local/bin
cp -r build/linux/* pkg-deb/usr/local/bin/
cd pkg-deb && fpm -s dir -t deb -n vulkanmc -v 1.0.0 -C . --prefix /usr/local --deb-priority optional --deb-compression xz
echo "✅ DEB created: pkg-deb/vulkanmc_1.0.0_amd64.deb"
