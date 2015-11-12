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
        static string authCode, twoFactorAuth;
        static bool isRunning;


        static void Main(string[] args)

        {
            Console.Title = "Steam Bot Beta V0.1";
            Console.WriteLine("CTRL+C To Quit.....");

            Console.Write("Username: ");
            user = Console.ReadLine();

            Console.Write("Password: ");
            pass = Console.ReadLine();

            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamUser = steamClient.GetHandler<SteamUser>();

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnloggedOff);

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
    }
}
