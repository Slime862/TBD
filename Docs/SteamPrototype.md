# TBD Steam 双人原型

## 本地运行

1. 使用 Unity `2022.3.61f2` 打开项目。
2. 执行菜单 `TBD/Prototype/Rebuild Steam Prototype` 可重新生成场景 UI 与 `TbdRoomPlayer.prefab`。
3. 单人模式不需要 Steam；角色配置会保存到 `Application.persistentDataPath/player_profile.json`。

## 双机测试

1. 两台 Windows 电脑分别登录不同 Steam 账号，并确保双方是 Steam 好友。
2. 两台电脑运行完全相同的构建目录，不能只复制 exe。
3. 房主点击“创建 Steam 房间”，再通过“邀请”打开 Steam Overlay；Overlay 不可用时复制大厅 ID 给另一方。
4. 客户端接受 Steam 邀请，或把大厅 ID 粘贴到首页后点击“加入”。
5. 双方选择角色、颜色和两个技能并准备，由房主开始战斗。

该构建使用 Spacewar AppID `480`，仅用于本地学习测试。正式发布前必须替换为自己的 AppID，并重新验证 Steamworks 后台配置。

## 联机边界

- 房间固定两人、仅好友可见，元数据协议版本为 `1`。
- FizzySteamworks 使用新版 SteamSockets；房主是唯一战斗模拟权威。
- 客户端只提交角色配置并播放房主发送的可靠有序事件。
- 不支持中途加入、掉线重连、房主迁移、公共匹配、聊天或奖励系统。
