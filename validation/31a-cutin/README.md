# 31A 插嘴校验

`Checks.csproj` 直接编译生产 `ThirtyOneACutinLibrary.cs`，验证总台词文件、用途、条件、Live 专用句、禁用接话，以及真实语料和人工循环图的四句上限。它不依赖 Godot。

```powershell
dotnet run --project validation/31a-cutin/Checks.csproj -c Release -- . outputs/31a-28-37-20261114/cutin-library.json
C:/Users/ROG/miniconda3/python.exe -X utf8 validation/31a-cutin/assets.py
```

`assets.py` 校验已复制文件的 SHA-256、PNG 尺寸、OGG 编码／采样率、当前工作选项，以及 16:9、16:10、21:9 和 720p 的停留边界。只读取文件；不操作 Godot，不重新处理图片或音频。

真实羁绊模型与命令检查放在 `validation/31a-memory-upgrades/CutinCases.cs`，由同一 native runtime profile 套件执行，覆盖牌种去重、HBR 闪避分支解除、Live 保存标记及真实士气授予、由希次数消耗和跨双方回合保留。

来源清单：`outputs/31a-28-37-20261114/31a-cutin-assets.json`。读取 `cutin-lines-full.json` 并合并 `cutin-picks-working.json` 的用途标记；原始剧情译文不改。只复制可用句与已启用接话能引用的 27 张图、1743 条声音。JSON 自身保留全部 1708 句／534 条接话及禁用状态，方便后续换数据。

条件映射：SkullFeather 在场／第一阶段高攻击增益招之前／首次观察到转入第二阶段的这一轮，FlatHand 第三阶段断链招之前，以及 Boss 战第一轮。原作存在但模组未实现的敌人条件保持禁用。条件句权重为普通句的五倍。

声音按句播放。回应在上一句声音结束时开始，最多四句，不重复；回应不产生羁绊效果，也不要求该成员在卡组。六名主说话者依次排队复用安全位置；回应可短暂叠在上一句的淡出尾部。最终句至少显示 1.15 秒，声音结束后保留 0.3 秒。

以上不等于进游戏测试：Godot 导入、着色器实际画面、语音听感、三个分辨率实机演示及真实联机仍需另行游戏内确认。

2026-11-14 本轮结果：

- `outputs/31a-28-37-20261114/cutin-library.json`：5170 项通过。
- `outputs/31a-28-37-20261114/cutin-assets-check.json`：10147 项通过。
- `outputs/31a-28-37-20261114/requirement28-notes.md`：最终规则、资源及边界。
