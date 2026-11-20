#!/usr/bin/env python3
"""Exercise service-order editing, Open/Closed uploads, and home navigation.

Use a dedicated test host folder. This journey uploads real files to TOHOST.
The first available service file must contain parts and at least two Open
orders. One order is changed to Closed before uploading. The app is never relaunched.
"""
import argparse
import os
import re
import sys

from ios_settings_screenshot import (
    Driver, center, fill_host_folder, find_label, find_tappable, frame_of,
    label_of, log, open_file_manager, open_settings, run, tap_tile, type_of, value_of,
)
from ios_service_orders_repro import tap_back_arrow


def tap(driver, caption):
    _, frame = driver.wait_for(
        lambda nodes: find_tappable(nodes, caption), caption, timeout=90)
    driver.tap(*center(frame))


def expect(driver, caption):
    return driver.wait_for(
        lambda nodes: find_label(nodes, caption), caption, timeout=90)


def home(driver, screenshot, tablet):
    nodes = driver.tree()
    if not find_label(nodes, "Main Menu"):
        button = find_tappable(nodes, "Home")
        if button:
            driver.tap(*center(button[1]))
        elif tap_back_arrow(driver, tablet=tablet) is None:
            raise SystemExit("Home navigation failed; app was not relaunched")
    expect(driver, "Main Menu")
    driver.screenshot(screenshot)


def toggles(nodes):
    """Visible checkbox/switch controls, in displayed row order."""
    controls = []
    for node in nodes:
        frame = frame_of(node)
        kind = type_of(node).casefold()
        if frame and frame["width"] > 0 and frame["height"] > 0:
            if "checkbox" in kind or "switch" in kind:
                controls.append((node, frame))
    return sorted(controls, key=lambda item: (item[1]["y"], item[1]["x"]))


def row_toggle(nodes, row):
    """Match a row label to the checkbox in the column immediately to its left."""
    frame = row[1]
    candidates = [
        item for item in toggles(nodes)
        if item[1]["x"] < frame["x"]
        and abs(center(item[1])[1] - center(frame)[1]) < 20
    ]
    if len(candidates) != 1:
        raise SystemExit("Could not uniquely locate checkbox for " + label_of(row[0]))
    return candidates[0]


def checked(node):
    value = str(node.get("AXValue", node.get("value", ""))).casefold()
    if value in ("1", "true", "on", "checked"):
        return True
    if value in ("0", "false", "off", "unchecked"):
        return False
    raise SystemExit("Checkbox state is not exposed; cannot verify selection")


def select(driver, control):
    if not checked(control[0]):
        driver.tap(*center(control[1]))
    x, y = center(control[1])

    def selected(nodes):
        return next((item for item in toggles(nodes)
                     if abs(center(item[1])[0] - x) < 3
                     and abs(center(item[1])[1] - y) < 3
                     and checked(item[0])), None)

    driver.wait_for(selected, "checked selection", timeout=20)


def success(driver, screenshot):
    def result(nodes):
        for title in ("Success", "Failure", "Error", "Invalid Request",
                      "No Internet Connection", "No Files Found"):
            hit = find_label(nodes, title)
            if hit:
                return hit
        return None

    alert = driver.wait_for(result, "transfer result", timeout=300)
    driver.screenshot(screenshot)
    if label_of(alert[0]).casefold() != "success":
        raise SystemExit("Transfer failed: " + label_of(alert[0]))
    tap(driver, "OK")
    expect(driver, "Main Menu")


def download_first(driver):
    open_file_manager(driver)
    tap(driver, "Download New Service Orders")
    tap(driver, "Select files to download")
    expect(driver, "Select Service Orders")
    controls = driver.wait_for(toggles, "first service file checkbox")
    select(driver, controls[0])
    driver.screenshot("06-first-service-file-selected.png")
    tap(driver, "Download")
    success(driver, "07-service-file-downloaded.png")


