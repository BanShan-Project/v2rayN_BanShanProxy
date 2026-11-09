using System.Net.Http;

namespace ServiceLib.Handler;

/// <summary>
/// The BanShan account login shared by the desktop UI:
/// POST https://proxy.yinshaoze.asia/get_sub with the credentials both in the query string (what the
/// server currently reads) and in the form body (what a fixed server can read), then import the
/// subscription returned in `data`.
///
/// The subscription is stored as a normal <see cref="SubItem"/> with a fixed id. It is hidden from
/// the subscription lists, its profiles are locked, and it is removed from backups/restores.
/// </summary>
public static class BanShanHandler
{
    public const string SubId = "banshan";
    public const string LoginUrl = "https://proxy.yinshaoze.asia/get_sub";
    public const string DisplayName = "半山代理";
    public const string ProtectedMessage = "半山代理登录订阅受保护：不能修改、删除、导出、分享或移动。";
    public const string DatabaseFileName = "guiNDB.db";

    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = true,
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public sealed record LoginResult(bool Success, string Message, int Count = 0, string? Url = null);

    public static bool IsManagedSubId(string? subId) => string.Equals(subId, SubId, StringComparison.Ordinal);

    public static bool IsManagedSubItem(SubItem? item) => item != null && IsManagedSubId(item.Id);

    public static bool IsManagedProfile(ProfileItem? profile) => profile != null && IsManagedSubId(profile.Subid);

    public static bool IsManagedProfile(ProfileItemModel? profile) => profile != null && IsManagedSubId(profile.Subid);

    public static async Task<SubItem?> GetSubItem()
    {
        return await AppManager.Instance.GetSubItem(SubId);
    }

    public static async Task<bool> IsLoggedIn()
    {
        return await GetSubItem() != null;
    }

    public static async Task<string?> CurrentUsername()
    {
        return (await GetSubItem())?.Memo;
    }

    /// <summary>
    /// Signs in, stores the subscription and imports its nodes. If a previous login exists and the
    /// new account is broken, the previous address is restored and its nodes are kept.
    /// </summary>
    public static async Task<LoginResult> LoginAsync(Config config, string userName, string password)
    {
        var account = userName.Trim();
        if (account.IsNullOrEmpty() || password.IsNullOrEmpty())
        {
            return new LoginResult(false, "请输入用户名和密码。");
        }

        var previous = await AppManager.Instance.GetSubItem(SubId);
        var previousUrl = previous?.Url;
        var previousMemo = previous?.Memo;

        var answer = await PostLoginAsync(account, password);
        if (!answer.Success)
        {
            return new LoginResult(false, answer.Message);
        }

        var newUrl = answer.Url!;
        var item = new SubItem
        {
            Id = SubId,
            Remarks = DisplayName,
            Url = newUrl,
            Enabled = true,
            AutoUpdateInterval = 6 * 60,
            Memo = account,
        };
        if (await ConfigHandler.AddSubItem(config, item) != 0)
        {
            return new LoginResult(false, "登录订阅保存失败。");
        }

        var messages = new List<string>();
        try
        {
            var subHandler = new SubscriptionHandler(config, false, (_, message) =>
            {
                if (!message.IsNullOrEmpty())
                {
                    messages.Add(message);
                }
                return Task.CompletedTask;
            });
            await subHandler.UpdateProcess(SubId);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("BanShanLogin", ex);
        }

        var profiles = await AppManager.Instance.ProfileItems(SubId) ?? [];
        if (profiles.Count == 0)
        {
            // A first, broken login must not leave a hidden "logged in" row behind. An existing
            // login keeps the old address (and its old nodes) instead.
            if (previousUrl.IsNullOrEmpty())
            {
                var created = await AppManager.Instance.GetSubItem(SubId);
                if (created != null)
                {
                    await SQLiteHelper.Instance.DeleteAsync(created);
                }
            }
            else if (previous != null)
            {
                previous.Url = previousUrl;
                previous.Memo = previousMemo;
                await ConfigHandler.AddSubItem(config, previous);
            }

            var detail = messages.LastOrDefault();
            return new LoginResult(false,
                detail.IsNullOrEmpty() ? "订阅没有返回可用节点，请检查账号或稍后重试。" : detail);
        }

        // Make the freshly imported nodes visible: the "all groups" view includes the hidden sub.
        config.SubIndexId = string.Empty;
        return new LoginResult(true, $"登录成功，已导入 {profiles.Count} 个节点。", profiles.Count, newUrl);
    }

