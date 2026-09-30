#!/usr/bin/env python3
"""Drive EasyReader through a user journey on a booted iOS Simulator.

Flow, exactly as a user would do it - each screenshot is numbered by step:
    1. wait for the Main Menu to be on screen          -> 01-main-menu.png
    2. tap the "Settings" tile                         -> 02-settings-page.png
    3. type the folder path into "Host Folder"         -> 03-settings-host-folder.png
    4. tap the nav bar's "Home" item to return         -> 04-home-again.png
    5. tap the "File Manager" tile                     -> 05-file-manager.png
    6. tap "Download New Readings", screenshot the
       dialog it raises                                -> 06-download-readings-alert.png
    7. tap "Get from Cloud", screenshot whatever the
       server returns                                -> 07-download-from-cloud.png
    8. tap the readings file (default "Test File.csv"),
       screenshot the download result, confirm any
       Success dialog                               -> 08-download-test-file.png

Accessibility-tree dumps are saved alongside as tree-*.json (not numbered, so
the numbers belong to the screenshots alone).

Device lifecycle (boot / install / launch) is done with `xcrun simctl` by the
workflow. Everything that touches the UI goes through `idb ui` so that elements
are located from the accessibility tree instead of hard-coded pixels.

Only the Python standard library is used, so this runs on a bare macOS runner.
"""

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time

# ---------------------------------------------------------------- plumbing --


def log(msg):
    print(f"[ui] {msg}", flush=True)


def run(cmd, timeout=180):
    """Run a command, echo it, and never raise on a non-zero exit."""
    log("$ " + " ".join(cmd))
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
    except subprocess.TimeoutExpired:
        log(f"  !! timed out after {timeout}s")
        return subprocess.CompletedProcess(cmd, 124, "", "timeout")
    err = (proc.stderr or "").strip()
    if proc.returncode != 0 and err:
        log(f"  !! exit {proc.returncode}: {err.splitlines()[0][:300]}")
    return proc


# ------------------------------------------------------- accessibility tree --


def frame_of(node):
    """Return {'x','y','width','height'} for a node, or None.

    Handles both the structured `frame` object and the legacy `AXFrame`
    string ("{{x, y}, {width, height}}") so idb output format changes do not
    break the run.
    """
    frame = node.get("frame")
    if isinstance(frame, dict) and {"x", "y", "width", "height"} <= set(frame):
        try:
            return {k: float(frame[k]) for k in ("x", "y", "width", "height")}
        except (TypeError, ValueError):
            pass
    ax = node.get("AXFrame")
    if isinstance(ax, str):
        m = re.search(
            r"\{\{\s*(-?[\d.]+),\s*(-?[\d.]+)\s*\},\s*\{\s*(-?[\d.]+),\s*(-?[\d.]+)\s*\}\}",
            ax,
        )
        if m:
            x, y, w, h = (float(g) for g in m.groups())
            return {"x": x, "y": y, "width": w, "height": h}
    return None


def label_of(node):
    for key in ("AXLabel", "label", "AXTitle", "title"):
        val = node.get(key)
        if isinstance(val, str) and val.strip():
            return val
    return ""


def value_of(node):
    for key in ("AXValue", "value"):
        val = node.get(key)
        if isinstance(val, str):
            return val
    return ""


def type_of(node):
    return str(node.get("type") or node.get("role") or "")


def center(frame):
    return frame["x"] + frame["width"] / 2, frame["y"] + frame["height"] / 2


def find_label(nodes, text):
    """Exact (case-insensitive) label match, preferring static text."""
    wanted = text.strip().casefold()
    matches = [n for n in nodes if label_of(n).strip().casefold() == wanted]
    if not matches:
        return None
    with_frame = [n for n in matches if frame_of(n)]
    if not with_frame:
        return None
    with_frame.sort(key=lambda n: 0 if "statictext" in type_of(n).lower() else 1)
    node = with_frame[0]
    return node, frame_of(node)


