# Persona 3 Portable Input Library

A Reloaded-II dependency mod that exposes Persona 3 Portable's keyboard and controller input through a simple shared API for other mods.

This is a Persona 3 Portable port of `p4g64.InputLibrary64`, which itself is based on rirurin's original Input Library.

This mod does not add visible gameplay features by itself. It provides input events that other Reloaded-II mods can consume.

## What it does

The library reads Persona 3 Portable's input state and publishes logical game actions through Reloaded-II's controller system.

It supports:

* Keyboard input
* Controller buttons
* Controller D-pad input
* Press and release events
* Distinguishing keyboard input from controller input
* Multiple simultaneous logical inputs in one bitmask
* Persona 3 Portable's configurable keyboard bindings

The library reports **logical game actions**, not physical keyboard keys.

For example, if `C` is currently assigned to Cancel, pressing `C` is reported as the same logical action as pressing Circle/B on a controller.

The physical key itself is not exposed to consuming mods.

This allows mods using the library to automatically follow the player's Persona 3 Portable keyboard configuration.

## Supported inputs

| Input      |    Value | Typical meaning        |
| ---------- | -------: | ---------------------- |
| `Select`   | `0x0001` | Select / Back          |
| `Start`    | `0x0008` | Start / Menu           |
| `Up`       | `0x0010` | D-pad or menu Up       |
| `Right`    | `0x0020` | D-pad or menu Right    |
| `Down`     | `0x0040` | D-pad or menu Down     |
| `Left`     | `0x0080` | D-pad or menu Left     |
| `LB`       | `0x0400` | Left shoulder          |
| `RB`       | `0x0800` | Right shoulder         |
| `Triangle` | `0x1000` | Triangle action        |
| `Circle`   | `0x2000` | Circle / Cancel action |
| `Cross`    | `0x4000` | Cross / Confirm action |
| `Square`   | `0x8000` | Square action          |

These values are flags and may be combined in one event.

For example:

```text
Up + RB = 0x0810
```

## Requirements

### For players

* Persona 3 Portable PC
* Reloaded-II
* Reloaded.Hooks
* Reloaded.Memory.SigScan
* A mod that depends on this library

### For mod developers

* A Reloaded-II mod project
* A reference to `p3ppc.InputLibrary.Interfaces.dll`, or a project reference to `p3ppc.InputLibrary.Interfaces`
* `p3ppc.InputLibrary` listed as a Reloaded-II dependency

## Installation

Install this mod through Reloaded-II and enable it for Persona 3 Portable.

Because this is a dependency library, it will normally be installed alongside another mod that requires it.

The library has an **Enable Debug Logging** setting.

When enabled, Input Library will print diagnostic information such as signature scans, hook installation, and input events to the Reloaded-II console.

When disabled, Input Library remains silent during normal operation.

## Using the library in another mod

### 1. Reference the interface assembly

Reference:

```text
p3ppc.InputLibrary.Interfaces.dll
```

A project reference can be used during development:

```xml
<ItemGroup>
  <ProjectReference Include="..\p3ppc.InputLibrary.Interfaces\p3ppc.InputLibrary.Interfaces.csproj" />
</ItemGroup>
```

If the Input Library repository is located beside your mod repository, the path may instead look like:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\p3ppc.InputLibrary\p3ppc.InputLibrary.Interfaces\p3ppc.InputLibrary.Interfaces.csproj" />
</ItemGroup>
```

A direct DLL reference can also be used:

```xml
<ItemGroup>
  <Reference Include="p3ppc.InputLibrary.Interfaces">
    <HintPath>Libraries\p3ppc.InputLibrary.Interfaces.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

`Private` should normally remain `false` because the installed Input Library supplies the interface assembly at runtime.

### 2. Add the Reloaded-II dependency

Add the library's mod ID to your mod's `ModConfig.json`:

```json
{
  "ModDependencies": [
    "p3ppc.InputLibrary"
  ]
}
```

Keep any other dependencies already required by your mod:

```json
{
  "ModDependencies": [
    "reloaded.sharedlib.hooks",
    "Reloaded.Memory.SigScan.ReloadedII",
    "p3ppc.InputLibrary"
  ]
}
```

This is important.

The dependency ensures that Input Library is loaded and registers its `IInputHook` controller before your mod tries to retrieve it.

Without the dependency, this can return null if your mod loads first:

```csharp
context.ModLoader.GetController<IInputHook>();
```

### 3. Retrieve `IInputHook`

```csharp
using p3ppc.InputLibrary.Interfaces;
using Reloaded.Mod.Interfaces;
using System;

public class Mod : ModBase
{
    private readonly WeakReference<IInputHook> _inputHookController;

    public Mod(ModContext context)
    {
        _inputHookController =
            context.ModLoader.GetController<IInputHook>();

        if (!_inputHookController.TryGetTarget(out IInputHook? inputHook) ||
            inputHook == null)
        {
            throw new InvalidOperationException(
                "Persona 3 Portable Input Library could not be acquired.");
        }

        inputHook.OnInput += OnInput;
    }

    private void OnInput(
        int input,
        bool risingEdge,
        bool controlType)
    {
        // Handle input here.
    }
}
```

