#!/bin/bash
set -e
rm -rf pkg-arch
mkdir -p pkg-arch/usr/local/bin
cp -r build/linux/* pkg-arch/usr/local/bin/

BUILD_DATE=$(date +%s)
SIZE=$(du -sb pkg-arch/usr | cut -f1)
cat > pkg-arch/.PKGINFO << EOF
pkgname = vulkanmc
pkgver = 1.0.0
pkgrel = 1
packager = GitHub Actions
builddate = $BUILD_DATE
size = $SIZE
arch = x86_64
EOF

cd pkg-arch && find . | sort | mtree -c -p . > .MTREE || true
cd .. && tar -cf - pkg-arch | zstd -19 -T0 -c > vulkanmc-1.0.0-1-x86_64.pkg.tar.zst
echo "✅ Arch package created!"
