# Constants Generator Plugin for Godot

The Godot plugin automatically extracts input actions, collision layers, and group names from `project.godot` and
generates strongly-typed C# constants.

## Features

- Parses `project.godot` to extract:
  - Input actions
  - Collision layers
  - Group names
- Generates AudioBus constants with name and id
- Automatically generates files on project settings change
- Generates static `StringName` constants or `uint` for collision layers
- Automatically infers root namespace from `.csproj`
- Customizable class names and location
- Optional disabling of specific categories (actions, groups, layers)
- Only updates changed files to avoid unnecessary recompilation

## Installation

1. Install plugin in `addons` folder
2. Enable the plugin:

- `Project` > `Project Settings` > `Plugins`
- Build the C# project if necessary
- Enable `Constants-Generator` in `Project` > `Project Settings` > `Plugins`

## Requirements

- Godot Mono 4.x
- C#/.NET project enabled


## Editor Settings

You can configure the plugin under:

`Editor` > `Editor Settings` > `Constants-Generator`

> Enable `Advanced Settings` in the top right and scroll down to the bottom to find the plugin settings.

| Key                   | Description                                                                |
|-----------------------|----------------------------------------------------------------------------|
| `project_name`        | Custom project name override (optional)                                    |
| `namespace_overwrite` | Namespace to use in generated files (optional)                             |
| `path_to_scripts`     | Output directory path for generated `.cs` files (relative to project root) |
| `actions_name`        | Class name for generated input actions                                     |
| `layers_name`         | Class name for generated collision layers                                  |
| `groups_name`         | Class name for generated node groups                                       |
| `audio_bus_name`      | Class name for generated audio bus constants                               |
| `generate_actions`    | Whether to generate the actions file                                       |
| `generate_layers`     | Whether to generate the layers file                                        |
| `generate_groups`     | Whether to generate the groups file                                        |
| `generate_audio_bus`  | Whether to generate the audio bus file                                     |

> Files are only written when content has changed to avoid unnecessary reimports and build noise.

## Example

Each file will contain a static class like the following:

```csharp
using Godot;

namespace MyGame.Constants;

public static class Actions {
    public static readonly StringName Jump = "Jump";
    public static readonly StringName Shoot = "Shoot";
}
```
And you can use these constants in your scripts like this:
```csharp
public override void _Input(InputEvent @event) {
    if (@event.IsActionPressed(Actions.Attack)) {
        // attack
    }
    if (@event.IsActionPressed(Actions.Jump)) {
        // jump
    }
}
```
> A more detailed example can be found in [ExampleUsage.cs](ExampleUsage.cs)

## License

MIT License