def find_tappable(nodes, text):
    """A tappable node carrying exactly this label, preferring real buttons.

    Nav bar items (the "Home" toolbar item) show up as buttons, but a plain
    label works too, so fall back to whatever carries the label.
    """
    wanted = text.strip().casefold()
    matches = [
        (n, f)
        for n, f in ((n, frame_of(n)) for n in nodes)
        if f and label_of(n).strip().casefold() == wanted
    ]
    if not matches:
        return None
    matches.sort(key=lambda nf: 0 if "button" in type_of(nf[0]).lower() else 1)
    return matches[0]


def text_fields(nodes):
    return [
        (n, f)
        for n, f in ((n, frame_of(n)) for n in nodes)
        if f and "textfield" in type_of(n).lower()
    ]


# ------------------------------------------------------------------ driver --


class Driver:
    def __init__(self, udid, out_dir, bundle_id):
        self.udid = udid
        self.out_dir = out_dir
        self.bundle_id = bundle_id

    def idb(self, *args, timeout=180):
        return run(["idb", *args, "--udid", self.udid], timeout=timeout)

    def tree(self, tag=None):
        proc = self.idb("ui", "describe-all")
        raw = proc.stdout or ""
        start, end = raw.find("["), raw.rfind("]")
        if start < 0 or end < 0:
            log("  !! describe-all produced no JSON array")
            return []
        try:
            nodes = json.loads(raw[start : end + 1])
        except json.JSONDecodeError as exc:
            log(f"  !! could not parse describe-all output: {exc}")
            return []
        if tag:
            path = os.path.join(self.out_dir, f"tree-{tag}.json")
            with open(path, "w", encoding="utf-8") as fh:
                json.dump(nodes, fh, indent=2)
        return nodes

    def quiet(self, seconds=15):
        self.idb("ui", "quiet", str(seconds), timeout=seconds + 60)

    def tap(self, x, y):
        log(f"  tap ({x:.0f}, {y:.0f})")
        return self.idb("ui", "tap", str(int(round(x))), str(int(round(y))))

    def type_text(self, text):
        log(f"  type {text!r}")
        return self.idb("ui", "text", text)

    def screenshot(self, name):
        path = os.path.join(self.out_dir, name)
        run(["xcrun", "simctl", "io", self.udid, "screenshot", path])
        log(f"  screenshot -> {name}")
        return path

    def wait_for(self, predicate, what, timeout=90):
        deadline = time.time() + timeout
        while True:
            nodes = self.tree()
            hit = predicate(nodes)
            if hit:
                log(f"  found {what}")
                return hit
            if time.time() >= deadline:
                raise SystemExit(f"timed out after {timeout}s waiting for {what}")
            time.sleep(2)


# ------------------------------------------------------------------- steps --


def wait_for_home(driver):
    def pred(nodes):
        return find_label(nodes, "Main Menu") or find_label(nodes, "Settings")

    driver.wait_for(pred, "the Main Menu", timeout=120)
    driver.quiet(15)
    driver.screenshot("01-main-menu.png")


def tap_tile(driver, caption, expect, screenshot=None):
    """Tap a Main Menu tile by its caption, then wait for the destination page.

    A tile is a Frame > Grid > (ImageButton above, caption Label below), and the
    button's bottom edge sits exactly where the caption starts. Prefer the real
    button hit target when the accessibility tree exposes one, otherwise tap the
    icon directly above the caption.
    """
    _node, frame = driver.wait_for(
        lambda nodes: find_label(nodes, caption), f"the {caption} tile"
    )

    button = closest_button_above(driver.tree(), frame)
    if button:
        x, y = center(button)
    else:
        x = center(frame)[0]
        y = frame["y"] - max(1.5 * frame["height"], 24)

    driver.tap(x, y)
    driver.quiet(20)
    driver.wait_for(lambda nodes: find_label(nodes, expect), expect, timeout=60)
    if screenshot:
        driver.screenshot(screenshot)


def open_settings(driver):
    tap_tile(driver, "Settings", "Host Folder:", "02-settings-page.png")