The public event contract is:

```csharp
public delegate void OnInputEvent(
    int input,
    bool risingEdge,
    bool controlType);
```

## Event parameters

### `input`

A bitmask containing the current logical input state for that device.

Because multiple inputs may be active at the same time, bitwise checks are generally preferred.

For example, Circle/Cancel is:

```csharp
private const int InputCircle = 0x2000;
```

You can check it with:

```csharp
if ((input & InputCircle) != 0)
{
    // Circle / Cancel is active.
}
```

An exact comparison should only be used when the action must be the only active input:

```csharp
if (input == InputCircle)
{
    // Circle is the only active logical input.
}
```

The `Input` enum used internally by Input Library is not part of the interface assembly.

Consuming mods can define the logical values they need locally:

```csharp
private const int InputSelect   = 0x0001;
private const int InputStart    = 0x0008;

private const int InputUp       = 0x0010;
private const int InputRight    = 0x0020;
private const int InputDown     = 0x0040;
private const int InputLeft     = 0x0080;

private const int InputLB       = 0x0400;
private const int InputRB       = 0x0800;

private const int InputTriangle = 0x1000;
private const int InputCircle   = 0x2000;
private const int InputCross    = 0x4000;
private const int InputSquare   = 0x8000;
```

### `risingEdge`

Indicates whether the event represents an active nonzero input state or a release to no input:

```text
true  = a nonzero input state was entered or changed
false = the input state returned to zero
```

The event behavior is:

```text
Press:                    input = current nonzero mask, risingEdge = true
Change held combination:  input = new nonzero mask,     risingEdge = true
Release all inputs:       input = 0,                    risingEdge = false
Remain held:              no repeated event
```

A release event therefore contains:

```text
input == 0
```

It does not contain the button that was released.

For one-shot behavior:

```csharp
private void OnInput(
    int input,
    bool risingEdge,
    bool controlType)
{
    if (!risingEdge)
        return;

    if ((input & InputCircle) != 0)
    {
        ActivateAction();
    }
}
```

### `controlType`

Identifies which input device produced the event:

```text
true  = keyboard
false = controller
```

For example:

```csharp
private void OnInput(
    int input,
    bool risingEdge,
    bool controlType)
{
    if (!risingEdge)
        return;

    bool keyboard = controlType;

    if (keyboard &&
        (input & InputCircle) != 0)
    {
        // Keyboard Cancel action.
    }

    if (!keyboard &&
        (input & InputCircle) != 0)
    {
        // Controller Circle / B action.
    }
}
```

## Complete example

This example reacts to several logical Persona 3 Portable actions while supporting combinations and both input devices:

```csharp
using p3ppc.InputLibrary.Interfaces;
using Reloaded.Mod.Interfaces;
using System;

public class Mod : ModBase
{
    private const int InputCircle   = 0x2000;
    private const int InputCross    = 0x4000;
    private const int InputLB       = 0x0400;
    private const int InputRB       = 0x0800;

    private readonly WeakReference<IInputHook> _inputHookController;

    public Mod(ModContext context)
    {
        _inputHookController =
            context.ModLoader.GetController<IInputHook>();

        if (!_inputHookController.TryGetTarget(out IInputHook? inputHook) ||
            inputHook == null)
        {
            throw new InvalidOperationException(
                "Missing dependency: p3ppc.InputLibrary");
        }

        inputHook.OnInput += OnInput;
    }

    private void OnInput(
        int input,
        bool risingEdge,
        bool controlType)
    {
        if (!risingEdge)
            return;

        bool keyboard = controlType;

        if ((input & InputCross) != 0)
        {
            OpenPrimaryCommand();
        }

        if ((input & InputCircle) != 0)
        {
            CloseOrCancel();
        }

        if ((input & InputLB) != 0)
        {
            PreviousSelection();
        }

        if ((input & InputRB) != 0)
        {
            NextSelection();
        }

        if (keyboard)
        {
            // Optional keyboard-specific behavior.
        }
    }

    private void OpenPrimaryCommand() { }
    private void CloseOrCancel() { }
    private void PreviousSelection() { }
    private void NextSelection() { }
}
```

## Tracking whether a button is held

Input Library does not send another event every frame while an unchanged input remains held.

A consuming mod that needs held-state behavior should store the latest input state.

For one device:

```csharp
private int _currentInput;

private void OnInput(
    int input,
    bool risingEdge,
    bool controlType)
{
    _currentInput =
        risingEdge
            ? input
            : 0;
}

private bool IsHeld(int input)
{
    return (_currentInput & input) != 0;
}
```

When keyboard and controller state need to be tracked independently:

```csharp
private int _keyboardInput;
private int _controllerInput;

private void OnInput(
    int input,
    bool risingEdge,
    bool controlType)
{
    int current =
        risingEdge
            ? input
            : 0;

    if (controlType)
        _keyboardInput = current;
    else
        _controllerInput = current;
}
```

The combined state can then be checked with:

```csharp
bool cancelHeld =
    ((_keyboardInput | _controllerInput) & InputCircle) != 0;
```

This is useful for features such as running while a button remains held.

