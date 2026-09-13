using p3ppc.InputLibrary.Configuration;
using p3ppc.InputLibrary.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sigscan;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static p3ppc.InputLibrary.Logging;

namespace p3ppc.InputLibrary;

public unsafe sealed class Inputs : IInputHook
{
    private const string ControllerSignature =
        "48 8B 4B 08 C6 43 24 01 48 85 C9 74 ?? " +
        "48 8B 01 4C 8D 83 C0 06 00 00 BA 50 00 00 00 " +
        "48 89 7C 24 30 FF 50 48 8B F8";

    private const int ControllerHookOffset = 0x24;
    private const int ControllerStateOffset = 0x6C0;

    private const string KeyboardSignature =
        "48 89 4C 24 ?? 56 41 55 41 56";

    private const int KeyboardActionConfigOffset = 0x1C56448;
    private const int ControllerActionConfigOffset = 0x1C56200;
    private const int KeyboardDevicePointerOffset = 0x1E57348;
    private const int KeyboardStateOffset = 0x6B8;
    private const int ActionCount = 0x1D;
    private const int ActionEntrySize = 5;

    private IHook<KeyboardInputFunction>? _keyboardHook;
    private int _lastKeyboardInput;
    private readonly IReloadedHooks _hooks;
    private readonly Logging _utils;
    private readonly nuint _moduleBase;
    private Config _config;
    private IReverseWrapper<ControllerPollFunction>? _controllerReverseWrapper;
    private IAsmHook? _controllerHook;
    private int _lastControllerInput;

    public Inputs(
        IReloadedHooks hooks,
        Config configuration,
        Logging utils)
    {
        _hooks = hooks;
        _config = configuration;
        _utils = utils;

        using Process process =
            Process.GetCurrentProcess();

        ProcessModule mainModule =
            process.MainModule
            ?? throw new InvalidOperationException(
                "The P3P main module is unavailable.");

        _moduleBase =
            (nuint)mainModule.BaseAddress;

        if (sizeof(DiJoystickState) != 0x50)
        {
            throw new InvalidOperationException(
                $"Unexpected DIJOYSTATE size: " +
                $"0x{sizeof(DiJoystickState):X}.");
        }

        _utils.LogDebug("Scanning for the P3P keyboard function.");
        ScanAndInstallKeyboardHook();

        _utils.LogDebug("Scanning for the P3P controller function.");
        ScanAndInstallControllerHook();
    }

    public void UpdateConfiguration(Config configuration)
    {
        _config = configuration;
    }

    private void ScanAndInstallKeyboardHook()
    {
        try
        {
            using Process process =
                Process.GetCurrentProcess();

            ProcessModule mainModule =
                process.MainModule
                ?? throw new InvalidOperationException(
                    "The P3P main module is unavailable.");

            using var scanner =
                new Scanner(process, mainModule);

            var result =
                scanner.FindPattern(KeyboardSignature);

            if (!result.Found)
            {
                _utils.LogDebug(
                    "Unable to find the P3P keyboard input function.");
                return;
            }

            nuint moduleBase =
                (nuint)mainModule.BaseAddress;

            nuint signatureAddress =
                moduleBase + (nuint)result.Offset;

            nuint hookAddress =
                signatureAddress;

            _utils.LogDebug(
                $"Keyboard signature found at " +
                $"P3P.exe+0x{result.Offset:X}.");

            _utils.LogDebug(
                $"Installing keyboard hook at " +
                $"P3P.exe+0x{result.Offset:X}.");

            InstallKeyboardHook(hookAddress);
        }
        catch (Exception exception)
        {
            _utils.LogDebug(
                $"Keyboard signature scan failed: {exception.Message}");
        }
    }

