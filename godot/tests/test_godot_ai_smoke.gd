@tool
extends McpTestSuite

## Minimal in-editor smoke suite for the pinned godot-ai integration.
## It intentionally tests only read-only editor/project state; gameplay rules
## remain covered by NUnit, GdUnit4Net, and the Core golden vectors.

func suite_name() -> String:
	return "godot_ai_smoke"


func test_editor_is_ready_with_main_scene_open() -> void:
	assert_true(Engine.is_editor_hint(), "test_run must execute in the editor")
	var root := EditorInterface.get_edited_scene_root()
	assert_true(root != null, "Main.tscn should be open")
	if root == null:
		return
	assert_eq(root.name, "TacticsMigrationRoot")
	assert_eq(root.scene_file_path, "res://scenes/Main.tscn")


func test_godot_ai_plugin_script_is_readable() -> void:
	var plugin_path := "res://addons/godot_ai/plugin.gd"
	assert_true(FileAccess.file_exists(plugin_path), "godot-ai plugin.gd must exist")
	if not FileAccess.file_exists(plugin_path):
		return
	var source := FileAccess.get_file_as_string(plugin_path)
	assert_contains(source, "extends EditorPlugin")
	assert_contains(source, "_start_server()")


func test_tactics_resource_catalog_is_loadable() -> void:
	var catalog_path := "res://content/poison_spear/ContentCatalog.tres"
	var catalog := load(catalog_path)
	assert_true(catalog != null, "Poison Spear ContentCatalog must load")
