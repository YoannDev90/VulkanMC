#!/bin/bash
set -e
mkdir -p pkg-rpm/usr/local/bin
cp -r build/linux/* pkg-rpm/usr/local/bin/
cd pkg-rpm && fpm -s dir -t rpm -n vulkanmc -v 1.0.0 -C . --prefix /usr/local
echo "✅ RPM created: pkg-rpm/vulkanmc-1.0.0-1.x86_64.rpm"