def edit_order(driver):
    tap_tile(driver, "Service Orders", "Update Service Order",
             "08-service-orders.png", attempts=1)
    tap(driver, "Update Service Order")
    expect(driver, "Select Parts")
    tap(driver, "Select Parts")
    controls = driver.wait_for(toggles, "available service-order parts")
    select(driver, controls[0])
    driver.screenshot("09-part-selected.png")
    tap(driver, "X")
    tap(driver, "Enter Note")
    _, frame = driver.wait_for(
        lambda nodes: next(((node, frame_of(node)) for node in nodes
                            if "textfield" in type_of(node).casefold()
                            and value_of(node) == "Enter Note..."
                            and frame_of(node)), None),
        "note entry field")
    driver.tap(*center(frame))
    note = "Service order upload navigation regression"
    if driver.type_text(note).returncode != 0:
        raise SystemExit("Could not type service-order note")
    driver.wait_for(
        lambda nodes: any(note in str(node.get("AXValue", node.get("value", "")))
                          for node in nodes),
        "entered note")
    driver.screenshot("10-note-entered.png")
    driver.type_text("\n")  # Return dismisses the software keyboard.
    tap(driver, "Save Note")
    expect(driver, "Select Parts")
    driver.screenshot("11-note-saved.png")
    tap(driver, "Update Status")
    expect(driver, "Update Service Order Status")
    tap(driver, "CLOSED")
    expect(driver, "Update Status")
    expect(driver, "CLOSED")
    driver.screenshot("11-order-status-closed.png")


def upload_status(driver, status, number):
    open_file_manager(driver, f"{number}-file-manager-{status.lower()}.png")
    tap(driver, "Upload Service Orders")
    expect(driver, "Select Statuses")

    def status_row(nodes):
        for node in nodes:
            match = re.fullmatch(re.escape(status) + r"\s*\((\d+)\)",
                                 label_of(node).strip(), re.IGNORECASE)
            frame = frame_of(node)
            if match and frame:
                if int(match.group(1)) == 0:
                    raise SystemExit(f"Test file has no {status} service orders")
                return node, frame
        return None

    row = driver.wait_for(status_row, f"{status} status with orders")
    select(driver, row_toggle(driver.tree(), row))
    # The application resets selections each time the popup opens.
    selected = [item for item in toggles(driver.tree()) if checked(item[0])]
    if len(selected) != 1:
        raise SystemExit(f"Expected only {status} to be selected")
    driver.screenshot(f"{number}-selected-{status.lower()}.png")
    tap(driver, "Upload")
    success(driver, f"{number}-uploaded-{status.lower()}.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--udid", required=True)
    parser.add_argument("--text", required=True, help="Dedicated test Host Folder")
    parser.add_argument("--out", required=True)
    parser.add_argument("--bundle-id", default="com.creativetechusa.easyreader")
    parser.add_argument("--tablet", action="store_true")
    args = parser.parse_args()
    os.makedirs(args.out, exist_ok=True)
    driver = Driver(args.udid, args.out, args.bundle_id)
    try:
        if run(["idb", "connect", args.udid]).returncode != 0:
            raise SystemExit("Could not connect to simulator")
        expect(driver, "Main Menu")
        open_settings(driver)
        fill_host_folder(driver, args.text)
        home(driver, "04-home.png", args.tablet)
        download_first(driver)
        edit_order(driver)
        home(driver, "12-home-after-note.png", args.tablet)
        upload_status(driver, "Open", "13")
        upload_status(driver, "Closed", "14")
        # Probe Service Orders first, then other main-page navigation.
        for caption, destination in (
            ("Service Orders", "Update Service Order"),
            ("File Manager", "Manage Readings"),
            ("Settings", "Host Folder:"),
            ("Enter Readings", "New Reading:"),
            ("Search Accounts", "Missing Readings:"),
        ):
            name = caption.lower().replace(" ", "-")
            tap_tile(driver, caption, destination,
                     f"15-probe-{name}.png", attempts=1)
            home(driver, f"16-home-after-{name}.png", args.tablet)
    except (Exception, SystemExit) as exc:
        log(f"FAILED: {exc}")
        driver.tree(tag="tree-upload-repro-failed")
        driver.screenshot("99-upload-repro-failed.png")
        return 1
    log("PASS: first file, parts, note, Open upload, Closed upload, navigation")
    return 0


if __name__ == "__main__":
    sys.exit(main())