    private void ScanAndInstallControllerHook()
    {
        try
        {
            using Process process =
                Process.GetCurrentProcess();

            ProcessModule mainModule =
                process.MainModule
                ?? throw new InvalidOperationException(
                    "The P3P main module is unavailable.");

            using var scanner =
                new Scanner(process, mainModule);

            var result =
                scanner.FindPattern(
                    ControllerSignature);

            if (!result.Found)
            {
                _utils.LogDebug(
                    "Unable to find the P3P controller polling function.");
                return;
            }

            nuint moduleBase =
                (nuint)mainModule.BaseAddress;

            nuint signatureAddress =
                moduleBase + (nuint)result.Offset;

            nuint hookAddress =
                signatureAddress + ControllerHookOffset;

            _utils.LogDebug(
                $"Controller signature found at " +
                $"P3P.exe+0x{result.Offset:X}.");

            _utils.LogDebug(
                $"Installing controller hook at " +
                $"P3P.exe+0x{result.Offset + ControllerHookOffset:X}.");

            InstallControllerHook(hookAddress);
        }
        catch (Exception exception)
        {
            _utils.LogDebug(
                $"Controller signature scan failed: {exception.Message}");
        }
    }

    private void InstallKeyboardHook(nuint hookAddress)
    {
        _keyboardHook = _hooks
            .CreateHook<KeyboardInputFunction>(
                KeyboardInput,
                (long)hookAddress)
            .Activate();

        _utils.LogDebug(
            "Successfully installed the P3P keyboard hook.");
    }

    private void KeyboardInput(uint* input)
    {
        int inputBeforeKeyboard =
            (int)input[1];

        _keyboardHook!.OriginalFunction(input);

        int inputAfterKeyboard =
            (int)input[1];

        int keyboardInput =
            inputAfterKeyboard & ~inputBeforeKeyboard;

        keyboardInput |=
            BuildConfiguredKeyboardInput();

        KeyboardMaskReceived(keyboardInput);
    }

    private int BuildConfiguredKeyboardInput()
    {
        nuint keyboardDevice =
            *(nuint*)(_moduleBase + (nuint)KeyboardDevicePointerOffset);

        if (keyboardDevice == 0)
            return 0;

        byte* keyboardState =
            (byte*)(keyboardDevice + (nuint)KeyboardStateOffset);

        int* keyboardConfig =
            (int*)(_moduleBase + (nuint)KeyboardActionConfigOffset);
        int* controllerConfig =
            (int*)(_moduleBase + (nuint)ControllerActionConfigOffset);

        int input = 0;

        for (int action = 0; action < ActionCount; action++)
        {
            int* keyboardAction =
                keyboardConfig + 1 + action * ActionEntrySize;

            bool pressed =
                IsKeyboardBindingPressed(
                    keyboardState,
                    keyboardAction[0],
                    keyboardAction[2])
                ||
                IsKeyboardBindingPressed(
                    keyboardState,
                    keyboardAction[1],
                    keyboardAction[3]);

            if (!pressed)
                continue;

            int* controllerAction =
                controllerConfig + 1 + action * ActionEntrySize;

            if (controllerAction[0] == 3)
                input |= controllerAction[2];

            if (controllerAction[1] == 3)
                input |= controllerAction[3];
        }

        return input;
    }

    private static bool IsKeyboardBindingPressed(
        byte* keyboardState,
        int bindingType,
        int keyCode)
    {
        if (bindingType != 1 ||
            (uint)keyCode >= 0x100)
        {
            return false;
        }

        return (keyboardState[keyCode] & 0x80) != 0;
    }

    private void InstallControllerHook(nuint hookAddress)
    {
        string[] controllerFunction =
        {
            "use64",

            "pushfq",
            "push rax",
            "push rcx",
            "push rdx",
            "push r8",
            "push r9",
            "push r10",
            "push r11",

            $"lea rcx, [rbx+{ControllerStateOffset:X}h]",
            "mov edx, eax",

            "sub rsp, 20h",
            _hooks.Utilities.GetAbsoluteCallMnemonics(
                ControllerStateReceived,
                out _controllerReverseWrapper),
            "add rsp, 20h",

            "pop r11",
            "pop r10",
            "pop r9",
            "pop r8",
            "pop rdx",
            "pop rcx",
            "pop rax",
            "popfq"
        };

        _controllerHook = _hooks
            .CreateAsmHook(
                controllerFunction,
                (long)hookAddress,
                AsmHookBehaviour.ExecuteFirst)
            .Activate();

        _utils.LogDebug("Successfully installed the P3P controller hook.");
    }