def go_home(driver):
    """Return to the Main Menu via the nav bar's "Home" item (PopToRootAsync).

    Every page carries that toolbar item, but if iOS does not surface it in the
    accessibility tree, fall back to relaunching the app: the Main Menu is the
    Shell root, so a fresh launch lands on it too.
    """
    try:
        _node, frame = driver.wait_for(
            lambda nodes: find_tappable(nodes, "Home"), "the Home button", timeout=30
        )
    except SystemExit:
        log(f"  !! no Home item in the tree - relaunching {driver.bundle_id}")
        run(["xcrun", "simctl", "terminate", driver.udid, driver.bundle_id])
        run(["xcrun", "simctl", "launch", driver.udid, driver.bundle_id])
    else:
        driver.tap(*center(frame))

    driver.quiet(15)
    driver.wait_for(
        lambda nodes: find_label(nodes, "Main Menu"), "the Main Menu", timeout=90
    )
    driver.screenshot("04-home-again.png")


def open_file_manager(driver):
    tap_tile(driver, "File Manager", "Manage Readings", "05-file-manager.png")


def download_new_readings(driver):
    """Tap "Download New Readings" and screenshot the dialog it raises.

    On a fresh install the next thing on screen is the action sheet
    ("How would you like to download accounts?"); if a readings file is
    already loaded it is the overwrite warning instead, which is confirmed
    so the sheet is reached. The sheet is left open for the caller to tap
    "Get from Cloud" next - nothing is dismissed here.
    """
    _node, frame = driver.wait_for(
        lambda nodes: find_tappable(nodes, "Download New Readings"),
        "the Download New Readings button",
    )
    driver.tap(*center(frame))
    driver.quiet(20)

    alert = driver.wait_for(
        lambda nodes: find_label(nodes, "How would you like to download accounts?")
        or find_label(nodes, "Warning!"),
        "the dialog raised by Download New Readings",
        timeout=60,
    )
    log(f"  dialog shown: {label_of(alert[0])!r}")
    driver.screenshot("06-download-readings-alert.png")

    # Overwrite path: a loaded readings file raises "Warning!" first.
    # Confirm it so the "How would you like..." sheet comes up.
    if label_of(alert[0]).strip().casefold() == "warning!":
        overwrite = find_tappable(driver.tree(), "Yes, overwrite file")
        if not overwrite:
            raise SystemExit('no "Yes, overwrite file" button to confirm with')
        log('  tap "Yes, overwrite file"')
        driver.tap(*center(overwrite[1]))
        driver.quiet(20)
        driver.wait_for(
            lambda nodes: find_label(
                nodes, "How would you like to download accounts?"
            ),
            'the "How would you like to download accounts?" sheet',
            timeout=60,
        )
        driver.screenshot("06-download-readings-alert.png")


def download_from_cloud(driver):
    """Tap "Get from Cloud" and screenshot whatever the server returns.

    After the tap the app connects to the FTP host set in Settings. What
    appears next depends on the server, so any of these is accepted:
      - "Download New Readings" file picker (files found, left open for the
        caller to pick a file next)
      - "No Files Found" / "No Host Folder" / "Error" /
        "No Internet Connection" / "Success" alert (dismissed back to the
        File Manager, then SystemExit so the run stops cleanly)
    The result is captured as 07.
    """
    cloud = find_tappable(driver.tree(), "Get from Cloud")
    if not cloud:
        raise SystemExit('no "Get from Cloud" button on the download sheet')
    log('  tap "Get from Cloud"')
    driver.tap(*center(cloud[1]))
    # FTP connect + listing takes a while; quiet also settles the loading UI.
    driver.quiet(25)

    nodes = driver.tree(tag="tree-after-get-from-cloud")
    for title in (
        "Download New Readings",
        "No Files Found",
        "No Host Folder",
        "Error",
        "No Internet Connection",
        "Success",
        "How would you like to download accounts?",
        "Warning!",
    ):
        hit = find_label(nodes, title)
        if hit:
            log(f"  result dialog: {label_of(hit[0])!r}")
            break
    driver.screenshot("07-download-from-cloud.png")

    if find_label(nodes, "Download New Readings") and (
        find_tappable(nodes, "Cancel")
        or any(
            label_of(n).strip().casefold().endswith(".csv")
            for n in nodes
            if frame_of(n)
        )
    ):
        log("  file picker is open, leaving it for the file tap")
        return

    # No file list - dismiss whatever alert/sheet came up instead, back to
    # the File Manager, then stop: there is no file to tap.
    dismiss_to_file_manager(driver)
    raise SystemExit("no readings files offered by the server")


