@echo off
setlocal
cd /d "%~dp0\..\.."

echo ============================================================
echo  LITTLE CASTLE - CODEX RENDER OPTIMIZATION HANDOFF
echo ============================================================
echo.
echo Branch:
git branch --show-current 2>nul
echo.
echo Working tree:
git status --short 2>nul
echo.
echo Canonical handoff:
echo   docs\handoffs\CODEX_RENDER_OPTIMIZATION.md
echo.
echo Required architecture:
echo   docs\architecture\visibility-render-budget.md
echo   docs\architecture\lod-and-production-assets.md
echo   docs\architecture\render-optimization-hlod.md
echo.
echo IMPORTANT: AGENTS.md is authoritative. Do not create a second
echo camera-distance manager beside VisibilityBudgetManager.
echo.
type docs\handoffs\CODEX_RENDER_OPTIMIZATION.md
endlocal
