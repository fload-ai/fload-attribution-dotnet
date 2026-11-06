#!/usr/bin/env python3
"""Finds the .NET for iOS workload release that builds with a given Xcode.

Each Microsoft.iOS.Sdk pack names the one Xcode it accepts
(_RecommendedXcodeVersion in targets/Microsoft.iOS.Sdk.Versions.props). This
reads that file from the pack on nuget.org with HTTP range requests, so it
needs no download of the whole pack.

  pick-ios-workload.py <sdk band, e.g. 10.0.100> <xcode major.minor> [<xcode> ...]

Prints "<xcode> <ios workload version>" for the first Xcode (in the order
given) that has a release, newest release first. Exits 1 when none has.
"""
import json
import re
import struct
import sys
import urllib.request
import zlib

FLAT = "https://api.nuget.org/v3-flatcontainer"
WANTED = "targets/Microsoft.iOS.Sdk.Versions.props"


def get(url, start=None, end=None):
    request = urllib.request.Request(url)
    if start is not None:
        request.add_header("Range", f"bytes={start}-{'' if end is None else end}")
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read(), response.headers


def size_of(url):
    request = urllib.request.Request(url, method="HEAD")
    with urllib.request.urlopen(request, timeout=60) as response:
        return int(response.headers["Content-Length"])


def read_member(url, name):
    size = size_of(url)
    tail, _ = get(url, max(0, size - 65536))
    eocd = tail.rfind(b"PK\x05\x06")
    if eocd < 0:
        raise ValueError("no zip directory")
    cd_size, cd_offset = struct.unpack("<II", tail[eocd + 12:eocd + 20])
    directory, _ = get(url, cd_offset, cd_offset + cd_size - 1)
    i = 0
    while i < len(directory):
        (method, csize, nlen, xlen, clen, offset) = (
            struct.unpack("<H", directory[i + 10:i + 12])[0],
            struct.unpack("<I", directory[i + 20:i + 24])[0],
            *struct.unpack("<HHH", directory[i + 28:i + 34]),
            struct.unpack("<I", directory[i + 42:i + 46])[0],
        )
        entry = directory[i + 46:i + 46 + nlen].decode()
        if entry == name:
            header, _ = get(url, offset, offset + 29)
            lnlen, lxlen = struct.unpack("<HH", header[26:30])
            start = offset + 30 + lnlen + lxlen
            data, _ = get(url, start, start + csize - 1)
            return zlib.decompress(data, -15) if method == 8 else data
        i += 46 + nlen + xlen + clen
    raise KeyError(name)


def recommended_xcode(version):
    pack = f"microsoft.ios.sdk.net{band.split('.')[0]}.0_{'.'.join(version.split('.')[:2])}"
    url = f"{FLAT}/{pack}/{version}/{pack}.{version}.nupkg"
    props = read_member(url, WANTED).decode("utf-8", "replace")
    # Older packs set _RecommendedXcodeVersion; newer ones set RecommendedXcodeVersion
    # and point the underscored one at it.
    for tag in ("RecommendedXcodeVersion", "_RecommendedXcodeVersion"):
        found = re.search(rf"<{tag}>\s*(\d+(?:\.\d+)*)\s*</{tag}>", props)
        if found:
            return found.group(1)
    return None


band = sys.argv[1]
xcodes = sys.argv[2:]
releases, _ = get(f"{FLAT}/microsoft.net.sdk.ios.manifest-{band}/index.json")
versions = [v for v in json.loads(releases)["versions"] if "-" not in v][::-1]
cache = {}
for xcode in xcodes:
    for version in versions:
        # A release only builds with an Xcode of its own major version or newer.
        if int(version.split(".")[0]) > int(xcode.split(".")[0]):
            continue
        if version not in cache:
            try:
                cache[version] = recommended_xcode(version)
            except Exception as error:  # a pack missing on nuget.org, a network error
                print(f"skip {version}: {error}", file=sys.stderr)
                cache[version] = None
        if cache[version] == xcode:
            print(f"{xcode} {version}")
            sys.exit(0)
        # Releases are newest first: once they want an older Xcode, stop for this one.
        if cache[version] and tuple(map(int, cache[version].split("."))) < tuple(map(int, xcode.split("."))):
            break
sys.exit(1)
