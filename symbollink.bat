@echo off
REM 创建从 .clinerules 到 .cursor 的符号链接
REM 此脚本应在项目根目录下执行
mklink /d .clinerules .cursor