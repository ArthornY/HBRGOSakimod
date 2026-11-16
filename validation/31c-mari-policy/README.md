# 佐月商店纯策略检查

`dotnet run --project validation/31c-mari-policy/Checks.csproj -c Release -- <报告路径>`。

仅链接纯 `Core/Equipment/HbrSatsukiShopPolicy.cs`，无需游戏程序集。检查50个折扣组合实例、先乘后取整、三种31C门槛、原价及最低1金币、30%概率端点、最高星和高级护符概率、额外+栏、严格大于50的退款及实付价一半的重炼价。

该套件不生成随机数，不代替真实的装备交易、永久牌组或联机验证。
