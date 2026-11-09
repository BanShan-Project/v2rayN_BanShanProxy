namespace v2rayN.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        btnLogin.Click += BtnLogin_Click;
        btnCancel.Click += (_, _) => Close(false);
        Opened += LoginWindow_Opened;
        CanMinimize = false;
    }

    private async void LoginWindow_Opened(object? sender, EventArgs e)
    {
        try
        {
            var current = await BanShanHandler.CurrentUsername();
            if (!string.IsNullOrWhiteSpace(current))
            {
                txtUsername.Text = current;
                txtStatus.Text = $"当前账号：{current}。重新登录会刷新节点；换号会替换旧节点。";
                txtPassword.Focus();
            }
            else
            {
                txtUsername.Focus();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("BanShanLoginWindowOpened", ex);
            ShowStatus($"读取登录状态失败：{ex.Message}", isError: true);
        }
    }

    private async void BtnLogin_Click(object? sender, RoutedEventArgs e)
    {
        var userName = txtUsername.Text?.Trim() ?? string.Empty;
        var password = txtPassword.Text ?? string.Empty;
        if (userName.Length == 0 || password.Length == 0)
        {
            ShowStatus("请输入用户名和密码。", isError: true);
            return;
        }

        SetBusy(true);
        ShowStatus("正在登录...", isError: false);
        try
        {
            var result = await BanShanHandler.LoginAsync(AppManager.Instance.Config, userName, password);
            if (result.Success)
            {
                Close(true);
                return;
            }
            ShowStatus(result.Message, isError: true);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("BanShanLoginWindow", ex);
            ShowStatus($"登录失败：{ex.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        txtUsername.IsEnabled = !busy;
        txtPassword.IsEnabled = !busy;
        btnLogin.IsEnabled = !busy;
        btnCancel.IsEnabled = !busy;
        btnLogin.Content = busy ? "登录中..." : "登录";
    }

    private void ShowStatus(string message, bool isError)
    {
        txtStatus.Text = message;
        txtStatus.Foreground = isError ? Brushes.IndianRed : Brushes.Gray;
    }
}
