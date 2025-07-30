#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace minigolf.addons.constants_generator;

[Tool]
public partial class ConstantsGenerator : EditorPlugin {
	private const string KEY_ROOT = "plugin/constants_generator/";
	private const string KEY_PROJECT_NAME = KEY_ROOT + "project_name";
	private const string KEY_NAMESPACE_OVERWRITE = KEY_ROOT + "namespace_overwrite";
	private const string KEY_PATH_TO_SCRIPTS = KEY_ROOT + "path_to_scripts";
	private const string KEY_ACTIONS_NAME = KEY_ROOT + "actions_name";
	private const string KEY_LAYERS_NAME = KEY_ROOT + "layers_name";
	private const string KEY_GROUPS_NAME = KEY_ROOT + "groups_name";
	private const string KEY_GENERATE_ACTIONS = KEY_ROOT + "generate_actions";
	private const string KEY_GENERATE_LAYERS = KEY_ROOT + "generate_layers";
	private const string KEY_GENERATE_GROUPS = KEY_ROOT + "generate_groups";

	private EditorSettings settings;

	private string settingsNamespace => settings.GetSetting(KEY_PROJECT_NAME).AsString();
	private string namespaceOverwrite => settings.GetSetting(KEY_NAMESPACE_OVERWRITE).AsString();
	private string actionsName => settings.GetSetting(KEY_ACTIONS_NAME).AsString();
	private string layersName => settings.GetSetting(KEY_LAYERS_NAME).AsString();
	private string groupsName => settings.GetSetting(KEY_GROUPS_NAME).AsString();
	private bool generateActions => settings.GetSetting(KEY_GENERATE_ACTIONS).AsBool();
	private bool generateLayers => settings.GetSetting(KEY_GENERATE_LAYERS).AsBool();
	private bool generateGroups => settings.GetSetting(KEY_GENERATE_GROUPS).AsBool();
	private string pathToScripts => settings.GetSetting(KEY_PATH_TO_SCRIPTS).AsString();

	public override void _EnterTree() {
		settings = EditorInterface.Singleton.GetEditorSettings();
		InitEditorSettings();
		ProjectSettings.SettingsChanged += OnProjectSettingsChanged;
	}