## Keyboard behavior

Keyboard input is normalized against Persona 3 Portable's own action configuration.

Persona 3 Portable stores keyboard bindings separately from its controller-style logical input values. Input Library bridges those configurations by action rather than exposing raw physical keyboard scan codes.

For example:

```text
Keyboard key assigned to Confirm  -> Cross  -> 0x4000
Keyboard key assigned to Cancel   -> Circle -> 0x2000
Keyboard key assigned to Up       -> Up     -> 0x0010
```

With Persona 3 Portable's default bindings, this means a key such as `C` can act as Cancel and be reported as:

```text
Circle
0x2000
```

The important part is that Input Library does not depend on `C` specifically.

If the player changes the keyboard binding for the same game action, the library follows the configured action instead.

This allows consuming mods to work with custom keyboard layouts without knowing which physical key the player selected.

## Controller behavior

Controller input is converted into the same logical action values used by keyboard events.

The public API currently exposes:

* Face-button actions
* Left and right shoulder actions
* Select and Start
* D-pad directions
* Diagonal D-pad input as combined direction flags

Analog sticks, triggers, L3, and R3 are not currently represented by the public logical input bitmask.

## Example: Fast Run

A consuming mod such as Fast Run can listen for the logical Cancel/Circle action without caring whether it came from the keyboard or controller.

```csharp
private const int InputCircle = 0x2000;

private bool _keyboardRunHeld;
private bool _controllerRunHeld;

private void OnInput(
    int input,
    bool risingEdge,
    bool controlType)
{
    bool runHeld =
        risingEdge &&
        (input & InputCircle) != 0;

    if (controlType)
        _keyboardRunHeld = runHeld;
    else
        _controllerRunHeld = runHeld;
}
```

The movement logic can then use:

```csharp
bool runHeld =
    _keyboardRunHeld ||
    _controllerRunHeld;
```

With the correct Input Library dependency, the consuming mod does not need to read Windows keyboard state or poll XInput itself.

## Important implementation notes

* Treat `input` as a bitmask.
* Prefer bitwise checks when combinations are possible.
* Use `risingEdge` for one-shot actions.
* A complete release event has `input == 0`.
* An unchanged held state does not repeatedly emit events.
* `controlType` is `true` for keyboard and `false` for controller.
* Keyboard events represent logical Persona 3 Portable actions, not physical keyboard keys.
* Keyboard bindings are translated through Persona 3 Portable's configured action mappings.
* Do not bundle the main Input Library implementation DLL inside a consuming mod.
* Declare `p3ppc.InputLibrary` as a Reloaded-II dependency.

## Debug logging

Input Library normally remains silent.

To enable diagnostic output, turn on:

```text
Enable Debug Logging
```

in the mod's Reloaded-II configuration.

When enabled, debug logging can include:

```text
Scanning for the P3P keyboard function.
Keyboard signature found at P3P.exe+...
Installing keyboard hook at P3P.exe+...
Successfully installed the P3P keyboard hook.

Scanning for the P3P controller function.
Controller signature found at P3P.exe+...
Installing controller hook at P3P.exe+...
Successfully installed the P3P controller hook.

Keyboard: Circle Pressed
Controller: Circle Pressed
```

All Input Library diagnostic logging uses the debug logging setting and is suppressed when it is disabled.

## Troubleshooting

### `GetController<IInputHook>()` has no target

Check that:

1. `p3ppc.InputLibrary` is installed and enabled.
2. Your mod lists `p3ppc.InputLibrary` in `ModDependencies`.
3. Your project references the matching `p3ppc.InputLibrary.Interfaces.dll`.
4. Your code imports `p3ppc.InputLibrary.Interfaces`.
5. You are not bundling a conflicting copy of the interface assembly.

The dependency entry is especially important because Input Library must register its controller before the consuming mod tries to retrieve it.

### Keyboard movement is detected, but another keyboard action is not

Input Library should report logical Persona 3 Portable actions rather than arbitrary physical keys.

For example, the key assigned to Cancel should produce the logical Circle/Cancel value:

```text
0x2000
```

Enable debug logging and check the Input Library event output.

### Pressing `C` reports Circle instead of C

This is expected.

The API reports the logical Cancel/Circle action, not the physical key name:

```text
C -> Cancel -> Circle -> 0x2000
```

If Cancel is rebound to another keyboard key, that key should produce the same logical action.

### A release callback has `input == 0`

This is expected.

A release-to-zero event is represented as:

```text
input = 0
risingEdge = false
```

### An action triggers while another button is held

Use a bitwise check when combinations are allowed:

```csharp
(input & InputCircle) != 0
```

Use exact equality only when the logical action must be active by itself:

```csharp
input == InputCircle
```

### A held button does not repeat every frame

This is expected.

Events are emitted when the logical input state changes. A consuming mod that needs continuous held behavior should store the current state and use it from its own update or gameplay hook.

## Credits

* seyleonhart, for `p4g64.InputLibrary64`
* rirurin, for the original Input Library
* Persona 3 Portable modding community for reverse engineering and tooling

## License

MIT License. See the repository's license information for details regarding usage and redistribution.
