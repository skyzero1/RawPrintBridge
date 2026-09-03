# RawPrintBridge — 虚拟 RAW 打印机转发服务

部署在现场电脑上的后台服务：监听 TCP 9100 端口（RAW 打印协议），把服务器发来的打印字节流
透明转发给本机连接的真实打印机（USB）。服务器端只需把打印机配置为「RAW 端口」指向本机 IP 即可。

## 工作原理

```
服务器(打印任务) --网络/TCP 9100--> RawPrintBridge(现场电脑) --winspool RAW--> USB 打印机
```

- 每个 TCP 连接 = 一个打印任务；对端关闭连接或读空闲超时即任务结束。
- 字节流不做任何解析、不渲染，原样写入 Windows 打印池（datatype=RAW）。
- **前提**：服务器端输出的指令语言（PCL / ESC-POS / ZPL / PostScript 等）必须与打印机匹配。

## 配置说明（appsettings.json）

| 配置项 | 默认值 | 说明 |
|---|---|---|
| `BindAddress` | `0.0.0.0` | 监听地址 |
| `Port` | `9100` | 监听端口 |
| `TargetPrinterName` | （必改） | Windows「打印机和扫描仪」里显示的打印机名称，必须完全一致 |
| `IdleTimeoutSeconds` | `30` | 读空闲超时，超时视为任务结束 |
| `MaxJobSizeMB` | `50` | 单任务最大体积，超出丢弃防内存爆 |
| `DocNamePrefix` | `RawPrintBridge` | 打印队列中的任务名前缀 |
| `ValidatePrinterOnStartup` | `true` | 启动时校验打印机是否存在 |

日志文件：exe 目录下 `logs/rawprintbridge-yyyyMMdd.log`，每天一个。

## 发布与部署

### 1. 发布

需要现场电脑装有 .NET 8 Runtime 时（体积小）：

```
dotnet publish -c Release -r win-x64 --self-contained false
```

现场电脑**没有** .NET 8 Runtime 时（自包含，免安装运行时）：

```
dotnet publish -c Release -r win-x64 --self-contained true
```

产物在 `bin\Release\net8.0\win-x64\publish\`。

### 2. 部署到现场电脑

1. 把 `publish` 目录整个拷贝到现场电脑，例如 `D:\RawPrintBridge\`。
2. **先改 `appsettings.json`**：把 `TargetPrinterName` 改成真实打印机名称。
3. 双击 `install-service.bat`（需管理员权限），服务将注册为
   `RawPrintBridge` 并设为开机自启，进程崩溃时自动重启。
4. 防火墙放行 9100 端口（管理员 PowerShell）：
   ```
   netsh advfirewall firewall add rule name="RawPrintBridge 9100" dir=in action=allow protocol=TCP localport=9100
   ```

### 3. 服务器端配置

在打印应用/系统里添加打印机，端口类型选 **RAW / TCP 端口**，地址填现场电脑 IP，端口 9100。
打印驱动选择与真实打印机匹配的型号（指令语言要一致）。

### 4. 卸载

管理员运行 `uninstall-service.bat`。

## 注意

- 现场电脑必须开机且服务运行中，服务器才能打印。
- 配置端口中->取消勾选「SNMP 状态已启用」→ 确定
