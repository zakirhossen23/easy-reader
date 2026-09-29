#!/usr/bin/env python3
"""Download the iOS screenshot artifact from GitHub Actions to your machine.

Examples
--------
    # newest "settings-screenshot" from the current branch -> ./artifacts/
    python .github/scripts/download_ios_artifacts.py

    # the built simulator app instead
    python .github/scripts/download_ios_artifacts.py -a ios-simulator-app

    # a specific run, into a specific folder, and open the image
    python .github/scripts/download_ios_artifacts.py --run 1234567890 -o tmp/ios --open

    # see what is available without downloading anything
    python .github/scripts/download_ios_artifacts.py --list

Auth
----
Artifacts are never public, so a token is required even for a public repo.
This picks one up from, in order: --token, $GH_TOKEN, $GITHUB_TOKEN, `gh auth
token`. Otherwise create a classic token with the `repo` scope at
https://github.com/settings/tokens and export it as GH_TOKEN.

Standard library only - no gh, jq or curl needed.
"""

import argparse
import io
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile

API = "https://api.github.com"
DEFAULT_WORKFLOW = "ios-settings-screenshot.yml"
DEFAULT_ARTIFACT = "settings-screenshot"


def log(msg):
    print(msg, flush=True)


def git(*args):
    try:
        out = subprocess.run(
            ["git", *args], capture_output=True, text=True, timeout=30
        )
    except (OSError, subprocess.SubprocessError):
        return ""
    return out.stdout.strip() if out.returncode == 0 else ""


# --------------------------------------------------------------- discovery --


def detect_repo():
    """owner/repo from the origin remote, handling https and ssh URLs."""
    url = git("remote", "get-url", "origin")
    if not url:
        return None
    match = re.search(r"github\.com[:/]+([^/]+)/([^/]+?)(?:\.git)?/?$", url)
    return f"{match.group(1)}/{match.group(2)}" if match else None


def detect_branch():
    branch = git("rev-parse", "--abbrev-ref", "HEAD")
    return "" if branch in ("", "HEAD") else branch


def detect_token(args):
    if args.token:
        return args.token
    for var in ("GH_TOKEN", "GITHUB_TOKEN"):
        if os.environ.get(var):
            return os.environ[var]
    if shutil.which("gh"):
        out = subprocess.run(
            ["gh", "auth", "token"], capture_output=True, text=True
        )
        if out.returncode == 0 and out.stdout.strip():
            return out.stdout.strip()
    return None


# ------------------------------------------------------------------- client --


class NoRedirect(urllib.request.HTTPRedirectHandler):
    """Surface 3xx responses instead of following them.

    GitHub's artifact endpoint redirects to a pre-signed blob URL that rejects
    requests carrying an Authorization header, so the redirect has to be
    followed deliberately and without the token.
    """

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise urllib.error.HTTPError(req.full_url, code, msg, headers, fp)


class Client:
    def __init__(self, token):
        self.token = token
        self.opener = urllib.request.build_opener()

    def _request(self, url, accept="application/vnd.github+json", auth=True):
        headers = {
            "Accept": accept,
            "User-Agent": "easy-reader-ci",
            "X-GitHub-Api-Version": "2022-11-28",
        }
        if auth and self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        return urllib.request.Request(url, headers=headers)

    def get_json(self, path):
        url = path if path.startswith("http") else API + path
        try:
            with self.opener.open(self._request(url), timeout=60) as resp:
                return json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            if exc.code == 401:
                raise SystemExit(
                    "GitHub rejected the token (401). Check GH_TOKEN."
                )
            if exc.code == 404:
                raise SystemExit(
                    f"Not found (404): {url}\n"
                    "Check --repo / --workflow, and that the token can read this repo."
                )
            body = exc.read().decode("utf-8", "replace")[:300]
            raise SystemExit(f"GitHub API error {exc.code} for {url}\n{body}")

    def get_bytes(self, url, follow=True, auth=True):
        """Download a URL; returns (status, headers, bytes).

        `follow=False` surfaces the redirect so the caller can re-request the
        pre-signed location *without* credentials, and `auth=False` omits the
        token - signed blob URLs reject requests carrying an Authorization
        header that was not part of the signature.
        """
        opener = self.opener if follow else urllib.request.build_opener(NoRedirect)
        try:
            with opener.open(self._request(url, auth=auth), timeout=120) as resp:
                return resp.status, resp.headers, resp.read()
        except urllib.error.HTTPError as exc:
            if exc.code in (301, 302, 303, 307, 308):
                return exc.code, exc.headers, b""
            if exc.code == 403:
                raise SystemExit(
                    "GitHub returned 403 for the artifact download.\n"
                    "The token needs read access to Actions artifacts "
                    "(classic scope: repo; fine-grained: Actions: Read)."
                )
            raise SystemExit(f"Artifact download failed: HTTP {exc.code}")


