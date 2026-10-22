using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using MonitorBrightness.Native;

namespace MonitorBrightness.Services;

internal sealed record DiagnosticResult(string Report, string[] Candidates);

internal static class MonitorDiagnostics
{
    // Called by a single worker task. Handles never escape this scan.
    internal static DiagnosticResult Collect(CancellationToken cancellationToken)
    {
        var report = new StringBuilder();
        var candidates = new List<string>();
        var readableCount = 0;
        report.AppendLine("Monitor Brightness - M2 READ-ONLY diagnostics");
        report.AppendLine($"Scan time (UTC): {DateTimeOffset.UtcNow:O}");
        report.AppendLine("No brightness writes. The popup slider remains DEMO.");
        report.AppendLine("External/internal identity: AMBIGUOUS - these APIs do not establish HDMI/DP versus laptop panel.");
        report.AppendLine("Primary status, descriptions, and enumeration order are NOT proof of external identity.");
        report.AppendLine("Match display names, desktop bounds, and monitor descriptions to Windows Display settings.");
        report.AppendLine("Physical entry numbers are valid only for this scan; do not reuse them after a reconnect.");
        report.AppendLine("No hardware target is selected automatically. No serial numbers or device paths are included.");
        report.AppendLine();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var logicalMonitors = new List<nint>();
            Exception? callbackError = null;
            PhysicalMonitorNative.MonitorEnumCallback callback = (monitor, _, _, _) =>
            {
                try
                {
                    logicalMonitors.Add(monitor);
                    return true;
                }
                catch (Exception exception)
                {
                    // Do not let managed exceptions cross the native callback boundary.
                    callbackError = exception;
                    return false;
                }
            };
            var enumerated = PhysicalMonitorNative.EnumDisplayMonitors(0, 0, callback, 0);
            var enumerationError = Marshal.GetLastPInvokeError();
            GC.KeepAlive(callback);
            if (callbackError is not null)
            {
                throw new InvalidOperationException("Monitor enumeration callback failed.", callbackError);
            }

            if (!enumerated)
            {
                report.AppendLine(Failure("EnumDisplayMonitors", enumerationError));
                report.AppendLine("This API does not document extended last-error information.");
            }
            else
            {
                report.AppendLine($"EnumDisplayMonitors: OK; logical monitors = {logicalMonitors.Count}");
                for (var index = 0; index < logicalMonitors.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    readableCount += InspectLogicalMonitor(logicalMonitors[index], index + 1, report, candidates, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            report.AppendLine("Scan cancelled; results are partial. Acquired handles were submitted for release.");
        }
        catch (Exception exception)
        {
            report.AppendLine($"Scan stopped: {exception.GetType().Name}: {exception.Message}");
        }

        report.AppendLine();
        report.AppendLine($"Physical entries found: {candidates.Count}; valid brightness reads: {readableCount}.");
        report.AppendLine(readableCount == 0
            ? "No usable brightness read established. DDC/CI is unsupported or unavailable on this path; stop and review errors."
            : "Read support observed only for entries marked OK. External identity still needs your explicit confirmation.");
        report.AppendLine("A read failure may mean DDC/CI is disabled, unsupported, disconnected, or blocked by a dock/driver.");
        report.AppendLine("No automatic retry or fallback was attempted. Review every API result separately.");
        return new DiagnosticResult(report.ToString(), candidates.ToArray());
    }

    private static int InspectLogicalMonitor(nint logicalMonitor, int index, StringBuilder report,
        List<string> candidates, CancellationToken cancellationToken)
    {
        report.AppendLine();
        var info = new PhysicalMonitorNative.MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<PhysicalMonitorNative.MonitorInfo>()
        };
        var logicalName = $"Logical display {index} (name unavailable)";
        if (PhysicalMonitorNative.GetMonitorInfoW(logicalMonitor, ref info))
        {
            logicalName = info.DeviceName;
            var bounds = info.Monitor;
            report.AppendLine($"Logical display {index}: {logicalName}");
            report.AppendLine($"  GetMonitorInfoW: OK; bounds=({bounds.Left},{bounds.Top})-({bounds.Right},{bounds.Bottom}); primary={(info.Flags & 1) != 0}");
            for (uint deviceIndex = 0; ; deviceIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var device = new PhysicalMonitorNative.DisplayDevice
                {
                    Size = (uint)Marshal.SizeOf<PhysicalMonitorNative.DisplayDevice>()
                };
                if (!PhysicalMonitorNative.EnumDisplayDevicesW(logicalName, deviceIndex, ref device, 0))
                {
                    report.AppendLine($"  EnumDisplayDevicesW: ended after {deviceIndex} entries (FALSE also means end of enumeration; no documented last-error).");
                    break;
                }

                report.AppendLine($"  Windows monitor entry: {device.DeviceName}; {device.Description}; flags=0x{device.StateFlags:X8}");
            }
            report.AppendLine("  Windows monitor entries above are descriptive only; their order is not matched to physical handles.");
        }
        else
        {
            var error = Marshal.GetLastPInvokeError();
            report.AppendLine(logicalName);
            report.AppendLine("  " + Failure("GetMonitorInfoW", error));
            report.AppendLine("  This API does not document extended last-error information; mapping is unavailable.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!PhysicalMonitorNative.GetNumberOfPhysicalMonitorsFromHMONITOR(logicalMonitor, out var count))
        {
            var error = Marshal.GetLastPInvokeError();
            report.AppendLine("  " + Failure("GetNumberOfPhysicalMonitorsFromHMONITOR", error));
            return 0;
        }

        report.AppendLine($"  GetNumberOfPhysicalMonitorsFromHMONITOR: OK; count={count}");
        if (count == 0)
        {
            report.AppendLine("  No physical monitor handles available for this display.");
            return 0;
        }

        var physicalMonitors = new PhysicalMonitorNative.PhysicalMonitor[checked((int)count)];
        var acquired = false;
        var readableCount = 0;
        try
        {
            acquired = PhysicalMonitorNative.GetPhysicalMonitorsFromHMONITOR(logicalMonitor, count, physicalMonitors);
            if (!acquired)
            {
                var error = Marshal.GetLastPInvokeError();
                report.AppendLine("  " + Failure("GetPhysicalMonitorsFromHMONITOR", error));
                return 0;
            }

            report.AppendLine("  GetPhysicalMonitorsFromHMONITOR: OK");
            for (var physicalIndex = 0; physicalIndex < physicalMonitors.Length; physicalIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var monitor = physicalMonitors[physicalIndex];
                var candidate = $"{logicalName} -> physical {physicalIndex + 1}: {monitor.Description}";
                candidates.Add(candidate);
                report.AppendLine("  " + candidate);
                if (PhysicalMonitorNative.GetMonitorCapabilities(monitor.Handle, out var capabilities, out var temperatures))
                {
                    report.AppendLine($"    GetMonitorCapabilities: OK; flags=0x{capabilities:X8}; color-temperature flags=0x{temperatures:X8}");
                    report.AppendLine($"    MC_CAPS_BRIGHTNESS advertised: {(capabilities & PhysicalMonitorNative.BrightnessCapability) != 0}");
                }
                else
                {
                    var error = Marshal.GetLastPInvokeError();
                    report.AppendLine("    " + Failure("GetMonitorCapabilities", error));
                }

                // Capability reporting can be incomplete, so try the read independently once.
                cancellationToken.ThrowIfCancellationRequested();
                if (PhysicalMonitorNative.GetMonitorBrightness(monitor.Handle, out var minimum, out var current, out var maximum))
                {
                    report.AppendLine($"    GetMonitorBrightness: OK; min={minimum}, current={current}, max={maximum} (raw hardware values)");
                    if (maximum <= minimum || current < minimum || current > maximum)
                    {
                        report.AppendLine("    Unusable/inconsistent brightness range; percentage is unavailable.");
                    }
                    else
                    {
                        var percent = 100.0 * (current - minimum) / (maximum - minimum);
                        report.AppendLine(FormattableString.Invariant($"    Normalized current brightness: {percent:F1}%"));
                        readableCount++;
                    }
                }
                else
                {
                    var error = Marshal.GetLastPInvokeError();
                    report.AppendLine("    " + Failure("GetMonitorBrightness", error));
                    report.AppendLine("    Current brightness unavailable; no substitute value is shown.");
                }
            }
        }
        finally
        {
            // Release every returned handle, even after partial acquisition, cancellation, or a failed read.
            // Do not cancel cleanup or stop releasing later handles if one release fails.
            for (var physicalIndex = 0; physicalIndex < physicalMonitors.Length; physicalIndex++)
            {
                var handle = physicalMonitors[physicalIndex].Handle;
                // Success establishes ownership even when the returned value is zero.
                // Only skip untouched zero slots after a failed acquisition.
                if (!acquired && handle == 0)
                {
                    continue;
                }

                try
                {
                    if (PhysicalMonitorNative.DestroyPhysicalMonitor(handle))
                    {
                        report.AppendLine($"  DestroyPhysicalMonitor (physical {physicalIndex + 1}): OK");
                    }
                    else
                    {
                        var error = Marshal.GetLastPInvokeError();
                        report.AppendLine("  " + Failure($"DestroyPhysicalMonitor (physical {physicalIndex + 1})", error));
                    }
                }
                catch (Exception exception)
                {
                    report.AppendLine($"  Handle release failed for physical {physicalIndex + 1}: {exception.Message}");
                }
            }
        }

        return readableCount;
    }

    private static string Failure(string api, int error)
    {
        var message = error == 0 ? "No extended error was supplied." : new Win32Exception(error).Message;
        return $"{api}: FAILED; Win32={error} (0x{error:X8}): {message}";
    }
}
