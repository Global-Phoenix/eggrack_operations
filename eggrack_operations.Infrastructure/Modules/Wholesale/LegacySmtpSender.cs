using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace Eggrack.Operations.Infrastructure.Modules.Wholesale;

public sealed record LegacySmtpSettings(string Host,int Port,string Login,string Password,string FromAddress,string FromName,IReadOnlyList<string> InternalRecipients);

public sealed class LegacySmtpSender
{
    public async Task SendHtmlAsync(LegacySmtpSettings settings,string recipient,string subject,string html,CancellationToken token)
    {
        var recipients=new[]{recipient}.Concat(settings.InternalRecipients)
            .Select(value=>value.Trim()).Where(value=>value.Length>0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach(var address in recipients)await SendOneAsync(settings,address,subject,html,token);
    }

    private static async Task SendOneAsync(LegacySmtpSettings settings,string recipient,string subject,string html,CancellationToken token)
    {
        using var tcp=new TcpClient();
        await tcp.ConnectAsync(settings.Host,settings.Port,token);
        await using var ssl=new SslStream(tcp.GetStream(),false);
        await ssl.AuthenticateAsClientAsync(settings.Host);
        using var reader=new StreamReader(ssl,Encoding.ASCII,false,1024,true);
        await using var writer=new StreamWriter(ssl,new UTF8Encoding(false),1024,true){NewLine="\r\n",AutoFlush=true};
        await ExpectAsync(reader,2,token);
        await CommandAsync(writer,reader,"EHLO eggrack-operations",2,token);
        await CommandAsync(writer,reader,"AUTH LOGIN",3,token);
        await CommandAsync(writer,reader,Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.Login)),3,token);
        await CommandAsync(writer,reader,Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.Password)),2,token);
        await CommandAsync(writer,reader,$"MAIL FROM:<{settings.FromAddress}>",2,token);
        await CommandAsync(writer,reader,$"RCPT TO:<{recipient}>",2,token);
        await CommandAsync(writer,reader,"DATA",3,token);
        var encodedSubject=Convert.ToBase64String(Encoding.UTF8.GetBytes(subject));
        var encodedBody=Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
        await writer.WriteLineAsync($"From: \"{settings.FromName.Replace("\"",string.Empty)}\" <{settings.FromAddress}>");
        await writer.WriteLineAsync($"To: <{recipient}>");
        await writer.WriteLineAsync($"Subject: =?UTF-8?B?{encodedSubject}?=");
        await writer.WriteLineAsync("MIME-Version: 1.0");
        await writer.WriteLineAsync("Content-Type: text/html; charset=UTF-8");
        await writer.WriteLineAsync("Content-Transfer-Encoding: base64");
        await writer.WriteLineAsync();
        for(var offset=0;offset<encodedBody.Length;offset+=76)await writer.WriteLineAsync(encodedBody.Substring(offset,Math.Min(76,encodedBody.Length-offset)));
        await writer.WriteLineAsync(".");
        await ExpectAsync(reader,2,token);
        await CommandAsync(writer,reader,"QUIT",2,token);
    }

    private static async Task CommandAsync(StreamWriter writer,StreamReader reader,string command,int expectedClass,CancellationToken token)
    {
        await writer.WriteLineAsync(command);
        await ExpectAsync(reader,expectedClass,token);
    }

    private static async Task ExpectAsync(StreamReader reader,int expectedClass,CancellationToken token)
    {
        string? line;
        do
        {
            line=await reader.ReadLineAsync(token);
            if(line is null||line.Length<3||!int.TryParse(line[..3],out var code))throw new IOException("SMTP 服务器响应无效。");
            if(code/100!=expectedClass)throw new IOException($"SMTP 服务器返回错误代码 {code}。");
        }while(line.Length>3&&line[3]=='-');
    }
}
