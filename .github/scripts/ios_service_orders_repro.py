#!/usr/bin/env python3
"""Reproduce the "Service Orders download locks the app" report on a booted
iOS simulator.

Flow, exactly as a user would do it - each step is numbered by screenshot:
    1. wait for the Main Menu to be on screen          -> 01-main-menu.png
    2. tap the "Settings" tile                          -> 02-settings-page.png
    3. type the folder path into "Host Folder"          -> 03-settings-host-folder.png
    4. tap the nav bar's "Home" item to return          -> 04-home-again.png
    5. tap the "File Manager" tile                      -> 05-file-manager.png
    6. tap "Download New Service Orders" and screenshot
       the Choose Files to Download sheet (or the empty-
       server "No Files Found" alert, which is the
       reported case)                                -> 06-download-service-orders-sheet.png
    7. tap "Download all files (N)" (skipped if the
       alert shown in step 6 was "No Files Found")  -> 07-download-service-orders-tapped.png
    8. wait for the result alert, screenshot it      -> 08-download-service-orders-result.png
    9. confirm OK on the result alert                 -> 09-download-confirmed.png
   10a. IF "No Files Found" (the reported case):
        tap "Download New Service Orders" again and
        require the same alert to re-appear - proves
        no other button was left working              -> 10-second-attempt.png
   10b. IF the SOs downloaded: wait for the Main
        Menu, then open Enter Readings / Search
        Accounts and verify they are tappable          -> 10..14-*.png

The user reports that on an empty server "click download service order after
click ok it is getting stuck eman no other button is working" - the loading
overlay is stranded above the main page and swallows all touches. The fix
(SelectSOs/FtpDownload now use async FTP calls and pop the overlay before
raising the next dialog) is committed separately; this reproducer fails the
workflow when the app stops responding after that OK tap, and passes when the
UI stays alive.

Accessibility-tree dumps are saved alongside as tree-*.json.

Only the Python standard library is used, so this runs on a bare macOS runner.
The journey helpers are shared with ios_settings_screenshot.py - run that
script first (or this one) so an unconfigured simulator has not been mutated.
"""

import argparse
import os
import shutil
import sys

# Reuse the exact same driver/finders as the settings journey. The module is
# import-safe (its __main__ guard only fires when executed as a script).
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ios_settings_screenshot import (  # noqa: E402
    Driver,
    center,
    find_label,
    find_tappable,
    frame_of,
    go_home,
    label_of,
    log,
    open_file_manager,
    open_settings,
    run,
    tap_tile,
    type_of,
    value_of,
    wait_for_home,
    fill_host_folder,
)


def find_prefix_label(nodes, prefix):
    """First node whose label starts with `prefix` (case-insensitive)."""
    wanted = prefix.strip().casefold()
    for node in nodes:
        if label_of(node).strip().casefold().startswith(wanted):
            frame = frame_of(node)
            if frame:
                return node, frame
    return None


def find_result_alert(driver):
    """A node carrying one of the labels a Service Orders download can end on."""
    prefixes = ("Success", "Error", "No Files Found", "No Internet Connection")
    def pred(nodes):
        for prefix in prefixes:
            hit = find_label(nodes, prefix)
            if hit:
                return hit
        return None
    return pred


def download_service_orders(driver):
    _node, frame = driver.wait_for(
        lambda nodes: find_tappable(nodes, "Download New Service Orders"),
        "the Download New Service Orders button",
    )
    driver.tap(*center(frame))
    driver.quiet(20)

    # Expected: the "Choose Files to Download" action sheet ("Download all
    # files (N)" / "Select files to download"). A fresh/empty host folder
    # instead raises "No Files Found" - that is still a valid result.
    sheet = driver.wait_for(
        lambda nodes: find_prefix_label(nodes, "Download all files")
        or find_label(nodes, "No Files Found")
        or find_label(nodes, "Choose Files to Download"),
        "the Choose Files to Download sheet",
        timeout=60,
    )
    log(f"  sheet shown: {label_of(sheet[0])!r}")
    driver.screenshot("06-download-service-orders-sheet.png")

    if find_label(driver.tree(), "No Files Found"):
        # Nothing to download - just dismiss and stop cleanly.
        ok = find_tappable(driver.tree(), "OK")
        if ok:
            log("  tap OK (no service orders in the host folder)")
            driver.tap(*center(ok[1]))
        return "no-files"

    target = find_prefix_label(driver.tree(), "Download all files")
    if target is None:
        raise SystemExit(
            "the Choose Files to Download sheet did not offer a download-all option"
        )
    _node, frame = target
    log(f"  tapping {label_of(_node)!r}")
    driver.tap(*center(frame))
    driver.quiet(10)
    driver.screenshot("07-download-service-orders-tapped.png")

    # The FTL downloads every selected file XML and inserts it into the DB.
    # Give the FTP flow a generous window - the alert is the end signal.
    alert = driver.wait_for(
        find_result_alert(driver),
        "the Service Orders download result alert",
        timeout=300,
    )
    alert_label = label_of(alert[0]).strip()
    log(f"  result: {alert_label!r}")
    driver.screenshot("08-download-service-orders-result.png")

    # Confirm any terminating alert so we land wherever the app takes us.
    if find_tappable(driver.tree(), "OK"):
        node, frame = find_tappable(driver.tree(), "OK")
        log(f"  tap OK on the {alert_label!r} alert")
        driver.tap(*center(frame))
        driver.quiet(20)
        driver.screenshot("09-download-confirmed.png")
    return alert_label


