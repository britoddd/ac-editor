# CC-7: a PreToolUse hook for Bash and PowerShell. It refuses any shell command that names the workspace's car folder (D:\ACModding\cars),
# so Claude's changes to cars go through the acmod MCP tools, which snapshot, validate and log them. Claude's file tools are blocked by the
# Edit deny rule in .claude/settings.json; native Windows has no Claude Code sandbox, so this text check is the shell's only guard.
# ponytail: matches the command text only, so a path built from a variable or reached through cd slips past; a real boundary needs the sandbox (WSL2).
$call = [Console]::In.ReadToEnd() | ConvertFrom-Json
$command = [string]$call.tool_input.command

if ($command -match '(?i)acmodding[\\/]+cars') {
    [Console]::Error.WriteLine('Shell commands may not touch D:\ACModding\cars (CC-7). Read car files with the Read tool, and change cars with the acmod MCP tools (preview_change, apply_change, restore_snapshot), so every change is snapshotted and validated.')
    exit 2
}

exit 0
