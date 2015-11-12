using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamKit2;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace SteamBot
{
    class Program
    {
        static string user, pass;
        static SteamClient steamClient;
        static CallbackManager manager;
        static SteamUser steamUser;
        static SteamFriends steamFriends;
        static string authCode, twoFactorAuth;
        static bool isRunning;


        static void Main(string[] args)

        {
            if (!File.Exists("chat.txt"))
            {
                File.Create("chat.txt").Close();
                File.WriteAllText("chat.txt", "abc | 123");
            }

            Console.Title = "Steam Bot Beta V0.1";
            Console.WriteLine("CTRL+C To Quit.....");

            Console.Write("Username: ");
            user = Console.ReadLine();

            Console.Write("Password: ");
            pass = Console.ReadLine();

            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamUser = steamClient.GetHandler<SteamUser>();
            steamFriends = steamClient.GetHandler<SteamFriends>();

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnloggedOff);
            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnChatMessage);

            manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);

            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);

            isRunning = true;

            Console.WriteLine("\nConnecting To Steam....\n");

            steamClient.Connect();

            while (isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable To Connect Steam Because {0}", callback.Result);
                isRunning = false;
                return;
            }

            Console.WriteLine("\nConnected To Steam! Logging In {0}", user);

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,

                AuthCode = authCode,

                TwoFactorCode = twoFactorAuth,

                SentryFileHash = sentryHash,

            });
        }

        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected form Steam, reconnecting in 5 seconds..");
            Thread.Sleep(TimeSpan.FromSeconds(5));
            steamClient.Connect();
        }

        static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {
                Console.WriteLine("This Account Proctected By Steam Guard!..");

                if (is2FA)
                {
                    Console.Write("Please Enter You 2 Factor Auth Code From Your App: ");
                    twoFactorAuth = Console.ReadLine();
                }
                else
                {
                    Console.Write("Please Enter The Auth Code Sent To Your Email at {0}: ", callback.EmailDomain);
                    authCode = Console.ReadLine();
                }
                return;
            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable To Login To Steam Because {0]/{1}", callback.Result, callback.ExtendedResult);
                isRunning = false;
                return;
            }
            Console.WriteLine("Sucesfully Logged On To Steam");
        }

        static void OnloggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged Off Of Steam: {0}", callback.Result);
        }

        static void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating SentryFile.....");

            int fileSize;
            byte[] sentryHash;
            using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new SHA1CryptoServiceProvider())
                {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,
                FileName = callback.FileName,
                BytesWritten = callback.BytesToWrite,
                FileSize = fileSize,
                Offset = callback.Offset,
                Result = EResult.OK,
                LastError = 0,
                OneTimePassword = callback.OneTimePassword,
                SentryFileHash = sentryHash,
            });
            Console.WriteLine("Done Saving SentryFile...");
        }

        static void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online);
        }

        static void OnChatMessage(SteamFriends.FriendMsgCallback callback)
        {
            string[] args;
            if (callback.EntryType == EChatEntryType.ChatMsg)
            {
                if (callback.Message.Length > 1)
                {
                    if (callback.Message.Remove(1) == "!")
                    {
                        string command = callback.Message;
                        if (callback.Message.Contains(" "))
                        {
                            command = callback.Message.Remove(callback.Message.IndexOf(' '));
                        }

                        switch (command)
                        {
                            case "!send":
                                args = seperate(2, ' ', callback.Message);
                                Console.WriteLine("!send " + args[1] + args[2] +" command receive. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Wrong Commnand Syntax: !send [friend] [message]");
                                    return;
                                }
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    if (steamFriends.GetFriendPersonaName(friend).ToLower().Contains(args[1].ToLower()))
                                    {
                                        steamFriends.SendChatMessage(friend, EChatEntryType.ChatMsg, args[2]);
                                    }
                                }
                                    break;
                        }
                    }
                }
            }
            string rline;
            string trimmed = callback.Message;
            char[] trim = { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', '\\', '|', ';', ':', '"', '\'', ',', '<', '.', '>', '/', '?' };

            for (int i = 0; i < 30; i++)
            {
                trimmed = trimmed.Replace(trim[i].ToString(), "");
            }

            StreamReader cReader = new StreamReader("chat.txt");

            while((rline = cReader.ReadLine()) != null)
            {
                string text = rline.Remove(rline.IndexOf('|') - 1);
                string msg = rline.Remove(0, rline.IndexOf('|') + 2);

                if(callback.Message.Contains(text))
                {
                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, msg);
                    cReader.Close();
                    return;
                }
            }
        }

        static void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            Thread.Sleep(TimeSpan.FromSeconds(30));
            foreach (var friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    steamFriends.AddFriend(friend.SteamID);
                    Thread.Sleep(500);
                    steamFriends.SendChatMessage(friend.SteamID, EChatEntryType.ChatMsg, "Hello! I'm Now Your Friend!");
                }
            }
        }

        public static string[] seperate(int number, char seperator, string thestring)
        {
            string[] returned = new string[4];

            int i = 0;

            int error = 0;

            int length = thestring.Length;

            foreach(char c in thestring)
            {
                if (i != number)
                {
                    if (error > length || number > 5)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                    else if (c == seperator)
                    {
                        returned[1] = thestring.Remove(thestring.IndexOf(c));
                        thestring = thestring.Remove(0, thestring.IndexOf(c) + 1);
                        i++;
                    }
                    error++;

                    if (error == length && i != number)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                }
                else
                {
                    returned[i] = thestring;
                }
            }
            return returned;
        }

    }
}