    private static async Task<(bool Success, string Message, string? Url)> PostLoginAsync(string userName, string password)
    {
        var query = $"?username={Uri.EscapeDataString(userName)}&password={Uri.EscapeDataString(password)}";
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = userName,
            ["password"] = password,
        });

        HttpResponseMessage response;
        string text;
        try
        {
            response = await _http.PostAsync(LoginUrl + query, body);
            text = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Logging.SaveLog("BanShanLogin", ex);
            return (false, $"无法连接登录服务器：{ex.Message}", null);
        }

        using (response)
        {
            if (text.IsNullOrEmpty())
            {
                return (false, $"登录服务器返回空响应（HTTP {(int)response.StatusCode}）。", null);
            }

            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var codeText = root.TryGetProperty("code", out var codeEl) ? codeEl.ToString() : string.Empty;
                _ = int.TryParse(codeText, out var code);
                var message = GetString(root, "msg") ?? GetString(root, "message") ?? string.Empty;
                var data = GetString(root, "data");
                if (data.IsNullOrEmpty() || string.Equals(data, "Null", StringComparison.OrdinalIgnoreCase))
                {
                    data = null;
                }
                data ??= GetString(root, "sub_url") ?? GetString(root, "subUrl") ?? GetString(root, "url");

                if (code != 200)
                {
                    return (false, message.IsNullOrEmpty()
                        ? $"登录失败（code {codeText}，HTTP {(int)response.StatusCode}）。"
                        : message, null);
                }

                if (!IsHttpUrl(data))
                {
                    return (false, message.IsNullOrEmpty()
                        ? "登录服务器没有返回有效的订阅地址。"
                        : $"{message}（订阅地址无效）", null);
                }

                return (true, message.IsNullOrEmpty() ? "登录成功。" : message, data);
            }
            catch (JsonException)
            {
                return (false, $"登录服务器返回异常（HTTP {(int)response.StatusCode}）。", null);
            }
        }
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.ToString().Trim(),
        };
    }

    private static bool IsHttpUrl(string? value)
    {
        if (value.IsNullOrEmpty())
        {
            return false;
        }
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Removes the login subscription and its nodes from a database file. Used on the temporary
    /// copy before a backup is zipped, and on the restored database before the app restarts.
    /// </summary>
    public static IReadOnlyCollection<string> RemoveFromDatabase(string dbPath)
    {
        var removed = new List<string>();
        if (!File.Exists(dbPath))
        {
            return removed;
        }

        try
        {
            using var db = new SQLiteConnection(dbPath);
            removed.AddRange(db.Query<ProfileItem>(
                "SELECT * FROM ProfileItem WHERE Subid = ?", SubId).Select(t => t.IndexId));
            db.Execute("DELETE FROM ProfileItem WHERE Subid = ?", SubId);
            db.Execute("DELETE FROM SubItem WHERE Id = ?", SubId);
            try
            {
                db.Execute("VACUUM");
            }
            catch
            {
                // VACUUM is only housekeeping; the rows are already gone.
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("BanShanRemoveFromDatabase", ex);
        }

        return removed;
    }

    /// <summary>Removes the login subscription from a copied config directory before zipping it.</summary>
    public static void RemoveFromConfigDir(string configDir)
    {
        var removed = RemoveFromDatabase(Path.Combine(configDir, DatabaseFileName));

        // Keep the GUI config from naming the login subscription or one of its removed profiles.
        var configPath = Path.Combine(configDir, Global.ConfigFileName);
        if (!File.Exists(configPath))
        {
            return;
        }
        try
        {
            if (JsonNode.Parse(File.ReadAllText(configPath)) is not JsonObject config)
            {
                return;
            }

            static string? Key(JsonObject obj, string name)
                => obj.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

            var subKey = Key(config, nameof(Config.SubIndexId));
            if (subKey != null && IsManagedSubId(GetNodeString(config[subKey])))
            {
                config[subKey] = string.Empty;
            }

            var indexKey = Key(config, nameof(Config.IndexId));
            if (indexKey != null && removed.Contains(GetNodeString(config[indexKey]) ?? string.Empty))
            {
                config[indexKey] = string.Empty;
            }

            File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        }
        catch (Exception ex)
        {
            Logging.SaveLog("BanShanRemoveFromConfigDir", ex);
        }
    }

    private static string? GetNodeString(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }
}