# -------------------------------------------------------------------- runs --


def list_runs(client, repo, workflow, branch, limit=10):
    path = f"/repos/{repo}/actions/workflows/{workflow}/runs?per_page={limit * 2}"
    if branch:
        path += f"&branch={urllib.parse.quote(branch)}"
    runs = client.get_json(path).get("workflow_runs", [])
    runs.sort(key=lambda r: r.get("created_at") or "", reverse=True)
    return runs[:limit]


def run_artifacts(client, repo, run_id):
    return client.get_json(
        f"/repos/{repo}/actions/runs/{run_id}/artifacts?per_page=100"
    ).get("artifacts", [])


def pick_artifact(client, repo, runs, wanted):
    """Newest run that actually produced `wanted`, so in-progress runs are skipped."""
    wanted_lower = wanted.casefold()
    for run in runs:
        for artifact in run_artifacts(client, repo, run["id"]):
            if artifact["name"].casefold() != wanted_lower:
                continue
            if artifact.get("expired"):
                log(f"  ! {artifact['name']} from run {run['id']} has expired - skipping")
                continue
            return run, artifact
    return None, None


# ------------------------------------------------------------------ output --


def human_size(num):
    for unit in ("B", "KB", "MB", "GB"):
        if num < 1024 or unit == "GB":
            return f"{num:.0f} {unit}" if unit == "B" else f"{num:.1f} {unit}"
        num /= 1024.0


def safe_extract(zf, dest):
    dest = os.path.abspath(dest)
    for member in zf.infolist():
        target = os.path.abspath(os.path.join(dest, member.filename))
        if target != dest and not target.startswith(dest + os.sep):
            raise SystemExit(f"Refusing unsafe zip entry: {member.filename}")
    zf.extractall(dest)


def open_file(path):
    try:
        if platform.system() == "Windows":
            os.startfile(path)  # noqa: S606
        elif platform.system() == "Darwin":
            subprocess.Popen(["open", path])
        else:
            subprocess.Popen(["xdg-open", path])
    except OSError as exc:
        log(f"  (could not open automatically: {exc})")


# -------------------------------------------------------------------- main --