	private void OnProjectSettingsChanged() {
		string projectRoot = Directory.GetCurrentDirectory();
		string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), pathToScripts);

		string outputRelativePath = Path.GetRelativePath(
			projectRoot,
			outputDirectory
		);

		string projectNamespace = settingsNamespace;

		if (settingsNamespace == "") {
			projectNamespace = FindNamespace(projectRoot);
			if (projectNamespace == null) {
				return;
			}
		}

		string effectiveNamespace = namespaceOverwrite;
		if (namespaceOverwrite == "") {
			effectiveNamespace = $"{projectNamespace}.{outputRelativePath.Replace('/', '.').Replace('\\', '.')}";
		}

		string godotProjectPath = Path.Combine(projectRoot, "project.godot");

		if (!File.Exists(godotProjectPath)) {
			GD.PrintErr("project.godot not found.");
			return;
		}

		string[] lines = File.ReadAllLines(godotProjectPath);

		bool inInput = false;
		bool inLayers = false;
		bool inGroups = false;

		var inputActions = new List<string>();
		var collisionLayers = new Dictionary<string, int>();
		var groups = new List<string>();

		foreach (string line in lines) {
			string trimmed = line.Trim();

			switch (trimmed) {
				case "[input]":
					inInput = true;
					inLayers = false;
					inGroups = false;
					continue;
				case "[layer_names]":
					inInput = false;
					inLayers = true;
					inGroups = false;
					continue;
				case "[global_group]":
					inInput = false;
					inLayers = false;
					inGroups = true;
					continue;
			}

			if (trimmed.StartsWith('[')) {
				inInput = false;
				inLayers = false;
				inGroups = false;
				continue;
			}

			if (inInput && line.Contains('=')) {
				string name = line.Split('=')[0].Trim();
				if (!String.IsNullOrWhiteSpace(name))
					inputActions.Add(name);
			}

			if (inLayers && line.Contains("d_physics/layer_")) {
				string[] parts = line.Split('=');
				string layerStr = parts[0].Split('_')[2].Trim();
				string name = parts[1].Trim().Trim('"');

				if (Int32.TryParse(layerStr, out int layerIndex))
					collisionLayers[name] = layerIndex - 1;
			}

			if (inGroups && line.Contains('=')) {
				string group = line.Split('=')[0].Trim();
				if (!String.IsNullOrWhiteSpace(group))
					groups.Add(group);
			}
		}

		Directory.CreateDirectory(outputDirectory);

		// Generate Actions.cs
		if (generateActions) {
			var actionBuilder = new StringBuilder();
			actionBuilder.AppendLine("using Godot;\n");
			actionBuilder.AppendLine($"namespace {effectiveNamespace};\n");
			actionBuilder.AppendLine($"public static class {actionsName} {{");
			foreach (string name in inputActions) {
				string sanitized = SanitizeName(name);
				actionBuilder.AppendLine($"\tpublic static readonly StringName {sanitized} = \"{name}\";");
			}
			actionBuilder.AppendLine("}");

			string actionPath = Path.Combine(outputDirectory, $"{actionsName}.cs");
			if (UpdateFileIfNew(actionPath, actionBuilder)) {
				GD.Print($"Generated: {actionsName}.cs with {inputActions.Count} input actions");
			}
		}

		// Generate CollisionLayers.cs
		if (generateLayers) {
			var layersBuilder = new StringBuilder();
			layersBuilder.AppendLine($"namespace {effectiveNamespace};\n");
			layersBuilder.AppendLine($"public static class {layersName} {{");
			foreach ((string name, int index) in collisionLayers) {
				string sanitized = SanitizeName(name);
				layersBuilder.AppendLine($"\tpublic const uint {sanitized} = 1 << {index};");
			}
			layersBuilder.AppendLine("}");

			string layersPath = Path.Combine(outputDirectory, $"{layersName}.cs");
			if (UpdateFileIfNew(layersPath, layersBuilder)) {
				GD.Print($"Generated: {layersName}.cs with {collisionLayers.Count} layer names");

			}
		}

		// Generate Groups.cs
		if (generateGroups) {
			var groupsBuilder = new StringBuilder();
			groupsBuilder.AppendLine("using Godot;\n");
			groupsBuilder.AppendLine($"namespace {effectiveNamespace};\n");
			groupsBuilder.AppendLine($"public static class {groupsName} {{");
			foreach (string name in groups) {
				string sanitized = SanitizeName(name);
				groupsBuilder.AppendLine($"\tpublic static readonly StringName {sanitized} = \"{name}\";");
			}
			groupsBuilder.AppendLine("}");

			string groupsPath = Path.Combine(outputDirectory, $"{groupsName}.cs");
			if (UpdateFileIfNew(groupsPath, groupsBuilder)) {
				GD.Print($"Generated: {groupsName}.cs with {groups.Count} group names");
			}
		}
	}

	// Helper: sanitize names for C# identifiers
	private static string SanitizeName(string raw) {
		if (String.IsNullOrEmpty(raw)) return "Unnamed";

		var sb = new StringBuilder();
		bool capitalizeNext = true;
		foreach (char c in raw) {
			if (Char.IsLetterOrDigit(c)) {
				sb.Append(capitalizeNext ? Char.ToUpper(c) : c);
				capitalizeNext = false;
			} else {
				capitalizeNext = true; // skip symbols/spaces/dashes
			}
		}

		string result = sb.ToString();
		if (Char.IsDigit(result[0])) result = "_" + result;
		return result;
	}

	// Helper: find the root namespace from the .csproj file
	private string FindNamespace(string root) {
		string csprojFile = Directory.GetFiles(root, "*.csproj").FirstOrDefault();
		if (csprojFile == null) {
			GD.PrintErr("No project name found. Please specify the project name in the code.");
			return null;
		}

		var doc = XDocument.Load(csprojFile);
		var nsElement = doc.Descendants("RootNamespace").FirstOrDefault();
		return nsElement != null ? nsElement.Value : Path.GetFileNameWithoutExtension(csprojFile);
	}

	private bool UpdateFileIfNew(string filePath, StringBuilder fileBuilder) {
		if (File.Exists(filePath)) {
			if (File.ReadAllText(filePath).Equals(fileBuilder.ToString())) {
				return false;
			}
		}
		File.WriteAllText(filePath, fileBuilder.ToString());
		return true;
	}

	private void InitEditorSettings() {
		AddSetting(KEY_PROJECT_NAME, "");
		AddSetting(KEY_NAMESPACE_OVERWRITE, "");
		AddSetting(KEY_ACTIONS_NAME, "Actions");
		AddSetting(KEY_LAYERS_NAME, "CollisionLayers");
		AddSetting(KEY_GROUPS_NAME, "Groups");
		AddSetting(KEY_GENERATE_ACTIONS, true);
		AddSetting(KEY_GENERATE_LAYERS, true);
		AddSetting(KEY_GENERATE_GROUPS, true);
		AddSetting(KEY_PATH_TO_SCRIPTS, "scripts/generated");
	}

	private void AddSetting(string name, Variant defaultValue) {
		if (!settings.HasSetting(name)) {
			settings.SetSetting(name, defaultValue);
		}
	}

	public override void _ExitTree() {
		ProjectSettings.SettingsChanged -= OnProjectSettingsChanged;
	}
}
#endif
