using MegaCrit.Sts2.Core.Map;

namespace HBRGOSakiMod.Core.Map;

public sealed class HbrgoActFourMap : ActMap
{
    private const int CenterColumn = 3;

    private readonly MapPoint[,] _grid = new MapPoint[7, 4];

    public override MapPoint BossMapPoint { get; }

    public override MapPoint StartingMapPoint { get; }

    protected override MapPoint[,] Grid => _grid;

    public HbrgoActFourMap()
    {
        StartingMapPoint = CreatePoint(
            CenterColumn,
            0,
            MapPointType.Ancient);
        MapPoint shop = CreatePoint(
            CenterColumn,
            1,
            MapPointType.Shop);
        MapPoint firstRest = CreatePoint(
            CenterColumn,
            2,
            MapPointType.RestSite);
        MapPoint secondRest = CreatePoint(
            CenterColumn,
            3,
            MapPointType.RestSite);
        BossMapPoint = CreatePoint(
            CenterColumn,
            4,
            MapPointType.Boss);

        _grid[CenterColumn, 1] = shop;
        _grid[CenterColumn, 2] = firstRest;
        _grid[CenterColumn, 3] = secondRest;

        startMapPoints.Add(shop);
        StartingMapPoint.AddChildPoint(shop);
        shop.AddChildPoint(firstRest);
        firstRest.AddChildPoint(secondRest);
        secondRest.AddChildPoint(BossMapPoint);
    }

    private static MapPoint CreatePoint(
        int column,
        int row,
        MapPointType pointType)
    {
        return new MapPoint(column, row)
        {
            PointType = pointType,
            CanBeModified = false
        };
    }
}
