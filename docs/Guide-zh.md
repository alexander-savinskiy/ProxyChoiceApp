# ProxyChoicePro 1.1.3

[en](Guide-en.md) · [ru](Guide-ru.md) · [fr](Guide-fr.md) · [de](Guide-de.md) · [es](Guide-es.md) · [zh](Guide-zh.md)

两种连接模式：让所选应用通过 SOCKS5/HTTP/HTTPS 代理，或通过 WireGuard 访问工作网络，再使用 Windows 远程桌面。包括自适应标签页、夜间模式、诊断、收藏的电脑和自动恢复连接。

快速开始：打开 ProxyChoicePro.exe 或安装 ProxyChoicePro-Setup.exe。选择工作网络，导入为这台笔记本签发的 WireGuard 客户端 .conf，然后连接。在电脑标签页添加工作电脑 IP，再打开 RDP。若家庭与工作子网重叠，请使用各电脑的 /32 路由和可访问的 TCP 测试服务。路由器必须登记客户端公钥并允许访问局域网；服务器配置不能直接当作客户端配置。参阅 Keenetic 设置指南。

[Keenetic](Keenetic-zh.md)

## 1. 哪些流量使用代理

连接后，勾选的应用使用所选代理，其他应用使用运营商网络。ProxyChoicePro 自身的互联网连接和 IP 检查始终通过代理。私有网络地址保持直连，包括地址为 LOCAL_ROUTER_IP 的调制解调器。断开后，所有应用恢复普通连接。本程序通过 TUN 接口控制应用路由，并非带永久断网保护的 VPN：程序或引擎退出后，Windows 会恢复运营商网络。

## 2. 连接与检查 IP

在代理模式下，按服务商提供的信息选择 SOCKS5、HTTP 或 HTTPS。输入不带 URL 前缀的服务器、端口和必要的登录凭据。“测试代理”不会改变 Windows 路由。“连接”会请求管理员权限以创建 TUN 接口，并验证所选连接。“检查 IP”显示测试连接的外部地址，不会逐一验证所有应用。任务栏和托盘中的整个图标在断开时为灰色，连接过程中为黄色，连接验证成功后为绿色，访问失败时为红色。要访问工作网络，请使用 WireGuard，参阅第 10 节。

## 3. 选择 Chrome、Edge 和 YouTube

勾选应用后点击应用。已连接时将重新启动隧道。请重启受影响的应用，以关闭更改前建立的连接。运行中的应用支持按 Ctrl 多选；选择 .exe 也可添加未运行的应用。浏览器各标签页共用网络进程：选择 Chrome 或 Edge 后，该浏览器的所有网站均使用代理。首次启动时，如果检测到已安装的 YouTube，会自动添加并勾选。YouTube 网页应用使用浏览器的可执行文件，它与对应浏览器共享勾选状态，不建立单独的 YouTube 网站规则。

## 4. Microsoft Store

Microsoft Store 默认勾选，也可关闭。规则覆盖 WinStore.App.exe / WinStore.exe，以及 Windows 辅助服务使用的已知 Microsoft 目录、许可证、登录和下载地址。启用后，共用的 Microsoft 登录和 Windows Update 地址也可能使用代理。并不会将全部 svchost.exe 进程导向代理。Store 中的第三方 Win32 应用可能从不在 Microsoft 列表中的发布商服务器下载，可能需要添加实际执行下载的应用。应用更改后请重启 Store。

## 5. 多个代理配置与密码

可添加、重命名并保存多个配置。更换代理前请先断开。应用选择、Store、UDP 和语言设置在各配置之间共用。记住密码使用 Windows DPAPI 为当前 Windows 用户加密保存，其他用户或电脑无法解密该文件。取消勾选可避免保存该配置的密码。首次启动可导入 ProxyFlow 的代理服务器，但不会将其原有例外规则转成所选应用。两个程序的数据目录相互独立。

## 6. 网络、DNS 和 UDP

支持 IPv4 和 IPv6。启动隧道前先解析代理服务器。隧道内 DNS 使用虚拟 IP 映射，使规则能够跟随应用，即使名称由 Windows 服务解析。通用 DNS 解析通过代理使用 Cloudflare DoH。直连使用当前网卡的 DNS，无可用服务器时使用直连 DoH。因此，未勾选应用的服务 DNS 请求也可能通过代理。局域网 IP 保持直连。所选应用默认禁用 UDP，使浏览器 QUIC 可回退为 TCP。仅在 SOCKS5 服务器支持 UDP 时启用。HTTP/HTTPS 无法承载 UDP。不支持的 UDP 和 ICMP 仅在所选路由上被拒绝，其他应用保留直连。

