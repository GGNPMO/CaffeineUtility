using System.Runtime.InteropServices;

namespace CaffeineUtility;

/// <summary>
/// A deliberately small Windows keep-awake utility.
///
/// Windows maintains the requested execution state for the current process, so
/// this application does not need a timer, simulated mouse movement, or a
/// background polling loop.
/// </summary>
internal static class Program
{
    private const uint ExecutionStateContinuous = 0x80000000;
    private const uint ExecutionStateSystemRequired = 0x00000001;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint executionState);

    private static int Main()
    {
        Console.Title = "Caffeine - Keep Awake";

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Caffeine is supported on Windows only.");
            return 1;
        }

        if (!KeepSystemAwake())
        {
            var error = Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"Could not enable keep-awake mode (Windows error {error}).");
            return 1;
        }

        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            RenderStatus();
            Console.ReadLine();
        }
        finally
        {
            StopKeepingSystemAwake();
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        return 0;
    }

    private static bool KeepSystemAwake() =>
        SetThreadExecutionState(ExecutionStateContinuous | ExecutionStateSystemRequired) != 0;

    private static void StopKeepingSystemAwake()
    {
        // ES_CONTINUOUS without ES_SYSTEM_REQUIRED releases our request and
        // lets Windows return to the user's normal power-plan behavior.
        SetThreadExecutionState(ExecutionStateContinuous);
    }

    private static void RenderStatus()
    {
        var interactiveConsole = !Console.IsOutputRedirected;
        if (interactiveConsole)
            Console.Clear();

        if (interactiveConsole)
            Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  CAFFEINE");
        if (interactiveConsole)
            Console.ResetColor();
        Console.WriteLine("  Keep your Windows system awake");
        Console.WriteLine();
        if (interactiveConsole)
            Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  [ACTIVE]");
        if (interactiveConsole)
            Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Sleep prevention  System idle sleep blocked");
        Console.WriteLine("  Display           Unchanged (your power plan applies)");
        Console.WriteLine("  Resource use      No timer or simulated input");
        Console.WriteLine();
        Console.WriteLine("  Press Enter or Ctrl+C to stop.");
    }

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // Restore the power policy before allowing the process to terminate.
        StopKeepingSystemAwake();
        e.Cancel = false;
    }
}
