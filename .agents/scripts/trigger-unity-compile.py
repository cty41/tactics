import json
import sys
import urllib.request
from pathlib import Path

HEADERS = {
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}


def find_project_root(start_dir: str) -> Path | None:
    """从 start_dir 向上查找包含 .agents/mcp.json 的目录。"""
    current = Path(start_dir).resolve()
    for _ in range(64):  # 防止无限向上遍历
        if (current / ".agents" / "mcp.json").is_file():
            return current
        parent = current.parent
        if parent == current:
            break
        current = parent
    return None


def load_mcp_url(project_root: Path) -> str:
    """从项目级 .agents/mcp.json 读取 Unity MCP 的唯一 URL。"""
    config_path = project_root / ".agents" / "mcp.json"
    with config_path.open(encoding="utf-8") as config_file:
        config = json.load(config_file)

    url = config.get("mcpServers", {}).get("unityMCP", {}).get("url")
    if not isinstance(url, str) or not url.startswith("http://127.0.0.1:") or not url.endswith("/mcp"):
        raise ValueError(f"invalid unityMCP URL in {config_path}: {url!r}")
    return url


def call_mcp(url: str, data: dict, timeout: float = 5.0):
    req = urllib.request.Request(
        url,
        data=json.dumps(data).encode(),
        headers=HEADERS,
    )
    return urllib.request.urlopen(req, timeout=timeout)


def main():
    # 读取 stdin 中的 hook payload，提取 cwd
    try:
        payload = json.load(sys.stdin)
        cwd = payload.get("cwd", "")
    except Exception:
        cwd = ""

    if not cwd:
        # fallback：使用当前工作目录
        cwd = str(Path.cwd())

    project_root = find_project_root(cwd)
    if project_root is None:
        # 不在 Unity 项目目录下，静默跳过
        return

    try:
        url = load_mcp_url(project_root)
    except Exception as e:
        print(f"[hook] Unity MCP configuration error ({e})", file=sys.stderr)
        return

    try:
        # 1. Initialize session
        resp = call_mcp(
            url,
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {},
                    "clientInfo": {"name": "kimi-hook", "version": "1.0"},
                },
            },
            timeout=5.0,
        )
    except Exception as e:
        print(f"[hook] Unity MCP not available ({e})", file=sys.stderr)
        return

    session_id = resp.headers.get("mcp-session-id", "")
    HEADERS["mcp-session-id"] = session_id

    # 2. Send initialized notification
    call_mcp(url, {"jsonrpc": "2.0", "method": "notifications/initialized"}, timeout=2.0)

    # 3. Call refresh_unity
    resp2 = call_mcp(
        url,
        {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/call",
            "params": {
                "name": "refresh_unity",
                "arguments": {"compile": "request", "wait_for_ready": False},
            },
        },
        timeout=30.0,
    )

    body = resp2.read().decode()
    for line in body.splitlines():
        if line.startswith("data: "):
            payload = json.loads(line[6:])
            result = payload.get("result", {})
            content = result.get("content", [{}])
            text = json.loads(content[0].get("text", "{}"))
            msg = text.get("message", "done")
            state = text.get("data", {}).get("resulting_state", "?")
            print(f"[hook] Unity: {msg} (state: {state})")
            break


if __name__ == "__main__":
    main()
