# Changelog

## 1.1.3 — WireGuard on IPv4-only Windows

- Publish empty profile defaults and a WireGuard import template containing placeholders for every client/server address and key.
- Reject unfinished templates with a clear message in all six languages.
- Replace network examples with documentation ranges and publish screenshots with empty fields.
- Audit release sources and archives for personal configuration files, literal private keys and non-example network addresses.
- Keep the work WireGuard transport on the ordinary OS route used by preflight, without default-interface socket binding.
- Exclude the resolved IPv4 or IPv6 VPN server address from captured work routes to prevent a transport routing loop.
- Allow a fresh tunnel 25 seconds to establish verified TCP access; report the destination and timeout instead of an unexplained cancellation.
- Preserve engine diagnostics before failed-tunnel cleanup and show the post-adapter verification stage explicitly.
- Allow manual cancellation while the initial connection is being checked.

## 1.1.2 — IPv4 compatibility

- Retry a failed IPv6 TUN address configuration once using an IPv4-only adapter and DNS.
- Preserve explicit IPv6 work routes by rejecting an IPv4-only retry when those routes are required.
- Keep cancellation, failed-session cleanup and proxy/work route rules intact.
- Display the actual executable version in the window footer.

## 1.1.1 — Startup diagnostics

- Capture early network engine errors and Windows exit codes that previously disappeared.
- Preserve the hidden console, graceful shutdown and owned-process cleanup while restricting inherited handles.
- Log preliminary access checks and Windows VPN adapter startup separately.
- Distinguish the preliminary engine check from VPN startup in all six languages.
- Show proxy access in Proxy mode and reset stale diagnostic results after mode changes.

## 1.1.0 — ProxyChoicePro

- Added WireGuard work network profiles with split routes and protected client credentials.
- Added saved work PCs, native Windows RDP launch and TCP diagnostics.
- Added automatic recovery after failure/resume and cancellation through the window or tray.
- Added installed browser, ChatGPT, grouped Codex and codex-computer-use-swift selections.
- Added validated route/probe metadata in prepared WireGuard client files.
- Detect overlapping home/work networks.
- Added responsive tabs, night mode, visible window border and consistent whole-icon status colors.
- Added six-language documentation and installer payloads; migrate existing proxy profiles.

## 1.0.0

- Selected application routing; other applications keep the ISP connection.
- Mandatory ProxyChoiceApp proxy access and default Microsoft Store rules.
- Chrome/Edge selection and installed YouTube discovery with shared browser routing for web apps.
- Multiple encrypted proxy profiles, disconnect and protected process lifecycle.
- Compact readable interface, taskbar/tray colors, speed and session traffic counters.
- Clear/export journal and nine help topics in six interface languages.
- Russian, English, German and Spanish documentation.
- Standalone EXE, self-extracting installer, complete source package and Windows release workflow.
