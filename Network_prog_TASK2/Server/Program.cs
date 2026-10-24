using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Server
{
    public class User
    {
        [JsonPropertyName("username")]
        public string username { get; set; } = "";

        [JsonPropertyName("password")]
        public string password { get; set; } = "";

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
        static TcpListener listener = null!;
        static List<TcpClient> Clients = new List<TcpClient>();
        static List<User> Users = new List<User>();
        static object obj = new object();
        static string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users.json");

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "TCP Server - Task 2";

            LoadUsers();

            var ip = IPAddress.Any;
            var port = 27001;
            var ep = new IPEndPoint(ip, port);

            listener = new TcpListener(ep);
            listener.Start();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================");
            Console.WriteLine("               TCP SERVER - İSTİFADƏÇİ PANELİ                 ");
            Console.WriteLine("===============================================================");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Server işə salındı...");
            Console.ResetColor();
            Console.WriteLine($"Dinlənilir: {listener.LocalEndpoint}");
            Console.WriteLine();

            PrintUsersTable();

            while (true)
            {
                var client = listener.AcceptTcpClient();
                Task.Run(() => HandleClient(client));
            }
        }

        static void HandleClient(TcpClient client)
        {
            string remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Bilinmir";
            User? authenticatedUser = null;
            NetworkStream stream = null!;
            BinaryReader br = null!;
            BinaryWriter bw = null!;

            try
            {
                stream = client.GetStream();
                br = new BinaryReader(stream);
                bw = new BinaryWriter(stream);

                string loginJson = br.ReadString();
                var loginReq = JsonSerializer.Deserialize<LoginRequest>(loginJson);

                if (loginReq == null || string.IsNullOrWhiteSpace(loginReq.username))
                {
                    SendResponse(bw, false, "Yanlış sorğu formatı.");
                    client.Close();
                    return;
                }

                lock (obj)
                {
                    var user = Users.FirstOrDefault(u =>
                        u.username.Equals(loginReq.username, StringComparison.OrdinalIgnoreCase));

                    if (user == null || user.password != loginReq.password)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[RƏDD EDİLDİ] Yanlış istifadəçi adı və ya şifrə: {loginReq.username} ({remoteEndPoint})");
                        Console.ResetColor();

                        SendResponse(bw, false, "İstifadəçi adı və ya şifrə yanlışdır!");
                        client.Close();
                        return;
                    }

                    if (user.isOnline)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[RƏDD EDİLDİ] {user.username} artıq online-dır! Mövcud IP: {user.remoteendpoint}, Giriş cəhdi edən: {remoteEndPoint}");
                        Console.ResetColor();

                        SendResponse(bw, false, $"Bu istifadəçi artıq başqa sessiyada ({user.remoteendpoint}) aktivdir!");
                        client.Close();
                        return;
                    }

                    user.isOnline = true;
                    user.remoteendpoint = remoteEndPoint;
                    authenticatedUser = user;
                    Clients.Add(client);

                    SaveUsers();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[GİRİŞ] {user.username} sistemə daxil oldu! IP: {user.remoteendpoint}");
                    Console.ResetColor();

                    SendResponse(bw, true, "Sistemə uğurla daxil oldunuz!");
                    PrintUsersTable();
                    BroadcastUserList();
                }

                while (true)
                {
                    var message = br.ReadString();

                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    if (message.Trim().Equals("Exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    Console.WriteLine($"[MESAJ] {authenticatedUser.username} ({authenticatedUser.remoteendpoint}): {message}");
                    BroadcastMessage(authenticatedUser.username, message, client);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (authenticatedUser != null)
                {
                    lock (obj)
                    {
                        authenticatedUser.isOnline = false;
                        SaveUsers();
                        Clients.Remove(client);

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"[ÇIXIŞ] {authenticatedUser.username} ({authenticatedUser.remoteendpoint}) sistemdən ayrıldı (OFFLINE).");
                        Console.ResetColor();

                        PrintUsersTable();
                        BroadcastUserList();
                    }
                }

                try { client.Close(); } catch { }
            }
        }

        static void SendResponse(BinaryWriter bw, bool success, string message)
        {
            var res = new LoginResponse { success = success, message = message };
            bw.Write(JsonSerializer.Serialize(res));
        }

        static void BroadcastUserList()
        {
            lock (obj)
            {
                var json = JsonSerializer.Serialize(Users);
                var message = $"[USER_LIST]{json}";

                foreach (var c in Clients.ToList())
                {
                    try
                    {
                        if (c.Connected)
                        {
                            var bw = new BinaryWriter(c.GetStream());
                            bw.Write(message);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        static void BroadcastMessage(string sender, string msg, TcpClient senderClient)
        {
            lock (obj)
            {
                var formatted = $"[ÇAT] {sender}: {msg}";
                foreach (var c in Clients.ToList())
                {
                    if (c != senderClient && c.Connected)
                    {
                        try
                        {
                            var bw = new BinaryWriter(c.GetStream());
                            bw.Write(formatted);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        static void LoadUsers()
        {
            if (File.Exists(jsonFilePath))
            {
                var json = File.ReadAllText(jsonFilePath);
                Users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }
            else
            {
                Users = new List<User>
                {
                    new User { username = "Mahmud", password = "123", remoteendpoint = "10.1.10.1:61362", isOnline = false },
                    new User { username = "Leyla", password = "123", remoteendpoint = "10.1.10.1:61361", isOnline = false },
                    new User { username = "Murad", password = "123", remoteendpoint = "-", isOnline = false },
                    new User { username = "Aysel", password = "123", remoteendpoint = "-", isOnline = false }
                };
            }

            foreach (var user in Users)
            {
                user.isOnline = false;
            }

            SaveUsers();
        }

        static void SaveUsers()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Users, options);
            File.WriteAllText(jsonFilePath, json);
        }

        static void PrintUsersTable()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine($"{"İstifadəçi",-15} {"Son IP Ünvanı",-25} {"Status",-10}");
            Console.WriteLine("---------------------------------------------------------------");
            Console.ResetColor();

            foreach (var user in Users)
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
            Console.WriteLine("---------------------------------------------------------------\n");
            Console.ResetColor();
        }
    }
}