def probe_responsiveness(driver, so_result):
    """Verify the app did not lock up after the SO download/alert.

    Two scenarios, matching exactly what the user reports:

    * "no-files" - the server has no SOs (or none selected): the app shows
      "No Files Found", the user taps OK, and "no other button is working"
      right after. Probe: tap "Download New Service Orders" again and expect
      the same "No Files Found" alert to re-appear within 60s. Nothing can be
      stuck if the UI answers that tap.
    * success feedback - SOs downloaded: the app routes itself to the Main
      Menu. Probe: wait for the Main Menu, then Enter Readings / Search
      Accounts are tappable.
    """
    res = []

    if so_result == "no-files":
        _node, frame = driver.wait_for(
            lambda nodes: find_tappable(nodes, "Download New Service Orders"),
            "the Download New Service Orders button for the responsiveness probe",
            timeout=30,
        )
        driver.tap(*center(frame))
        try:
            driver.wait_for(
                lambda nodes: find_label(nodes, "No Files Found"),
                "the No Files Found alert on the second attempt",
                timeout=60,
            )
            log("  probe: No Files Found re-presented - UI responsive")
            driver.screenshot("10-second-attempt.png")
            ok = find_tappable(driver.tree(), "OK")
            if ok:
                driver.tap(*center(ok[1]))
                driver.quiet(10)
            res.append("second attempt: OK")
        except SystemExit as exc:
            try:
                driver.tree(tag="tree-so-probe-failed")
                driver.screenshot("99-failed-state.png")
            except Exception:
                pass
            res.append(f"second attempt: {exc}")
        except Exception as exc:
            res.append(f"second attempt: {exc!r}")
        return res

    # SO download finished - the app auto-routes to the Main Menu.
    try:
        driver.wait_for(
            lambda nodes: find_label(nodes, "Main Menu"),
            "the Main Menu after the Service Orders download",
            timeout=90,
        )
        driver.screenshot("10-home-after-so-download.png")
        res.append("main menu: OK")
    except SystemExit as exc:
        res.append(f"main menu after SO download: {exc}")
        try:
            driver.tree(tag="tree-so-mainmenu-failed")
            driver.screenshot("99-failed-state.png")
        except Exception:
            pass
        return res

    try:
        tap_tile(driver, "Enter Readings", "New Reading:",
                 "11-enter-readings-after-so.png")
        res.append("Enter Readings: OK")
        go_home(driver, "12-home-after-enter-readings.png")
    except SystemExit as exc:
        res.append(f"Enter Readings: {exc}")
        try:
            driver.tree(tag="tree-so-enter-readings-failed")
        except Exception:
            pass

    try:
        tap_tile(driver, "Search Accounts", "Missing Readings:",
                 "13-search-accounts-after-so.png")
        res.append("Search Accounts: OK")
        go_home(driver, "14-home-final.png")
    except SystemExit as exc:
        res.append(f"Search Accounts: {exc}")
        try:
            driver.tree(tag="tree-so-search-accounts-failed")
        except Exception:
            pass

    return res


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--udid", required=True, help="booted simulator UDID")
    parser.add_argument("--text", default="ABC Utilities",
                        help="folder path to type into Host Folder")
    parser.add_argument("--out", required=True, help="screenshot output directory")
    parser.add_argument("--tablet", action="store_true",
                        help="iPad geometry (unused by this journey)")
    parser.add_argument("--bundle-id",
                        default="com.creativetechusa.easyreader",
                        help="app bundle id, used only by the relaunch fallback")
    args = parser.parse_args()

    if not shutil.which("idb"):
        raise SystemExit("idb is not on PATH (brew install facebook/fb/idb)")
    os.makedirs(args.out, exist_ok=True)

    driver = Driver(args.udid, args.out, args.bundle_id)
    log(f"idb target: {args.udid}")
    run(["idb", "connect", args.udid])

    failure = None
    try:
        wait_for_home(driver)
        open_settings(driver)
        fill_host_folder(driver, args.text)

        driver.quiet(10)
        driver.screenshot("03-settings-host-folder.png")
        go_home(driver)

        open_file_manager(driver)
        so_result = download_service_orders(driver)

        results = probe_responsiveness(driver, so_result)
        for line in results:
            log(f"  responsiveness: {line}")
        locked = [line for line in results
                  if not line.rstrip().endswith(": OK")]
        if locked:
            failure = SystemExit(
                "app appears locked after the Service Orders download: "
                + "; ".join(locked)
            )
    except SystemExit as exc:
        failure = exc

    if failure:
        log(f"FAILED: {failure}")
        try:
            driver.tree(tag="tree-failed-state")
            driver.screenshot("99-failed-state.png")
        except Exception as dump_exc:
            log(f"  !! could not capture failure state: {dump_exc}")
        return 1

    log("done -> responsiveness verified after the Service Orders download")
    return 0


if __name__ == "__main__":
    sys.exit(main())
