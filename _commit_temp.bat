@echo off
cd /d e:\Hegemonia_save
taskkill /F /IM git.exe /T 2>nul
timeout /t 1 /nobreak >nul
del /f .git\index.lock 2>nul
set GIT_CONFIG_NOSYSTEM=1
set GIT_CONFIG_NOSYSTEM=1
set GIT_LFS_SKIP_SMUDGE=1
set GIT_LFS_SKIP_PUSH=1
git -c filter.lfs.required=false -c filter.lfs.process= commit --no-verify -m "feat: novos sistemas governo/tripulacao/auditoria + 27 scripts atualizados"
echo EXIT_CODE=%ERRORLEVEL%
