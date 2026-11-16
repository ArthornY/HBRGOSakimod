# 剧情演出

旧事件和记忆之庭升级回忆共用同一套演出。每条作者行可选填 `direction` 指令数组和 `voice` cue。同一行拆成多屏文字时，演出和语音只执行一次。没有新字段的旧剧情仍可读取；仅人物出入和表情新增默认0.3秒淡化，presentation中的 characterFadeDuration/expressionFadeDuration可改为0关闭。

在剧情工坊的「演出」面板按类别添加、排序或删除指令；默认参数来自共享C#定义。可直接插入「回想开场」「回想收尾」。检查会拒绝未知指令、非法参数和缺失资源。语音填cue名称，不能填文件系统路径。

`scene` 表示在这个位置应用本行背景、人物。未写scene则在行首应用。白屏换场需把scene放到whiteOut和whiteIn之间。时间单位秒；fade/white/wait/cutIn/miniCutIn阻塞，noWait继续后续指令。

按Ctrl会把时长归零，跳过语音、音效、震动、闪白、cut-in和拟声，仍落地背景、人物、滤镜、CG、特效、BGM和环境音。上一句恢复持久状态，不重新播放瞬时声画。暂停/检视覆盖时本地演出和音频暂停；关闭、跳过、切换run/room时清理。没有bgm指令的剧情不改变游戏音乐。

人物原作像素坐标乘5/6，正Y向上；背景取景按原图像素并贴边。震动暂按原作1单位=100原作像素换算，再吃游戏原生震动强度，观感需实机调整。

资源表是 data/story_effects.json、story_visuals.json、story_audio.json。声音只定向导入选中的cue，原作库只读。BGM保留intro、声道、采样率和loopStart；OGG在原loopEnd结束，原HCA总采样数另记来源。HCA导入需配置本机vgmstream与含PyAV/NumPy的Python，已导入OGG的试读和分享不依赖解码工具。

有原作素材的角色才能播放对应cut-in，缺表情不伪造。原Novel字体仅用于新增对话泡/cut-in；旧正文、姓名、拟声和黑幕文字不替换。特效采用原纹理的二维重建，具体三维投影、扭曲和轨迹有近似，不是Unity播放器。

立绘 X.png 可配相邻X.face.json：eyesClosed为{image,x,y}，mouth为恰好三项同格式；坐标是原PNG像素。自动眨眼默认间隔2～6秒、闭眼0.12秒；没有sidecar的图保持不动。voiceCharacter可给场景人物指定原作角色代码或稳定ID，避免A/B等场景ID误配说话人。实际脸部差分由作者提供，不自动制作替代脸图。

本轮只做源代码/受控宿主验证，未启动Godot或进行实机渲染、听音和表情逐像素验收。
