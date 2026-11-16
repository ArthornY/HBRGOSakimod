# 31C 羁绊「魔界骑兵启动！」用的原作图（2026-11-14）

「31O 改牌」会话从本机解包库抠出来的原作素材，只给模组内部用。需求四十七和它的更正引用这里的文件。

| 文件 | 用途 | 来源 |
|---|---|---|
| `machina_button_active.png` | 按钮：可以按 | `BattleAtlas`（`extracted\images\cache\ee90dcc721d143e2a829c97ecc46af67\a71b00fbc6bcc5ce96f78d8b253af4a6\…BattleAtlas-88b70823.png`）按透明通道抠出 |
| `machina_button_small.png` | 按钮：鼠标悬停（金边） | 同上 |
| `machina_button_disabled.png` | 按钮：用完、不是自己回合 | 同上 |
| `yamawaki_servant_icon.png` | 「山胁大人的仆人」计数的小图标；31C 羁绊遗物图标的占位 | 原作 `IconBIYamawakiServant`（`extracted\images\cache\77f052d6efa63013ec76f34a8c09096e\412a4d09f39e4da4547dd046ec38e518\`） |
| `fear_of_devil_icon.png` | 能力「对魔王的畏惧」的图标 | 原作 `IconFearOfDevil`，同上目录 |
| `yamawaki_enemy_servant_icon.png` | 备用 | 原作 `IconBIYamawakiEnemyServant`，同上目录 |

- 同一批抠出来的四个魔王脸圆图标，用户指出是原作司令官（手塚咲）的「鬼神化」按钮，不是 31C 的。已挪到 `..\..\tezuka-kishin-20261114\icons\`，31C 不用。
- `sprites.json` 记了每张图在图集里的位置和尺寸。
- `preview.png` 是总览图，灰底。
- 抠图时把完全透明像素的颜色清成了黑色，图集里透明处原本残留的杂色不会带进来。
- 脚本在「31O 改牌」会话的 scratchpad：`crop_31c_button.py`、`split_kishin_icons.py`。
