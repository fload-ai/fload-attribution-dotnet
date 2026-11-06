#!/usr/bin/env python3
"""A local stand-in for Fload's ingest API, for tests only. Never production.

It checks each request against the wire contract (Fload's
packages/shared-types/src/contracts/attribution-ingest.ts), answers like the
real routes (202 / 400 / 401 / 410 / 413), and appends every request it got
to a JSON Lines log.

  python3 stub.py --port 3901 --key fload_pk_test_local --log requests.jsonl

GET /__stub__/requests returns the log as a JSON array; DELETE clears it.
"""
import argparse
import json
import re
import sys
import threading
from datetime import datetime, timezone
import socketserver
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PHONE_ID = re.compile(r"^[A-Za-z0-9._:-]{8,128}$")
TOKEN = re.compile(r"^[A-Za-z0-9+/=_-]+$")
INSTANT = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")
APPLE_STATUSES = {"token", "token_expired", "unsupported_os", "token_error"}
PLAY_STATUSES = {"ok", "unavailable", "not_supported", "error"}
MAX_BODY = 2048


class Invalid(Exception):
    pass


def need(cond, message):
    if not cond:
        raise Invalid(message)


def text(value, name, lo, hi, trim=True, optional=False):
    if value is None and optional:
        return
    need(isinstance(value, str), f"{name} must be a string")
    v = value.strip() if trim else value
    # The contract counts UTF-16 units (JavaScript string length).
    n = len(v.encode("utf-16-le")) // 2
    need(lo <= n <= hi, f"{name} length {n} not in [{lo}, {hi}]")


def instant(value, name, optional=True):
    if value is None and optional:
        return
    need(isinstance(value, str) and INSTANT.match(value), f"{name} must be an ISO 8601 instant")
    datetime.fromisoformat(value.replace("Z", "+00:00"))


def phone_id(value, name):
    need(isinstance(value, str) and PHONE_ID.match(value), f"{name} must be a phone id")


def check_install(body):
    phone_id(body.get("requestId"), "requestId")
    phone_id(body.get("installReference"), "installReference")
    platform = body.get("platform")
    need(platform in ("ios", "android"), "platform must be ios or android")
    text(body.get("appId"), "appId", 1, 256)
    for field in ("appVersion", "appBuild", "osVersion", "deviceModel", "locale"):
        text(body.get(field), field, 1, 64, optional=True)
    sdk = body.get("sdk")
    need(isinstance(sdk, dict), "sdk must be an object")
    text(sdk.get("name"), "sdk.name", 1, 64)
    text(sdk.get("version"), "sdk.version", 1, 64)
    instant(body.get("firstLaunchAt"), "firstLaunchAt", optional=False)
    text(body.get("appUserId"), "appUserId", 1, 256, optional=True)
    apple, play = body.get("appleAds"), body.get("playReferrer")
    if platform == "ios":
        need(isinstance(apple, dict) and play is None, "iOS sends appleAds, Android sends playReferrer")
        need(apple.get("status") in APPLE_STATUSES, "appleAds.status unknown")
        token = apple.get("token")
        if token is not None:
            need(isinstance(token, str) and len(token) <= 4096 and TOKEN.match(token), "appleAds.token malformed")
        need((apple.get("status") == "token") == bool(token), 'token is sent exactly when status is "token"')
        instant(apple.get("tokenCreatedAt"), "appleAds.tokenCreatedAt")
    else:
        need(isinstance(play, dict) and apple is None, "iOS sends appleAds, Android sends playReferrer")
        need(play.get("status") in PLAY_STATUSES, "playReferrer.status unknown")
        text(play.get("referrer"), "playReferrer.referrer", 0, 1024, trim=False, optional=True)
        for field in ("referrerClickAt", "installBeginAt", "referrerClickServerAt", "installBeginServerAt"):
            instant(play.get(field), f"playReferrer.{field}")
        text(play.get("installVersion"), "playReferrer.installVersion", 1, 64, optional=True)
        gpi = play.get("googlePlayInstant")
        need(gpi is None or isinstance(gpi, bool), "playReferrer.googlePlayInstant must be a boolean")


def check_identify(body):
    phone_id(body.get("requestId"), "requestId")
    phone_id(body.get("installReference"), "installReference")
    need(body.get("platform") in ("ios", "android"), "platform must be ios or android")
    text(body.get("appUserId"), "appUserId", 1, 256)


ROUTES = {"/attribution/v1/installs": check_install, "/attribution/v1/identify": check_identify}


def make_handler(keys, revoked, log_path):
    lock = threading.Lock()
    seen = []

    def record(entry):
        with lock:
            seen.append(entry)
            if log_path:
                with open(log_path, "a", encoding="utf-8") as f:
                    f.write(json.dumps(entry) + "\n")

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, fmt, *args):
            sys.stderr.write("stub: " + (fmt % args) + "\n")

        def reply(self, status, payload):
            data = json.dumps(payload).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            if self.path == "/__stub__/requests":
                with lock:
                    return self.reply(200, list(seen))
            self.reply(404, {"error": "not_found"})

        def do_DELETE(self):
            if self.path == "/__stub__/requests":
                with lock:
                    seen.clear()
                return self.reply(200, {"cleared": True})
            self.reply(404, {"error": "not_found"})

        def do_POST(self):
            path = self.path.rstrip("/")
            length = int(self.headers.get("Content-Length") or 0)
            raw = self.rfile.read(length) if length else b""
            key = self.headers.get("X-Fload-Write-Key") or ""
            entry = {
                "path": path,
                "receivedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "keyPrefix": key[:12],
                "userAgent": self.headers.get("User-Agent"),
                "contentType": self.headers.get("Content-Type"),
            }
            if path not in ROUTES:
                return self.reply(404, {"error": "not_found"})
            if key in revoked:
                entry["status"] = 410
                record(entry)
                return self.reply(410, {"error": "revoked", "message": "write key revoked"})
            if not key.startswith("fload_pk_") or key not in keys:
                entry["status"] = 401
                record(entry)
                return self.reply(401, {"error": "unauthorized", "message": "unknown write key"})
            if length > MAX_BODY:
                entry["status"] = 413
                record(entry)
                return self.reply(413, {"error": "too_large", "message": "body over 2 KB"})
            try:
                body = json.loads(raw)
                need(isinstance(body, dict), "body must be an object")
                ROUTES[path](body)
            except (Invalid, ValueError) as error:
                entry.update(status=400, error=str(error), body=raw.decode("utf-8", "replace"))
                record(entry)
                return self.reply(400, {"error": "invalid_request", "message": str(error)})
            entry.update(status=202, body=body)
            record(entry)
            self.reply(202, {"accepted": True})

    return Handler


class StubServer(ThreadingHTTPServer):
    daemon_threads = True

    def server_bind(self):
        # HTTPServer.server_bind looks up the host's FQDN, which can stall for
        # many seconds; the stub does not need it.
        socketserver.TCPServer.server_bind(self)
        self.server_name, self.server_port = self.server_address[:2]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=3901)
    parser.add_argument("--key", action="append", default=[], help="an accepted write key (repeatable)")
    parser.add_argument("--revoked", action="append", default=[], help="a revoked write key: 410")
    parser.add_argument("--log", help="append each request to this JSON Lines file")
    args = parser.parse_args()
    keys = set(args.key or ["fload_pk_test_local"])
    server = StubServer((args.host, args.port), make_handler(keys, set(args.revoked), args.log))
    print(f"ingest stub on http://{args.host}:{args.port} (keys: {len(keys)})", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
