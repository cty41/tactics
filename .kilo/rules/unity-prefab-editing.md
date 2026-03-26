# Unity Prefab Editing via MCP

When asked to modify a Unity Prefab (.prefab) or Asset (.asset):

- **NEVER** directly edit the YAML text content of a .prefab or .asset file.
- **ALWAYS** use the available unity-editor-mcp tools, specifically `modify_asset` or equivalent specialized skills.
- If the tool is missing, ask the user to configure the unity-editor-mcp server.
- Explain changes using natural language, and let the tool handle component-level property updates (e.g., changing component values, sprite references) while keeping GUIDs intact.