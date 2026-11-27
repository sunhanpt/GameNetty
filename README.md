# GameNetty

[![UnityVersion](https://img.shields.io/badge/Unity%20Ver-2019.4.12++-blue.svg?style=flat-square)](https://github.com/ALEXTANGXIAO/GameNetty)
[![License](https://img.shields.io/github/license/ALEXTANGXIAO/GameNetty)](https://github.com/ALEXTANGXIAO/GameNetty)
[![License](https://img.shields.io/github/last-commit/ALEXTANGXIAO/GameNetty)](https://github.com/ALEXTANGXIAO/GameNetty)
[![License](https://img.shields.io/github/issues/ALEXTANGXIAO/GameNetty)](https://github.com/ALEXTANGXIAO/GameNetty)


众所周知，目前游戏开发中最好的C#服务器基本上就是ET框架的服务器。但无奈他是一个整体的解决方案、前后端共享的方式以及工程共享、前后端耦合的模式不是所有人都能接受。还有客户端工程为了共享以及ClientServer模式加入了很多不必要的代码和库（monogo和NLog等）很多人、甚至中型团队想用ET做网络需求但是最方便接入只能往里面加自己的代码。

<strong>ClientServer这种模式虽然很适合独立开发、但是如果团队规模上去后，存在专门的前后端分工，大量的共享双端的意义已经不大了，以及安全起见更不会让源码权限都掌握每个人的手中。就绝大多数商业级项目而言，前后端分离是必然。（既然ET的服务器作为商业级解决方案已经很完美了，那不如就按照中大型项目的架构来分离前后端！一次到位）</strong>

于是再次首次拆分最新的ET8.1的前后端！客户端最精简大约750k，完美做成包的形式，几乎零成本 无侵入的嵌入进你的框架。真正做到人人都能往自己框架加ET。

<strong>改动：转表工具和启动服务器配置完全使用最新luban。现已完美兼容支持。</strong>

---
服务器运行指南：

1.打开Share解决方案编译全部解决方案。

2.打开Server服务器解决方案直接编译。（一定要执行第一步，因为Server依赖分析器和源代码生成器）。

3.完成、直接运行Server。

本地联调使用 `Start Server(Single Process).bat` 和 Unity 的 `Assets/Scenes/main.unity`。客户端的本机 Router 地址设置见 [登录流程](Docs/登录流程.md)。

当前重写以 `D:\REA\CapybaraGo-Unity` 为源项目，采用 GameNetty 的客户端和服务端框架。[重写目标](Docs/迁移计划.md)

「风灯旅记」的本地登录页按冒险账号载入存档，适用于本机联调。客户端默认隐藏 WebSocket SDK 的演示面板，Unity 编辑器和开发包运行时可按 `F8` 打开。

冒险联调前先启动 MongoDB。Gate 通过当前区的 `StartZoneConfig` 连接数据库，将玩家 ID、冒险进度和结算保存到 `AdventureProgress`。用同一账号重新登录可恢复已保存的阶段和战报。

营地可预览并选择「风铃栈道」和「潮汐渡口」。章节、地图与事件定义位于 `Config/Excel/GameConfig`。新增同规则章节时，将章节的 `MapId`、`EventId` 指向对应表项，再把 `Far.png`、`Middle.png`、`Terrain.png` 和 `Opponent.png` 放到 `Unity/Assets/Resources/Adventure/<ResourceKey>`，用 Luban 导出服务端数据。

营地的「背包与装备」可用金币购买、穿戴、卸下和强化装备。武器和护具各可穿戴一件，属性在开启冒险时计入当局。装备价格、加成和强化上限由 `AdventureEquipmentConfigCategory` 对应的源表配置。

营地的「天赋」和「旅伴」使用金币成长。随行旅伴每两回合助战一次。完成一次普通冒险结算后解锁「渡口巡潮试炼」。挑战每天 UTC 00:00 刷新，进入时消耗次数，放弃也计入当天次数。

出发后角色会走到路边事件并等待玩家选择，随后冲刺接敌。战斗结果由服务端计算，客户端依次播放战报，也可直接查看完整战报。战报结算后回到营地。留在营地跨过挑战刷新时间时，可通过「刷新进度」获取新的剩余次数。

从项目根目录运行 `./Publish-linux-x64.ps1` 会生成一份独立的服务端发布目录，包含运行程序集、配置和 SHA-256 清单。在安装了 ASP.NET Core Runtime 8 的主机上，从发布目录的 `Bin` 执行 `dotnet App.dll --AppType=Server --Process=1 --StartConfig=StartConfig/Localhost`。客户端和服务器须使用同一批生成协议。切回旧版本之前，按该版本的存档备份恢复数据库。

本机使用 `StartConfig/Localhost` 时，HTTP 和 WebSocket 通过 Kestrel 监听 `127.0.0.1`，可在普通窗口启动。Windows URL ACL 脚本用于之前的 HTTP.sys 版本。《风灯旅记》的配置、资源、玩法和发布验收见 [框架验证记录](Docs/迁移计划.md#风灯旅记框架验证记录)。

Windows 转表入口位于 `Tools/Luban`。将完整的 Luban 发布目录放在 `Tools/Luban/LubanRelease`，再运行 `GenConfig_Client.bat` 或 `GenConfig_Server.bat`。现有 Excel 表已用 Luban `4.5.0+998c69c83bb8a8df8d4d7ee8b8856adef9281772` 核验双端 `.bytes`。更换生成器版本时先检查 C# 产物差异。

---

![image](Books/src/info.png)


## <strong>特别鸣谢
#### <a href="https://github.com/egametang/ET"><strong>ET</strong></a> - ET是一个开源的游戏客户端（基于unity3d）服务端双端框架，服务端是使用C# .net core开发的分布式游戏服务端，其特点是开发效率高，性能强，双端共享逻辑代码，客户端服务端热更机制完善。


## <strong>Buy me a 奶茶.

[如果觉得本仓库对您有帮助，不妨请我可爱的女儿买杯奶茶吧~](Books/Donate.md)
