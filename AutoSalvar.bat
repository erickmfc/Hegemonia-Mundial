@echo off
title Hegemonia Global - Smart Save v4
echo ===================================================
echo   SMART SAVE v4 - HEGEMONIA GLOBAL
echo ===================================================
echo.

cd /d "E:\Hegemonia_save"
echo Pasta: %CD%
echo.

:: -----------------------------------------------
:: ETAPA 1: Matar tudo e limpar locks
:: -----------------------------------------------
echo [1/4] Encerrando processos Git...
taskkill /f /im git.exe           >nul 2>&1
taskkill /f /im git-remote-https.exe >nul 2>&1
taskkill /f /im git-repack.exe    >nul 2>&1
taskkill /f /im git-pack-objects.exe >nul 2>&1
timeout /t 3 /nobreak             >nul
call :UNLOCK
echo     Feito!
echo.

:: -----------------------------------------------
:: ETAPA 2: Detectar e bloquear arquivos grandes
:: -----------------------------------------------
echo [2/4] Verificando arquivos maiores que 90MB...
set FOUND_BIG=0
for /r %%F in (*) do (
    if %%~zF GTR 94371840 (
        echo     AVISO: %%F esta grande demais e sera ignorado
        echo %%~pnxF >> .gitignore_temp
        set FOUND_BIG=1
    )
)
if %FOUND_BIG%==1 (
    echo     Arquivos grandes detectados. Adicionando ao .gitignore...
    type .gitignore_temp >> .gitignore
    del /f /q .gitignore_temp >nul 2>&1
) else (
    echo     Tudo dentro do limite. OK!
)
echo.

:: -----------------------------------------------
:: ETAPA 3: Adicionar tudo e commitar
:: -----------------------------------------------
echo [3/4] Adicionando arquivos modificados...
call :UNLOCK
git add .
call :UNLOCK

git diff --cached --quiet
if %errorlevel% == 0 (
    echo.
    echo     Nenhuma mudanca detectada.
    echo     O projeto ja esta igual ao que esta no GitHub!
    echo.
    goto :PUSH
)

echo Criando commit...
call :UNLOCK
git commit -m "Auto-Save: %DATE% %TIME%"
call :UNLOCK
echo     Commit criado!
echo.

:: -----------------------------------------------
:: ETAPA 4: Enviar para o GitHub
:: -----------------------------------------------
:PUSH
echo [4/4] Enviando para o GitHub...
echo.
git push -u https://github.com/erickmfc/Hegemonia-Mundial.git main --force
echo.
echo ===================================================
echo   CONCLUIDO! VERIFIQUE AS MENSAGENS ACIMA.
echo ===================================================
echo.
pause
goto :EOF

:: -----------------------------------------------
:: Sub-rotina: limpa TODOS os locks conhecidos
:: -----------------------------------------------
:UNLOCK
if exist ".git\index.lock"           del /f /q ".git\index.lock"           >nul 2>&1
if exist ".git\HEAD.lock"            del /f /q ".git\HEAD.lock"            >nul 2>&1
if exist ".git\COMMIT_EDITMSG.lock"  del /f /q ".git\COMMIT_EDITMSG.lock"  >nul 2>&1
if exist ".git\MERGE_HEAD.lock"      del /f /q ".git\MERGE_HEAD.lock"      >nul 2>&1
if exist ".git\refs\heads\main.lock" del /f /q ".git\refs\heads\main.lock" >nul 2>&1
exit /b