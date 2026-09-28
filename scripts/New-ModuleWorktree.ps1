param(
    [Parameter(Mandatory)]
    [ValidateSet('wholesale','security','files','tasks','logs','menus','email-templates')]
    [string]$Module,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9][a-z0-9-]*$')]
    [string]$Task
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$parent = Split-Path $repositoryRoot -Parent
$branch = "feature/$Module-$Task"
$worktree = Join-Path $parent "eggrack_operations-wt-$Module-$Task"

if (Test-Path -LiteralPath $worktree) {
    throw "工作树目录已存在: $worktree"
}

$changes = git -C $repositoryRoot status --porcelain
if ($changes) {
    throw '主工作目录存在未提交或未跟踪的改动，请先提交或处理'
}

git -C $repositoryRoot show-ref --verify --quiet "refs/heads/$branch"
if ($LASTEXITCODE -eq 0) {
    git -C $repositoryRoot worktree add $worktree $branch
} else {
    git -C $repositoryRoot worktree add -b $branch $worktree main
}

Write-Host "工作树: $worktree"
Write-Host "分支:   $branch"
Write-Host "请在新窗口打开: $(Join-Path $worktree 'eggrack_operations.sln')"