def dismiss_to_file_manager(driver, rounds=5):
    """Tap Cancel/OK until the File Manager page is showing again."""
    for round_num in range(1, rounds + 1):
        nodes = driver.tree()
        back_on_page = bool(find_label(nodes, "Manage Readings"))
        dialog_open = any(
            find_label(nodes, t)
            for t in (
                "How would you like to download accounts?",
                "Warning!",
                "Download New Readings",
                "Download Imported Accounts",
                "No Files Found",
                "No Host Folder",
                "Error",
                "No Internet Connection",
                "Success",
            )
        )
        if back_on_page and not dialog_open:
            log("  dialogs dismissed, back on the File Manager")
            return
        cancel = find_tappable(nodes, "Cancel")
        if cancel:
            log(f"  tap Cancel (round {round_num})")
            driver.tap(*center(cancel[1]))
            driver.quiet(15)
            continue
        ok = find_tappable(nodes, "OK")
        if ok:
            log(f"  tap OK (round {round_num})")
            driver.tap(*center(ok[1]))
            driver.quiet(15)
            continue
        if back_on_page:
            log("  no Cancel/OK left and File Manager is showing - done")
            return
        raise SystemExit("no Cancel/OK button to dismiss the dialog with")

    raise SystemExit("the dialog would not dismiss after 5 taps")


def download_test_file(driver, filename):
    """Tap the readings file in the picker and capture the download result.

    The tap starts a real FTP download + CSV import, which can take a while,
    so a long settle is used before screenshotting 08. A "Success" alert is
    confirmed with OK; the app then returns to the Main Menu (09).
    """
    nodes = driver.tree()
    target = find_tappable(nodes, filename)
    picked = filename
    if not target:
        # Fall back to the first .csv option so a renamed server file does
        # not fail the whole run - but never tap Cancel by mistake.
        for node in nodes:
            label = label_of(node).strip()
            if (
                label
                and label.casefold().endswith(".csv")
                and frame_of(node)
                and "button" in type_of(node).lower()
            ):
                target = (node, frame_of(node))
                picked = label
                break
    if not target:
        raise SystemExit(f'no "{filename}" (or any .csv) button in the file picker')
    log(f'  tap "{picked}"')
    driver.tap(*center(target[1]))
    # Real download + CSV import + DB inserts; quiet also settles loading UI.
    driver.quiet(90)

    nodes = driver.tree(tag="tree-after-file-tap")
    for title in ("Success", "Error", "No Internet Connection", "Main Menu"):
        hit = find_label(nodes, title)
        if hit:
            log(f"  after download: {label_of(hit[0])!r}")
            break
    driver.screenshot("08-download-test-file.png")

    nodes = driver.tree()
    if find_label(nodes, "Success"):
        ok = find_tappable(nodes, "OK")
        if not ok:
            raise SystemExit('no OK button on the Success dialog')
        log('  tap OK on Success')
        driver.tap(*center(ok[1]))
        driver.quiet(20)
        driver.wait_for(
            lambda ns: find_label(ns, "Main Menu"),
            "the Main Menu after a successful download",
            timeout=90,
        )
        driver.screenshot("09-main-menu-after-download.png")
        log(f"  downloaded {picked!r} - readings are on device")
        return
    if find_label(nodes, "Main Menu"):
        driver.screenshot("09-main-menu-after-download.png")
        log(f"  downloaded {picked!r} - readings are on device")
        return
    if find_label(nodes, "Error") or find_label(nodes, "No Internet Connection"):
        raise SystemExit("the server reported an error for the file download")
    if find_label(nodes, "Download New Readings"):
        raise SystemExit(f'the tap on "{picked}" did not start the download')
    raise SystemExit("unknown state after tapping the readings file")


