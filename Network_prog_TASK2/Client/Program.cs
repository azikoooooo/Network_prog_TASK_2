using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Client
{
    public class UserDto
    {
        [JsonPropertyName("username")]
        public string username { get; set; } = "";

        [JsonPropertyName("remoteendpoint")]
        public string remoteendpoint { get; set; } = "-";

        [JsonPropertyName("isOnline")]
        public bool isOnline { get; set; } = false;
    }

    public class LoginRequest
    {
        [JsonPropertyName("username")]
        public string username { get; set; } = "";

        [JsonPropertyName("password")]
        public string password { get; set; } = "";
    }

    public class LoginResponse
    {
        [JsonPropertyName("success")]
        public bool success { get; set; }

        [JsonPropertyName("message")]
        public string message { get; set; } = "";
    }

    public class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "TCP Client - Task 2";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================");
            Console.WriteLine("                         TCP CLIENT                            ");
            Console.WriteLine("===============================================================");
            Console.ResetColor();

            Console.Write("Server IP ünvanı (Varsayılan 127.0.0.1): ");
            var ipInput = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(ipInput))
                ipInput = "127.0.0.1";

            var ip = IPAddress.Parse(ipInput);
            var port = 27001;
            var ep = new IPEndPoint(ip, port);

            while (true)
            {
                TcpClient client = new TcpClient();

                try
                {
                    client.Connect(ep);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Serverə qoşulmaq olmadı: {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine("Yenidən yoxlamaq üçün Enter basın (Çıxış üçün 'q')...");
                    var key = Console.ReadLine();
                    if (key?.ToLower() == "q")
                        return;
                    continue;
                }

                var stream = client.GetStream();
                var bw = new BinaryWriter(stream);
                var br = new BinaryReader(stream);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n--- İSTİFADƏÇİ GİRİŞİ ---");
                Console.ResetColor();

                Console.Write("Username: ");
                var username = Console.ReadLine()?.Trim() ?? "";

                Console.Write("Password: ");
                var password = Console.ReadLine()?.Trim() ?? "";

                var req = new LoginRequest { username = username, password = password };
                bw.Write(JsonSerializer.Serialize(req));

                string responseJson;
                try
                {
                    responseJson = br.ReadString();
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Serverdən cavab alınmadı.");
                    Console.ResetColor();
                    client.Close();
                    continue;
                }

                var response = JsonSerializer.Deserialize<LoginResponse>(responseJson);

                if (response == null || !response.success)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[GİRİŞ UĞURSUZ] {response?.message ?? "Xəta baş verdi"}");
                    Console.ResetColor();
                    client.Close();

                    Console.WriteLine("\nYenidən cəhd etmək üçün Enter basın (Çıxış üçün 'q')...");
                    var retry = Console.ReadLine();
                    if (retry?.ToLower() == "q")
                        return;

                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[GİRİŞ TƏSDİQLƏNDİ] {response.message}");
                Console.ResetColor();

                Console.Title = $"TCP Client - {username}";

                var readerTask = Task.Run(() =>
                {
                    try
                    {
                        while (true)
                        {
                            var msg = br.ReadString();

                            if (msg.StartsWith("[USER_LIST]"))
                            {
                                var json = msg.Substring("[USER_LIST]".Length);
                                var users = JsonSerializer.Deserialize<List<UserDto>>(json);
                                if (users != null)
                                {
                                    PrintUserList(users);
                                    Console.Write("> ");
                                }
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"\n{msg}");
                                Console.ResetColor();
                                Console.Write("> ");
                            }
                        }
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("\n[Əlaqə kəsildi]");
                        Console.ResetColor();
                    }
                });

                Console.WriteLine("\nMesaj göndərmək üçün yazın. Çıxış üçün 'Exit' daxil edin.");
                Console.Write("> ");

                while (true)
                {
                    var text = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    try
                    {
                        bw.Write(text);
                    }
                    catch
                    {
                        Console.WriteLine("Serverə məlumat göndərilə bilmədi.");
                        break;
                    }

                    if (text.Trim().Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Sistemdən çıxılır...");
                        Console.ResetColor();
                        Thread.Sleep(500);
                        client.Close();
                        return;
                    }

                    Console.Write("> ");
                }

                break;
            }
        }

        static void PrintUserList(List<UserDto> users)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("===============================================================");
            Console.WriteLine("                ONLINE / OFFLINE İSTİFADƏÇİLƏR                ");
            Console.WriteLine("===============================================================");
            Console.WriteLine($"{"İstifadəçi",-15} {"Son IP Ünvanı",-25} {"Status",-10}");
            Console.WriteLine("---------------------------------------------------------------");
            Console.ResetColor();

            foreach (var user in users)
            {
                Console.Write($"{user.username,-15} {user.remoteendpoint,-25} ");
                if (user.isOnline)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("ONLINE");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("OFFLINE");
                }
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("===============================================================");
            Console.ResetColor();
        }
    }
}
