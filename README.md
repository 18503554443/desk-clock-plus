# DeskClockPlus

一个 Windows 桌面时钟小程序，集成了时间、系统状态、发薪倒计时、法定节假日、自定义公历/农历倒计时和托盘图标。

## 功能

- 桌面时钟：时、分、秒
- 发薪倒计时和月薪设置
- 按实际工作日计算日薪
- 今日已赚按上下班时间进度计算
- 显示 GPU 使用率和 GPU 温度
- 显示 CPU 温度
- 法定节假日自动更新并按年度缓存
- 法定节假日不计今日收入
- 支持多个自定义倒计时
- 自定义倒计时支持公历和农历
- 自动显示最近即将到来的倒计时
- 支持托盘图标、显示/隐藏和右键菜单
- 跟随 Windows 虚拟桌面切换

## 安装包

直接运行：

```text
dist/DeskClockPlus.exe
```

程序为单文件版本。首次运行后，配置和缓存会保存到：

```text
%APPDATA%/DeskClockPlus/
```

## 系统要求

- Windows 10 或 Windows 11
- .NET Framework 4.x
- CPU 温度读取需要管理员权限；程序启动时 Windows 会请求一次 UAC 确认

## 构建

在 PowerShell 中运行：

```powershell
./build.ps1
```

生成单文件版本：

```powershell
./package.ps1
```

图标由 `make-icon.ps1` 生成，也可以替换 `app.ico` 后重新打包。

## 节假日数据

程序优先使用内置的 2026 年法定节假日数据，并会在启动时尝试从公开年度数据源更新当前年份和下一年份。

更新原理：

- 当前年份和下一年的数据会缓存到 `%APPDATA%/DeskClockPlus/`
- 缓存超过 14 天后自动重新获取
- 网络失败时继续使用缓存或内置数据

## 隐私

仓库不包含个人配置文件、薪资、日志或运行缓存。公开的默认薪资为 `0`，用户需要在程序中自行设置。

## License

MIT

See `THIRD_PARTY_NOTICES.md` for bundled sensor library licenses.
