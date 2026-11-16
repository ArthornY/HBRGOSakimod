# 31O 显示名审查

`check.py`对需求27的29张牌、14个配套能力标题及Rapture拼写做一次性只读审查，不建立第二套战斗效果测试。

```powershell
python -X utf8 validation/31o-display-renames/check.py --report outputs/desuwa-renames-20261114/rename-review.json
```

默认基线是`outputs/desuwa-renames-20261114/rename-before/`：从22–25已接受的主检出冻结65个原始文件，并逐文件记录SHA-256。可用`--baseline-root`指定同结构的冻结目录。

审查检查既有本地化键保留、仅准许明确名称替换、数值与格式变量不变、英文不变、常用词「翻倍／移除／强化」及「紧急撤出」保留，并对C# token核对类、标识符、费用、数值与图路径。需求26只准许新增desuwa的本地化键。旧`Repture`类名、文件名和键不更换。

历史上传夹具里的旧版本卡名、NGA／HBR原文引述保留；当前文档及夹具的名称覆盖另做人工核查。该脚本不执行游戏、加载完整存档、验证界面或导出网站数据。