    private void KeyboardMaskReceived(int currentInput)
    {
        if (currentInput == _lastKeyboardInput)
            return;

        if (currentInput != 0)
        {
            InputHappened(
                currentInput,
                true,
                true);
        }
        else if (_lastKeyboardInput != 0)
        {
            InputHappened(
                0,
                false,
                true);
        }

        _lastKeyboardInput = currentInput;
    }

    private void ControllerStateReceived(
        nuint stateAddress,
        int result)
    {
        if (result < 0 || stateAddress == 0)
        {
            ReleaseAllControllerInputs();
            return;
        }

        var state = (DiJoystickState*)stateAddress;
        int currentInput = BuildControllerInput(state);

        if (currentInput == _lastControllerInput)
            return;

        if (currentInput != 0)
        {
            InputHappened(
                currentInput,
                true,
                false);
        }
        else if (_lastControllerInput != 0)
        {
            InputHappened(
                0,
                false,
                false);
        }

        _lastControllerInput = currentInput;
    }

    private void ReleaseAllControllerInputs()
    {
        if (_lastControllerInput != 0)
        {
            InputHappened(
                0,
                false,
                false);
        }

        _lastControllerInput = 0;
    }

    private static int BuildControllerInput(
        DiJoystickState* state)
    {
        int input = 0;

        AddIfPressed(ref input, state->Buttons[0], Input.Cross);
        AddIfPressed(ref input, state->Buttons[1], Input.Circle);
        AddIfPressed(ref input, state->Buttons[2], Input.Square);
        AddIfPressed(ref input, state->Buttons[3], Input.Triangle);
        AddIfPressed(ref input, state->Buttons[4], Input.LB);
        AddIfPressed(ref input, state->Buttons[5], Input.RB);
        AddIfPressed(ref input, state->Buttons[6], Input.Select);
        AddIfPressed(ref input, state->Buttons[7], Input.Start);

        AddPov(ref input, state->Pov[0]);

        return input;
    }

    private static void AddIfPressed(
        ref int input,
        byte rawButton,
        Input mappedInput)
    {
        if ((rawButton & 0x80) != 0)
            input |= (int)mappedInput;
    }

    private static void AddPov(
        ref int input,
        uint pov)
    {
        if ((pov & 0xFFFF) == 0xFFFF)
            return;

        if (pov >= 31500 || pov <= 4500)
            input |= (int)Input.Up;

        if (pov >= 4500 && pov <= 13500)
            input |= (int)Input.Right;

        if (pov >= 13500 && pov <= 22500)
            input |= (int)Input.Down;

        if (pov >= 22500 && pov <= 31500)
            input |= (int)Input.Left;
    }

    private static string FormatInputs(int inputMask)
    {
        var names = new List<string>();

        foreach (Input input in Enum.GetValues(typeof(Input)))
        {
            int value = (int)input;

            if (value == 0)
                continue;

            if ((inputMask & value) != 0)
                names.Add(input.ToString());
        }

        return names.Count > 0
            ? string.Join(" + ", names)
            : $"Unknown 0x{inputMask:X}";
    }

    private void InputHappened(
        int input,
        bool risingEdge,
        bool keyboard)
    {
        string device = keyboard
            ? "Keyboard"
            : "Controller";

        string edge = risingEdge
            ? "Pressed"
            : "Released";

        _utils.LogDebug(
            $"{device}: {FormatInputs(input)} {edge}");

        InvokeOnInput(
            input,
            risingEdge,
            keyboard);
    }

    [Function(CallingConventions.Microsoft)]
    private delegate void KeyboardInputFunction(
        uint* input);
    private delegate void ControllerPollFunction(
        nuint stateAddress,
        int result);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DiJoystickState
    {
        public int X;          // +0x00
        public int Y;          // +0x04
        public int Z;          // +0x08, shared L2/R2 axis
        public int RotationX;  // +0x0C
        public int RotationY;  // +0x10
        public int RotationZ;  // +0x14
        public int Slider0;    // +0x18
        public int Slider1;    // +0x1C
        public fixed uint Pov[4];       // +0x20
        public fixed byte Buttons[32];  // +0x30
    }

    /* IInputHook */
    public event OnInputEvent? OnInput;

    public void InvokeOnInput(
        int inputs,
        bool risingEdge,
        bool controlType)
    {
        OnInput?.Invoke(
            inputs,
            risingEdge,
            controlType);
    }
}
