# v0.5.1 验收记录

2026-09-18，Unity 2022.3.51f1。按用户反馈取消第三人称局部剔除，保留原俯角、距离及俯视模式。状态：已完成本轮约定检查。

- 相机与空间站表现 PlayMode **11/11 通过**，包含三种俯角、近/远距离与 360° 环绕中的完整几何、完整镜头碰撞代理、切回第三人称立即恢复、默认俯视、V 切换、输入清理、Any/All、撤销/重开。测试结果见 [汇总](PlayMode.json)，原始 XML 为 `Logs/V0.5.1/PlayMode-11.xml`。
- v0.5.0 的射线/包围盒检测、空间余量与恢复延迟已删除。墙体和门上层现在只按视角模式显示/隐藏；正式关卡、模型资源与 GUID 未改。
- 取消隐藏后，完整墙体仍会让 Cinemachine 在贴墙时缩短实际距离；本次未通过删墙、穿墙或降低墙体替代原效果。

实际 Game View 已查看：[L06 第三人称](ThirdPerson.png)、[90° 贴墙机位](WallSide.png)、[切回俯视](TopDown.png)。同一贴墙机位下，40/40 个墙上层 Renderer 和 3/3 个门上层 Renderer 均启用，墙碰撞高恢复为 1.54 m；[状态记录](Visibility.json)。该机位因正常避障将实际距离缩短到约 0.58 m，未将其描述为新的观察体验优化。

首次构建成功但报告有待编译改动，见 [原警告](BuildMessages.json)；因此显式完成脚本刷新后重建，最终状态以 [构建结果](Build.json) 为准。

最终 Windows 非开发包构建成功，0 错误、0 警告，位置为 `Builds/V0.5.1/Windows/Sokoban.exe`；[构建请求](BuildRequest.json) 显式使用 Bootstrap。此次未重复上一版本的独立 Player 键盘验证。编译与 [Console](Console.json) 无错误，[Editor](FinalEditor.json) 恢复为 Bootstrap、非 Play Mode、无未保存场景改动。文档、本地链接及资源保持情况见 [检查结果](Verification.json)。