def main():
    parser = argparse.ArgumentParser(
        description="Download a GitHub Actions artifact from the iOS workflow."
    )
    parser.add_argument("-r", "--repo", help="owner/name (default: origin remote)")
    parser.add_argument("-w", "--workflow", default=DEFAULT_WORKFLOW)
    parser.add_argument("-a", "--artifact", default=DEFAULT_ARTIFACT)
    parser.add_argument("-b", "--branch", help="default: current branch")
    parser.add_argument("--any-branch", action="store_true", help="ignore the branch")
    parser.add_argument("--run", help="workflow run id to download from")
    parser.add_argument("-o", "--out", default="artifacts", help="output directory")
    parser.add_argument("-t", "--token", help="GitHub token")
    parser.add_argument("--list", action="store_true", help="list runs and exit")
    parser.add_argument("--open", action="store_true", help="open the newest image")
    parser.add_argument("--keep-zip", action="store_true", help="keep the .zip too")
    args = parser.parse_args()

    repo = args.repo or detect_repo()
    if not repo:
        raise SystemExit("Could not determine the repo - pass --repo owner/name.")
    branch = "" if args.any_branch else (args.branch or detect_branch())

    token = detect_token(args)
    if not token:
        raise SystemExit(
            "No GitHub token found. Artifacts always require auth.\n"
            "  Create a classic token with the 'repo' scope:\n"
            "    https://github.com/settings/tokens\n"
            "  then:  export GH_TOKEN=ghp_your_token\n"
            "  (or install the GitHub CLI and run 'gh auth login')"
        )

    client = Client(token)
    log(f"repo:     {repo}")
    log(f"workflow: {args.workflow}")
    log(f"branch:   {branch or '(any)'}")

    explicit_run = bool(args.run)
    if explicit_run:
        runs = [client.get_json(f"/repos/{repo}/actions/runs/{args.run}")]
    else:
        runs = list_runs(client, repo, args.workflow, branch)
        if not runs:
            raise SystemExit(
                "No runs found for that workflow/branch. "
                "Has the workflow run yet? (--list, or --any-branch)"
            )

    if args.list:
        log("")
        for run in runs:
            arts = run_artifacts(client, repo, run["id"])
            names = ", ".join(
                f"{a['name']} ({human_size(a['size_in_bytes'])}"
                f"{', expired' if a.get('expired') else ''})"
                for a in arts
            ) or "no artifacts"
            log(
                f"  {run['id']}  {(run.get('conclusion') or run.get('status') or '?'):<10} "
                f"{run['created_at']}  {(run.get('head_commit') or {}).get('message', '').splitlines()[0][:50]}"
            )
            log(f"              -> {names}")
        return 0

    if explicit_run:
        run = runs[0]
        artifacts = run_artifacts(client, repo, run["id"])
        artifact = next(
            (a for a in artifacts if a["name"].casefold() == args.artifact.casefold()),
            None,
        )
        if artifact is None:
            available = ", ".join(a["name"] for a in artifacts) or "none"
            raise SystemExit(f"Run {args.run} has no '{args.artifact}'. Available: {available}")
    else:
        run, artifact = pick_artifact(client, repo, runs, args.artifact)
        if artifact is None:
            raise SystemExit(
                f"No '{args.artifact}' artifact found in the last {len(runs)} run(s). "
                "Try --list."
            )

    commit = (run.get("head_commit") or {}).get("message", "").splitlines()[0]
    log("")
    log(f"run:      {run['id']}  ({run.get('conclusion') or run.get('status')})")
    log(f"commit:   {commit[:70]}")
    log(f"artifact: {artifact['name']}  {human_size(artifact['size_in_bytes'])}")
    log(f"page:     {run.get('html_url')}")

    status, headers, data = client.get_bytes(
        artifact["archive_download_url"], follow=False
    )
    if status in (301, 302, 303, 307, 308):
        location = headers.get("Location")
        if not location:
            raise SystemExit("Redirect without a Location header.")
        log("  following the pre-signed URL (token deliberately omitted)")
        _, _, data = client.get_bytes(location, auth=False)
    if not data:
        raise SystemExit("Downloaded an empty archive.")

    os.makedirs(args.out, exist_ok=True)
    if args.keep_zip:
        zip_path = os.path.join(args.out, f"{artifact['name']}.zip")
        with open(zip_path, "wb") as fh:
            fh.write(data)
        log(f"  saved {zip_path}")

    with zipfile.ZipFile(io.BytesIO(data)) as zf:
        safe_extract(zf, args.out)
        extracted = [n for n in zf.namelist() if not n.endswith("/")]

    log("")
    log(f"Extracted {len(extracted)} file(s) into {os.path.abspath(args.out)}:")
    images = []
    for name in sorted(extracted):
        path = os.path.join(args.out, name)
        size = os.path.getsize(path) if os.path.exists(path) else 0
        log(f"  {name}  ({human_size(size)})")
        if name.lower().endswith(".png"):
            images.append(path)

    if images:
        log("")
        log(f"Screenshot ready -> {os.path.abspath(images[0])}")
        if args.open:
            open_file(os.path.abspath(images[0]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
