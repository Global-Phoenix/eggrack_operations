using System.Text;
using Eggrack.Operations.Infrastructure.Modules.Security.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

Console.OutputEncoding = Encoding.UTF8;

var arguments = Arguments.Parse(args);
var configPath = Path.GetFullPath(arguments.ConfigPath);
if (!File.Exists(configPath))
    return Fail($"配置文件不存在：{configPath}");

var configuration = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: false)
    .Build();
var connectionString = configuration.GetConnectionString("EggrackConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    return Fail("配置中缺少 ConnectionStrings:EggrackConnection。");

var password = ReadSecret("新密码：");
var confirmation = ReadSecret("再次输入：");
if (!string.Equals(password, confirmation, StringComparison.Ordinal))
    return Fail("两次输入的密码不一致。");

var services = new ServiceCollection()
    .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
services.AddInternalIdentity(configuration);
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var users = scope.ServiceProvider.GetRequiredService<UserManager<InternalIdentityUser>>();

if (await users.FindByNameAsync(arguments.UserName) is not null)
    return Fail("用户名已存在，未做任何修改。");
if (await users.FindByEmailAsync(arguments.Email) is not null)
    return Fail("邮箱已存在，未做任何修改。");

var user = new InternalIdentityUser
{
    UserName = arguments.UserName,
    Email = arguments.Email,
    EmailConfirmed = true,
    DisplayName = arguments.DisplayName,
    StaffRef = $"bootstrap-{Guid.NewGuid():N}",
    MustEnableTwoFactor = true,
    LockoutEnabled = true
};

var createResult = await users.CreateAsync(user, password);
if (!createResult.Succeeded)
    return Fail(string.Join(Environment.NewLine, createResult.Errors.Select(x => $"- {x.Description}")));

var staffRef = user.Id.ToString();
user.StaffRef = staffRef;
var updateResult = await users.UpdateAsync(user);
if (!updateResult.Succeeded)
{
    await users.DeleteAsync(user);
    return Fail("账号映射失败，已回滚 Identity 用户。" + Environment.NewLine +
                string.Join(Environment.NewLine, updateResult.Errors.Select(x => $"- {x.Description}")));
}

try
{
    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    const string insertStaff = """
        INSERT INTO eggrack_auth_staff
            (staff_ref, staff_name, email, status, auth_version)
        VALUES
            (@StaffRef, @DisplayName, @Email, 1, 1)
        """;
    await using (var command = new MySqlCommand(insertStaff, connection, transaction))
    {
        command.Parameters.AddWithValue("@StaffRef", staffRef);
        command.Parameters.AddWithValue("@DisplayName", arguments.DisplayName);
        command.Parameters.AddWithValue("@Email", arguments.Email);
        await command.ExecuteNonQueryAsync();
    }

    const string assignRole = """
        INSERT INTO eggrack_auth_staff_role (staff_id, role_id, department_id, granted_by)
        SELECT s.id, r.id, NULL, s.id
        FROM eggrack_auth_staff s
        INNER JOIN eggrack_auth_role r ON r.role_code = 'super_admin' AND r.status = 1
        WHERE s.staff_ref = @StaffRef
        """;
    await using (var command = new MySqlCommand(assignRole, connection, transaction))
    {
        command.Parameters.AddWithValue("@StaffRef", staffRef);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("未找到启用的 super_admin 角色。");
    }

    await transaction.CommitAsync();
}
catch (Exception exception)
{
    await users.DeleteAsync(user);
    return Fail($"权限绑定失败，已回滚 Identity 用户：{exception.Message}");
}

Console.WriteLine();
Console.WriteLine("超级管理员创建成功。");
Console.WriteLine($"用户名：{arguments.UserName}");
Console.WriteLine("首次登录必须绑定 TOTP 双因素认证。");
return 0;

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return value.ToString();
        }
        if (key.Key == ConsoleKey.Backspace && value.Length > 0)
        {
            value.Length--;
            continue;
        }
        if (!char.IsControl(key.KeyChar))
            value.Append(key.KeyChar);
    }
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

file sealed record Arguments(string ConfigPath, string UserName, string Email, string DisplayName)
{
    public static Arguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2)
            values[args[i]] = args[i + 1];

        return new Arguments(
            Required(values, "--config"),
            Required(values, "--username"),
            Required(values, "--email"),
            Required(values, "--display-name"));
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException($"缺少参数 {key}");
}