## 7. 断开、退出与故障

断开将停止本程序拥有的网络引擎并恢复路由。退出也执行同样操作；最小化保持连接。Windows Job Object 会在主程序崩溃时终止其子进程。启动、检查和关闭均设有超时，并在必要时强制终止。如果仅代理服务器不可访问，所选应用保留代理路由，其他应用继续使用运营商网络。如果网络引擎本身退出，隧道停止，所有应用恢复运营商网络。测试时不要同时连接 ProxyFlow、其他 TUN VPN 和 ProxyChoicePro。

## 8. 速度、流量和日志

状态卡显示接收、发送和最近一次测得的速度。统计属于当前隧道会话，包含引擎处理的代理与直连流量，并非代理提供商的独立计费统计。此处 1 MB 按 1,048,576 字节计算。应用规则将创建新会话并重置计数。日志包含应用事件和最近的引擎记录。清空会从显示和导出中移除当前记录，之后仍会记录新事件。引擎文件保持打开，断开后删除。导出会隐藏认证信息，但日志仍可能包含网站和应用路径。

## 9. 故障排查与文件

连接失败时请检查协议、服务器、端口、用户名和密码，再测试代理。如果应用仍显示旧 IP，请核对其准确 .exe 路径，应用选择后重启。启动器和子程序可能需要单独添加。无法检查的进程可通过选择 .exe 添加。本程序适用于 Windows 10/11 x64，内含运行时和引擎，无须另装 .NET。安装程序创建快捷方式和卸载程序；独立 EXE 可免安装运行。数据目录为 %LOCALAPPDATA%\ProxyChoicePro。作者：Alexander Savinskiy，a.savinskiy@gmail.com。源代码采用 GPL-3.0-or-later，组件许可证见下方。

## 10. 办公网络

导入为此电脑创建的 WireGuard 客户端 .conf 文件。配置包含认证密钥、VPN 地址、服务器及允许的网络。保存一次后即可点击“连接”。办公路由适用于所有应用，包括 Windows 远程桌面。其他互联网流量仍使用普通连接。如果 AllowedIPs=0.0.0.0/0，请在配置选项中指定具体办公子网及可访问的 TCP 检查服务。不支持脚本或多个对等端。不会安装文件中的 DNS，请使用办公电脑 IP。

## 11. 办公电脑与 RDP

添加办公电脑名称、IPv4/IPv6 地址及 TCP 端口。默认 RDP 端口为 3389。用户名可选；密码由 Windows 远程桌面客户端请求，绝不写入 .rdp 文件。连接办公配置、选择电脑，然后点击“打开远程桌面”。目标 IP 必须属于办公路由。目标电脑必须开机、支持入站 RDP，且防火墙允许 VPN 客户端访问。RDP 检查仅确认 TCP 端口可达，不验证密码或 Windows 登录。

## 12. 诊断、恢复与夜间模式

诊断分别检查引擎、加密办公隧道访问、所选 RDP 服务以及重叠子网。引擎运行本身并不代表办公网络可用。两次健康检查失败或休眠唤醒后，自动恢复会间隔 5、10、20、40 秒，之后每 60 秒重试。点击“断开”或“停止重新连接”会取消重试。绿色表示访问已确认，灰色为断开，黄色为连接中，红色为访问错误。夜间模式与语言会保存。窄窗口中选项卡自动换行，连接表单纵向排列。

## 13. 应用列表和预先配置的文件

“应用”选项卡列出 Chrome、Edge、已安装的浏览器、ChatGPT、Codex 和 codex-computer-use-swift。未安装的应用显示为不可用。Codex 条目包含检测到的桌面程序、命令行和代码模式可执行文件。共享可执行文件的条目会同步勾选。Microsoft Store 有单独的复选框。勾选仅适用于代理模式；工作网络路由适用于所有应用。

预先配置的客户端文件可以包含以下注释。Routes 指定工作网络目标，ProbeHost 和 ProbePort 指定可访问的 TCP 服务。导入时会验证它们是否符合 AllowedIPs。标准 WireGuard 客户端会忽略这些注释。

```ini
# ProxyChoicePro.Routes = REPLACE_WITH_WORK_ROUTES_CIDR
# ProxyChoicePro.ProbeHost = REPLACE_WITH_WORK_TCP_SERVICE_IP
# ProxyChoicePro.ProbePort = REPLACE_WITH_WORK_TCP_SERVICE_PORT
```

© 2026 Alexander Savinskiy · a.savinskiy@gmail.com · GPL-3.0-or-later
