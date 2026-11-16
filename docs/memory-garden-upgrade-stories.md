# 记忆之庭升级回忆

每位庭园角色可添加一份 `story/memory_upgrades/<角色ID>.json`。ID沿用现有剧情角色名册，例如 `ruka`、`tama`、`mion`。新增或修改剧情只需改数据，再按正常流程构建资源；不需要注册事件或修改C#。

文件格式见[空模板](templates/memory-upgrade-story.template.json)。模板本身不在运行资源中，也没有测试剧情。

- `schemaVersion`固定为1；`characterId`必须与文件名相同。
- `presentation`与记忆碎片事件相同：背景路径、经典/原作对话样式、心声头像和立绘布局。每段可提供完整的`presentation`替换文件默认值。
- `segments`列段落；`level`是实际第1～7次升级，与选择哪条升级效果无关。拿到0级牌不播放。
- `perspectives`可列`31o`、`30a`、`hotaru`中的一个或多个。相同等级可有不同视角版本，但不可有重复覆盖。
- `lines`直接使用记忆碎片事件的行格式：`kind`为`narration`/`thought`/`dialogue`，以及`text`、`speaker`、`portrait`、`background`、`portraitLayout`、`characters`。背景与场上立绘按既有逐行继承规则处理。
- 只读回忆不接受选项、奖励、战斗、相遇、跳页或脚本字段。图片只能指向合法的`res://images/`路径。
- 文件、等级或当前视角缺文时安静跳过。允许先放空`segments`，不需要伪造占位台词。

剧情只在升级者本机显示，不会创建同步事件、替换逻辑房间或进入原生OverlayStack。原火堆、庭园、事件或奖励界面保留；读完或点“跳过”后回到原控件。暂停/检视等界面仍可覆盖阅读。原界面或当前run消失时结束为Abandoned，不重建旧场景。

本机设置为全部放/只放没看过的/关。Off、缺文、Unseen已看过滤不占本局Started；真正开始前写本局Started，SL不补放。读完或主动跳过记为跨局Seen，中断不记Seen。演出读速、设置和记录不写同步手册或玩法状态。

开发预览：在安全的本地非战斗界面输入 `hbrmemory preview ruka 1 31o`（角色、等级、视角可替换）。命令为非网络命令，绕过设置和Started/Seen账本；不会生成剧情，缺文会提示无内容。它沿用现有控制台的开发操作记录规则。

验证用临时文字仅在`validation/memory-upgrade-stories`中；不得把测试文件放进运行目录。
