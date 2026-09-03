# RawPrintBridge — 虚拟 RAW 打印机转发服务

部署在现场电脑上的后台服务：监听 RAW 打印端口（默认 9100），把服务器发来的打印字节流
透明转发给本机连接的真实打印机（USB）。服务器端只需把打印机配置为「RAW 端口」指向本机 IP 即可。

## 工作原理

```
服务器(打印任务) --网络/TCP 9100--> RawPrintBridge(现场电脑) --winspool RAW--> USB 打印机
```

- 每个 TCP 连接 = 一个打印任务；对端关闭连接或读空闲超时即任务结束。
- 字节流不做任何解析、不渲染，原样写入 Windows 打印池（datatype=RAW）。
- **前提**：服务器端输出的指令语言（PCL / ESC-POS / ZPL / PostScript 等）必须与打印机匹配。

## 版本选择（单项目双目标编译）

| 发布目录 | 适用系统 | 运行时要求 |
|---|---|---|
| `publish\net8.0-win-x64` | Win10/11（64 位） | 免安装运行时（自包含） |
| `publish\net48-win-x86` | Win7 SP1（32/64 位均可） | 需安装 .NET Framework 4.8 Runtime（Win7 SP1 官方支持） |

两个版本功能、配置文件格式、日志完全一致，安装方式相同。

## 配置说明（appsettings.json，全部可改）

| 配置项 | 默认值 | 说明 |
|---|---|---|
| `BindAddress` | `0.0.0.0` | 监听地址（服务端打印需跨机访问时保持 0.0.0.0） |
| `Port` | `9100` | 监听端口 |
| `TargetPrinterName` | （必改） | Windows「打印机和扫描仪」里显示的打印机名称，必须完全一致 |
| `IdleTimeoutSeconds` | `30` | 读空闲超时，超时视为任务结束 |
| `MaxJobSizeMB` | `50` | 单任务最大体积，超出丢弃防内存爆 |
| `DocNamePrefix` | `RawPrintBridge` | 打印队列中的任务名前缀 |
| `ValidatePrinterOnStartup` | `true` | 启动时校验打印机是否存在 |

日志文件：exe 目录下 `logs/rawprintbridge-yyyyMMdd.log`，每天一个。

## 部署到现场电脑（Win7 SP1 示例）

1. 装 .NET Framework 4.8 Runtime（若未装）：https://dotnet.microsoft.com/download/dotnet-framework/net48
2. 把 `publish\net48-win-x86` 目录整个拷贝到现场电脑，例如 `D:\RawPrintBridge\`。
3. **先改 `appsettings.json`**：把 `TargetPrinterName` 改成真实打印机名称。
4. 管理员运行 `deploy\install-service.bat`，服务注册为 `RawPrintBridge` 开机自启，崩溃自动重启。
5. 防火墙放行 9100 端口（管理员命令行）：
   ```
   netsh advfirewall firewall add rule name="RawPrintBridge 9100" dir=in action=allow protocol=TCP localport=9100
   ```

Win10/11 机器同理，改用 `publish\net8.0-win-x64` 目录。

## 服务器端配置

在打印应用/系统里添加打印机，端口类型选 **RAW / TCP 端口**，地址填现场电脑 IP，端口 9100。
打印驱动选择与真实打印机匹配的型号（指令语言要一致）。

**现场电脑上的「虚拟打印机」**：为本机已有的真实打印机创建副本打印机，其端口指向
`127.0.0.1:9100`（标准 TCP/IP RAW 端口）。注意：

- 端口配置里**必须取消勾选「SNMP 状态已启用」**，否则 Windows 探测不到 SNMP 响应会一直显示脱机，
  任务被扣在队列里发不出来；
- 建议同时取消「双向支持」。

## 卸载

管理员运行 `deploy\uninstall-service.bat`。

## 已知限制

- RAW 9100 是「发后即忘」协议，无法向服务器回传打印成功/失败状态。
- 若发送方长时间不关闭连接也不发数据，任务要等 `IdleTimeoutSeconds` 超时后才会出纸。
- 现场电脑必须开机且服务运行中，服务器才能打印。
