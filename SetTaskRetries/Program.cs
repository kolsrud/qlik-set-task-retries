using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace SetTaskRetries
{
    class Program
    {
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            var client = ParseConnectionArguments(args);
            try
            {
                client.Get("/qrs/about");
                Console.WriteLine("Connection successfully established.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to establish connection: " + e);
                return;
            }

            var retryArg = GetArg(args, "--retries", 1);
            var retryCount = 0;
            if (retryArg == null)
            {
                Console.WriteLine("No retry count specified. Using default value: " + retryCount);
            }
            else
            {
                retryCount = int.Parse(retryArg.Single());
                Console.WriteLine("Using specified retry count: " + retryCount);
            }

            var doApply = GetArg(args, "--apply", 0) != null;
            Console.WriteLine(doApply ? "Applying changes to tasks." : "Dry run only.");

            SetTaskRetries(client, retryCount, doApply);
        }

        private static void SetTaskRetries(IRestClient client, int retryCnt, bool doApply)
        {
            var tasksStr = client.Get("/qrs/reloadtask");
            var tasks = JArray.Parse(tasksStr);
            foreach (var task in tasks.OfType<JObject>())
            {
                var id = task.GetValue("id");
                var maxRetries = task.GetValue("maxRetries");
                var name = task.GetValue("name");
                Console.Write(id + " (" + maxRetries + ")");
                if (doApply)
                {
                    if (maxRetries.Value<int>() == retryCnt)
                        Console.Write(" - No need to update retry count");
                    else
                    {
                        var reloadTask = JObject.Parse(client.Get("/qrs/reloadtask/" + id));
                        Console.Write(" - Setting retry count to " + retryCnt);
                        reloadTask["maxRetries"] = retryCnt;
                        var body = new JObject {["task"] = reloadTask};
                        client.Post("/qrs/reloadtask/update", body.ToString(Formatting.None));
                    }
                }
                else
                {
                    Console.Write(" - Dry run only, no change applied");
                }
                Console.WriteLine(" : " + name);
                
            }
        }

        private static RestClient ParseConnectionArguments(string[] args)
        {
            RestClient client = null;

            var ntlmInfo = GetArg(args, "--ntlm", 1);
            if (ntlmInfo != null)
            {
                client = ProcessNtlmInfo(ntlmInfo);
            }
            else
            {
                var directInfo = GetArg(args, "--direct", 4);
                if (directInfo == null)
                {
                    PrintUsage();
                }

                client = ProcessDirectInfo(directInfo, GetArg(args, "--certs", 1));
            }

            return client;
        }

        private static RestClient ProcessDirectInfo(string[] directInfo, string[] certsInfo)
        {
            if (directInfo.Length != 4)
            {
                PrintUsage();
            }

            RestClient client = null;
            var url = directInfo[0];
            try
            {
                client = new RestClient(url);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create REST client for url: {0}", url);
                Console.WriteLine("  Error message: {0}", e.Message);
                PrintUsage();
            }

            if (!int.TryParse(directInfo[1], out var port))
            {
                Console.WriteLine("Failed to parse part as number: {0}", directInfo[1]);
                PrintUsage();
            }

            X509Certificate2Collection certs = null;
            try
            {
                if (certsInfo != null)
                {
                    if (certsInfo.Length != 1)
                    {
                        PrintUsage();
                    }
                    Console.WriteLine("Loading certificates from directory: " + certsInfo.Single());
                    certs = RestClient.LoadCertificateFromDirectory(certsInfo.Single());
                }
                else
                {
                    Console.WriteLine("Loading certificates from store.");
                    certs = RestClient.LoadCertificateFromStore();
                }
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine("Directory not found: {0}", e.Message);
                PrintUsage();
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("File not found: {0}", e.Message);
                PrintUsage();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to load certificates: {0}", e.Message);
                PrintUsage();
            }

            client.AsDirectConnection(directInfo[2], directInfo[3], port, false, certs);
            Console.WriteLine("Connecting as direct connection as ({0}\\{1}) to: {2}:{3}", directInfo[2], directInfo[3], url, port);

            return client;
        }

        private static RestClient ProcessNtlmInfo(string[] ntlmInfo)
        {
            if (ntlmInfo.Length != 1)
            {
                PrintUsage();
            }

            var url = ntlmInfo.Single();
            RestClient client = null;
            try
            {
                client = new RestClient(url);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create REST client for url: {0}", url);
                Console.WriteLine("  Error message: {0}", e.Message);
                PrintUsage();
            }
            client.AsNtlmUserViaProxy(certificateValidation: false);

            Console.WriteLine("Connecting as NTLM user ({0}\\{1}) to: {2}", Environment.UserDomainName,
                Environment.UserName, url);
            return client;
        }

        private static string[] GetArg(IEnumerable<string> args, string argName, int cnt)
        {
            var result = args.SkipWhile(arg => !argName.Equals(arg)).ToArray();
            return result.Any() ? result.Skip(1).Take(cnt).ToArray() : null;
        }

        private static void PrintUsage()
        {
            var binName = System.AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("Usage:   {0} --ntlm   <url> [--retries <retry cnt>] [--apply]", binName);
            Console.WriteLine("         {0} --direct <url> <port> <userDir> <userId> [--certs <path>] [--retries <retry cnt>] [--apply]", binName);
            Console.WriteLine("Example: {0} --ntlm   https://my.server.url --retries 3 --apply", binName);
            Console.WriteLine(@"         {0} --direct https://my.server.url 4242 MyUserDir MyUserId --certs C:\Tmp\MyCerts --retries 3", binName);
            Environment.Exit(1);
        }
    }
}
