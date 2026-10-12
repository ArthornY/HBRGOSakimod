using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Logging;

namespace HBRGOSakiMod.Core.Integration;

internal static class NararaShibaFloorSync
{
    private const string Endpoint =
        "https://ngabbs.com/app_api.php?__lib=post&__act=list";
    private const string ThreadId = "41989465";

    private static readonly HttpClient Client = CreateClient();

    internal static int CurrentFloor =>
        HbrgoSakiVariantPreference.GetNararaShibaFloor();

    internal static async Task<int> RefreshAsync()
    {
        try
        {
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["tid"] = ThreadId,
                    ["page"] = "e"
                });
            using HttpResponseMessage response =
                await Client.PostAsync(Endpoint, content);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(json);

            int highestLou = 0;
            int visibleRows = 0;
            ScanFloorValues(
                document.RootElement,
                ref highestLou,
                ref visibleRows);
            int floor = highestLou > 0
                ? highestLou
                : Math.Max(0, visibleRows - 1);
            if (floor <= 0)
            {
                throw new JsonException(
                    "The NGA response contained no valid floor number.");
            }

            HbrgoSakiVariantPreference.SetNararaShibaFloor(floor);
            Log.Info(
                $"Synced Narara Shiba HP to NGA thread {ThreadId} floor {floor}.");
            return floor;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
                TaskCanceledException or
                JsonException)
        {
            int cachedFloor = CurrentFloor;
            Log.Warn(
                $"Failed to sync Narara Shiba HP; using cached floor " +
                $"{cachedFloor}: {exception.Message}");
            return cachedFloor;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-User-Agent",
            "NGA_skull/7.3.1");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "NGA/7.3.1");
        return client;
    }

    private static void ScanFloorValues(
        JsonElement element,
        ref int highestLou,
        ref int visibleRows)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("lou") &&
                        TryReadPositiveInt(property.Value, out int lou))
                    {
                        highestLou = Math.Max(highestLou, lou);
                    }
                    else if (property.NameEquals("vrows") &&
                             TryReadPositiveInt(
                                 property.Value,
                                 out int rows))
                    {
                        visibleRows = Math.Max(visibleRows, rows);
                    }

                    ScanFloorValues(
                        property.Value,
                        ref highestLou,
                        ref visibleRows);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                {
                    ScanFloorValues(
                        child,
                        ref highestLou,
                        ref visibleRows);
                }

                break;
        }
    }

    private static bool TryReadPositiveInt(
        JsonElement element,
        out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value))
        {
            return value > 0;
        }

        return element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), out value) &&
            value > 0;
    }
}
