using System;
using System.Threading.Tasks;
using BiliLiveNotifier.Core;

public static class ApiClientTester
{
    public static async Task RunTestAsync()
    {
        try
        {
            ApiClient.LoadEndpoints("api_endpoints.json");
            long uid = 496751305;

            var roomResult = await ApiClient.RequestMappedAsync("GetMasterInfo", uid);
            Console.WriteLine("========== GetMasterInfo ==========");
            PrintDictionary(roomResult);

            // 安全提取 roomId
            long? roomId = null;
            if (roomResult?.TryGetValue("roomId", out var roomObj) == true && roomObj != null)
            {
                roomId = Convert.ToInt64(roomObj);
            }

            if (!roomId.HasValue)
            {
                Console.WriteLine("未能获取到 roomId，后续测试跳过。");
                return;
            }

            var liveResult = await ApiClient.RequestMappedAsync("GetLiveRoomDetail", roomId.Value);
            Console.WriteLine("\n========== GetLiveRoomDetail ==========");
            PrintDictionary(liveResult);

            var birthdayResult = await ApiClient.RequestMappedAsync("GetUserInfo", uid);
            Console.WriteLine("\n========== GetUserInfo ==========");
            PrintDictionary(birthdayResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"测试过程中发生异常: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"内部异常: {ex.InnerException.Message}");
        }
    }

    private static void PrintDictionary(Dictionary<string, object?>? dict)
    {
        if (dict == null)
        {
            Console.WriteLine("返回结果为 null");
            return;
        }
        if (dict.Count == 0)
        {
            Console.WriteLine("返回字典为空");
            return;
        }
        foreach (var kv in dict)
        {
            string valueStr = kv.Value?.ToString() ?? "<null>";
            Console.WriteLine($"{kv.Key}: {valueStr}");
        }
    }
}