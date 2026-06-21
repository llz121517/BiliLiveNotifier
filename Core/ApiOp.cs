// Core/ApiOp.cs
using System.Text.Json;
using System.Text.Json.Nodes;
using BiliLiveNotifier.Core;

namespace BiliLiveNotifier.Core;

/// <summary>
/// B站直播相关 API 模块
/// </summary>
public static class ApiOp
{
    /// <summary>
    /// 通过 UID 获取真实直播间 ID (room_id)
    /// </summary>
    /// <param name="uid">目标用户 UID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功返回 room_id，失败返回 null</returns>
    public static async Task<long?> GetRoomIdByUidAsync(long uid, CancellationToken ct = default)
    {
        string url = $"https://api.live.bilibili.com/live_user/v1/Master/info?uid={uid}";

        // 记录请求入参，便于确认传入的 UID 是否正确
        LLog.Debug($"[ApiOp] Requesting RoomID for UID: {uid}, URL: {url}");

        try
        {
            var response = await BiliApiClient.GetAsync<JsonObject>(url, ct: ct);

            // 序列化整个响应对象以记录完整结构，若为 null 则标记为空
            string rawResponse = response != null
                ? JsonSerializer.Serialize(response)
                : "null";
            LLog.Debug($"[ApiOp] UID:{uid} Raw Response: {rawResponse}");

            if (response?.Code == 0 && response.Data != null)
            {
                var roomIdNode = response.Data["room_id"];

                // 检查关键字段是否存在
                if (roomIdNode == null)
                {
                    LLog.Warn($"[ApiOp] UID:{uid} Response Code=0 but missing 'room_id' field");
                    return null;
                }

                // 兼容 room_id 可能以字符串形式返回的情况
                if (long.TryParse(roomIdNode.ToString(), out long roomId))
                {
                    LLog.Info($"[ApiOp] UID:{uid} resolved to RoomID:{roomId}");
                    return roomId;
                }

                LLog.Warn($"[ApiOp] UID:{uid} Failed to parse room_id, raw value: '{roomIdNode}'");
                return null;
            }

            // 记录业务层失败时的错误码与错误信息
            LLog.Warn($"[ApiOp] UID:{uid} API returned error, Code: {response?.Code}, Message: {response?.Message}");
            return null;
        }
        catch (OperationCanceledException)
        {
            // 请求被主动取消时记录日志并向上抛出，避免吞掉取消信号
            LLog.Warn($"[ApiOp] UID:{uid} Request was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            // 捕获网络异常、序列化异常等底层错误，记录完整堆栈信息
            LLog.Error($"[ApiOp] UID:{uid} Request exception: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
}