def fill_host_folder(driver, text):
    _node, label_frame = driver.wait_for(
        lambda nodes: find_label(nodes, "Host Folder:"), "the Host Folder label"
    )
    nodes = driver.tree(tag="tree-settings-before-typing")

    target = pick_host_folder_field(nodes, label_frame)
    if target is None:
        raise SystemExit("could not locate the Host Folder text field")
    _node, frame = target
    log(f"  host folder field at {frame}")

    for attempt in (1, 2):
        driver.tap(*center(frame))
        time.sleep(1.5)
        driver.type_text(text)
        time.sleep(1.5)
        # Return dismisses the software keyboard so it does not cover the
        # screenshot; a single-line Entry drops the character itself.
        driver.type_text("\n")
        time.sleep(1.5)

        current = driver.tree(tag=f"tree-settings-after-typing-{attempt}")
        if text_was_entered(current, text):
            log(f"  verified: Host Folder == {text!r}")
            return
        log(f"  !! attempt {attempt}: Host Folder field does not hold {text!r} yet")

    raise SystemExit(f"typing {text!r} into the Host Folder field failed")


def closest_button_above(nodes, caption_frame):
    """Button sharing the caption's column whose bottom edge meets its top."""
    best = None
    left = caption_frame["x"] - 5
    right = caption_frame["x"] + caption_frame["width"] + 5
    for node in nodes:
        if "button" not in type_of(node).lower():
            continue
        frame = frame_of(node)
        if not frame:
            continue
        if not left <= center(frame)[0] <= right:
            continue
        gap = caption_frame["y"] - (frame["y"] + frame["height"])
        if gap < -10:  # not above the caption
            continue
        if best is None or abs(gap) < abs(best[0]):
            best = (gap, frame)
    return best[1] if best else None


def pick_host_folder_field(nodes, label_frame):
    """The Entry sits in the same 44pt row as its label, to the right of it."""
    label_x, label_y = center(label_frame)
    candidates = []
    for node, frame in text_fields(nodes):
        cx, cy = center(frame)
        if cx <= label_x:
            continue
        candidates.append((abs(cy - label_y), node, frame))

    if candidates:
        candidates.sort(key=lambda c: c[0])
        return candidates[0][1], candidates[0][2]

    # Fallback: the Host Folder Entry is the second field from the top
    # (Reader Description is first, Variance lives further down the page).
    fields = sorted(text_fields(nodes), key=lambda t: t[1]["y"])
    if len(fields) > 1:
        return fields[1]
    return fields[0] if fields else None


def text_was_entered(nodes, text):
    wanted = text.strip().casefold()
    for node in nodes:
        if "textfield" not in type_of(node).lower():
            continue
        if value_of(node).strip().casefold() == wanted:
            return True
    return False


# -------------------------------------------------------------------- main --


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--udid", required=True, help="booted simulator UDID")
    parser.add_argument("--text", required=True, help="folder path to type")
    parser.add_argument("--out", required=True, help="screenshot output directory")
    parser.add_argument(
        "--file",
        default="Test File.csv",
        help="readings file to tap in the Download New Readings picker",
    )
    parser.add_argument(
        "--bundle-id",
        default="com.creativetechusa.easyreader",
        help="app bundle id, used only by the relaunch fallback",
    )
    args = parser.parse_args()

    if not shutil.which("idb"):
        raise SystemExit("idb is not on PATH (brew install facebook/fb/idb)")
    os.makedirs(args.out, exist_ok=True)

    driver = Driver(args.udid, args.out, args.bundle_id)
    log(f"idb target: {args.udid}")
    # Starts (or reuses) the companion backing this simulator.
    run(["idb", "connect", args.udid])

    wait_for_home(driver)
    open_settings(driver)

    failure = None
    try:
        fill_host_folder(driver, args.text)
    except SystemExit as exc:
        failure = exc

    # Always capture the final state, even when the entry could not be verified,
    # so the uploaded artifact shows what the run actually produced.
    driver.quiet(10)
    final = driver.screenshot("03-settings-host-folder.png")

    if failure:
        log(f"FAILED: {failure}")
        return 1

    # Continue the journey from a known-good Settings page: back to the Main
    # Menu, then into the File Manager, then Download New Readings ->
    # Get from Cloud -> tap the readings file, capturing each dialog.
    try:
        go_home(driver)
        open_file_manager(driver)
        download_new_readings(driver)
        download_from_cloud(driver)
        download_test_file(driver, args.file)
    except SystemExit as exc:
        log(f"FAILED: {exc}")
        return 1

    log(f"done -> {final}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
