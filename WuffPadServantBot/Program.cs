using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using WuffPadServantBot.XMLClasses;

namespace WuffPadServantBot
{
    class Program
    {
        private const string tempFilePath = "temp.xml";
        private static TelegramBotClient Bot;
        private static Regex regex = new Regex(@"[^\@]\b(\w)+\b");
        private static Regex number = new Regex(@"\d+");
        private static Random rnd = new Random();
        private const int newValueCount = 3;
        private const string authenticationFile = "C:\\Olfi01\\WuffPad\\auth.txt";
        static void Main(string[] args)
        {
            Bot = new TelegramBotClient(args[0]);

            Bot.OnMessage += ShcreibfelherMaker;
            Bot.OnCallbackQuery += WuffpadAuthenticator;

            Bot.StartReceiving();

            string input;
            do
            {
                input = Console.ReadLine();
            } while (input.ToLower() != "exit");

            Bot.StopReceiving();
        }

        private static void WuffpadAuthenticator(object sender, CallbackQueryEventArgs e)
        {
            if (!e.CallbackQuery.Data.StartsWith("auth:"))
            {
                if (e.CallbackQuery.Data != "dontauth") return;
                Bot.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Denied authorization.");
                return;
            }
            var token = e.CallbackQuery.Data.Substring("auth:".Length);
            var userId = e.CallbackQuery.From.Id;
            if (!File.Exists(authenticationFile)) File.WriteAllText(authenticationFile, "{}");
            Dictionary<int, List<string>> authentication = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(File.ReadAllText(authenticationFile));
            if (!authentication.ContainsKey(userId)) authentication[userId] = new List<string>();
            authentication[userId].Add(token);
            File.WriteAllText(authenticationFile, JsonConvert.SerializeObject(authentication));
            Bot.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "Successfully verified your user!", showAlert: true);
        }

        private static void ShcreibfelherMaker(object sender, MessageEventArgs e)
        {
            if (e.Message.Type != MessageType.Document) return;
            if (e.Message.Document.FileName != "Deutsch.xml") return;

            Console.WriteLine("Received a file!");
            Console.WriteLine("Downloading...");
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
            using (var stream = File.OpenWrite(tempFilePath))
            {
                Bot.DownloadFileAsync(Bot.GetFileAsync(e.Message.Document.FileId).Result.FilePath, stream).Wait();
            }
            Console.WriteLine("Processing...");
            XmlStrings newFile = MakeNewFile();
            newFile.Language.Variant = "Shcreibfelher";
            newFile.Language.Name = "Deutsch Schreibfehler";
            newFile.Language.Owner = "WWUebersetzen";

            string newFileString = SerializeXmlToString(newFile);
            File.WriteAllText("Deutsch Shcreibfelher.xml", newFileString);

            Console.WriteLine("Sending...");
            using (var stream = File.OpenRead("Deutsch Shcreibfelher.xml"))
            {
                InputOnlineFile sendFile = new InputOnlineFile(stream)
                {
                    FileName = "Deutsch Shcreibfelher.xml"
                };
                Bot.SendDocumentAsync(e.Message.Chat.Id, sendFile, caption: e.Message.Caption == null ? null : Randify(e.Message.Caption)).Wait();
            }

            Console.WriteLine("Cleaning up...");
            File.Delete(tempFilePath);
            File.Delete("Deutsch Shcreibfelher.xml");

            Console.WriteLine("Done!");
        }

        private static XmlStrings MakeNewFile()
        {
            string fileString = File.ReadAllText(tempFilePath);
            XmlStrings file = ReadXmlString(fileString);
            XmlStrings newFile = new XmlStrings()
            {
                Language = file.Language
            };
            foreach (XmlString str in file.Strings)
            {
                XmlString newStr = new XmlString()
                {
                    Isgif = str.Isgif,
                    Key = str.Key
                };
                foreach (string value in str.Values)
                {
                    for (int i = 0; i < newValueCount; i++) newStr.Values.Add(Randify(value));
                }
                newFile.Strings.Add(newStr);
            }
            return newFile;
        }

        private static string Randify(string value)
        {
            Dictionary<string, string> replace = new Dictionary<string, string>();
            foreach (Match m in regex.Matches(value.Replace("\\n", "\n")))
            {
                string match = m.Value.Trim();
                if (number.IsMatch(match) || match.Length < 4)
                {
                    continue;
                }
                string output;
                do
                {
                    string first = match.Substring(0, 1);
                    string last = match.Substring(match.Length - 1);
                    string proc = match.Substring(1, match.Length - 2);
                    output = first;
                    char[] chars = new char[proc.Length];
                    var randomNumbers = Enumerable.Range(0, proc.Length).OrderBy(x => rnd.Next()).Take(proc.Length).ToList();
                    for (int i = 0; i < proc.Length; i++)
                    {
                        chars[i] = proc[randomNumbers[i]];
                    }
                    foreach (var c in chars)
                    {
                        output += c;
                    }
                    output += last;
                } while (match == output && rnd.Next(10) < 8);
                if (!replace.ContainsKey(match)) replace.Add(match, output);
            }
            string newValue = value.Replace("\\n", "\n");
            foreach (var kvp in replace)
            {
                newValue = newValue.Replace(kvp.Key, kvp.Value);
            }

            return newValue.Replace("\n", "\\n");
        }

        private static XmlStrings ReadXmlString(string fileString)
        {
            XmlStrings result;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(XmlStrings));
                using (TextReader tr = new StringReader(fileString))
                {
                    result = (XmlStrings)serializer.Deserialize(tr);
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        private static string SerializeXmlToString(XmlStrings xmls)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(XmlStrings));
            using (TextWriter tw = new StringWriter())
            {
                serializer.Serialize(tw, xmls);
                string result = tw.ToString();
                //result = Utf16ToUtf8(result);
                return result.Replace("utf-16", "utf-8");
            }
        }
    }
}
