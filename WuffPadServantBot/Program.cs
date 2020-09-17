using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using WuffPadServantBot.XMLClasses;
using File = System.IO.File;

namespace WuffPadServantBot
{
    class Program
    {
        private const string tempFilePath = "temp.xml";
        private static TelegramBotClient Bot;
        private static readonly Regex regex = new Regex(@"[^\@]\b(\w)+\b");
        private static readonly Regex number = new Regex(@"\d+");
        private static readonly Random rnd = new Random();
        private const int newValueCount = 3;
        private const string authenticationFile = "C:\\Olfi01\\WuffPad\\auth.txt";
        private const string validationPath = "C:\\Olfi01\\WWValidation\\Files\\";
        private const string modelFile = "C:\\Olfi01\\WWValidation\\Files\\English.xml";
        private const string tgwwlangFile = "C:\\Olfi01\\WWValidation\\TgWWLang\\tgwwlang.py";

        static void Main(string[] args)
        {
            Bot = new TelegramBotClient(args[0]);

            Bot.OnMessage += OnMessage;
            Bot.OnCallbackQuery += WuffpadAuthenticator;

            Bot.StartReceiving();

            string input;
            do
            {
                input = Console.ReadLine();
            } while (input.ToLower() != "exit");

            Bot.StopReceiving();
        }

        private static async void OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Type != MessageType.Document) return;
            if (Path.GetExtension(e.Message.Document.FileName).ToLower() != ".xml") return;

            if (e.Message.Document.FileName.ToLower() == "english.xml")
            {
                await Bot.SendTextMessageAsync(e.Message.Chat.Id, "ℹ️ English.xml detected, skipping validation.", replyToMessageId: e.Message.MessageId);
                return;
            }

            await ValidateLanguageFile(e.Message);
            // we could theoretically use the returned bool and only create Shcreibfelher if the Deutsch.xml is fine.

            if (e.Message.Document.FileName == "Deutsch.xml")
                ShcreibfelherMaker(e);
        }

        #region Authenticator
        private static async void WuffpadAuthenticator(object sender, CallbackQueryEventArgs e)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(authenticationFile));
            if (!e.CallbackQuery.Data.StartsWith("auth:"))
            {
                if (e.CallbackQuery.Data != "dontauth") return;
                await Bot.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Denied authorization.");
                return;
            }
            var token = e.CallbackQuery.Data.Substring("auth:".Length);
            var userId = e.CallbackQuery.From.Id;
            if (!File.Exists(authenticationFile)) File.WriteAllText(authenticationFile, "{}");
            Dictionary<int, (List<string>, UserInfo)> authentication = JsonConvert.DeserializeObject<Dictionary<int, (List<string>, UserInfo)>>(File.ReadAllText(authenticationFile));
            if (!authentication.ContainsKey(userId)) authentication[userId] = (new List<string>(), new UserInfo());
            authentication[userId].Item1.Add(token);
            authentication[userId].Item2.Name = string.Join(" ", e.CallbackQuery.From.FirstName, e.CallbackQuery.From.LastName);
            authentication[userId].Item2.Username = e.CallbackQuery.From.Username ?? "no username";
            File.WriteAllText(authenticationFile, JsonConvert.SerializeObject(authentication));
            await Bot.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "Successfully verified your user!", showAlert: true);
            await Bot.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Authorized!");
        }

        private class UserInfo
        {
            public string Name { get; set; }
            public string Username { get; set; }

        }
        #endregion

        #region Shcreibfelher
        private static async void ShcreibfelherMaker(MessageEventArgs e)
        {
            Console.WriteLine("Received a Deutsch.xml file to randify!");
            Console.WriteLine("Downloading...");
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
            using (var stream = File.OpenWrite(tempFilePath))
            {
                await Bot.DownloadFileAsync(Bot.GetFileAsync(e.Message.Document.FileId).Result.FilePath, stream);
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
                await Bot.SendDocumentAsync(e.Message.Chat.Id, sendFile, caption: e.Message.Caption == null ? null : Randify(e.Message.Caption));
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
        #endregion

        #region Validator
        private static async Task<bool> ValidateLanguageFile(Message msg)
        {
            var filepath = Path.Combine(validationPath, msg.Document.FileName);
            using (var stream = File.OpenWrite(filepath))
            {
                await Bot.GetInfoAndDownloadFileAsync(msg.Document.FileId, stream);
            }

            var psi = new ProcessStartInfo()
            {
                FileName = "py.exe",
                Arguments = $"{tgwwlangFile} check {filepath} --json --model {modelFile}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            string stdout;
            using (var p = Process.Start(psi))
            {
                stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
            }

            File.Delete(filepath);

            var result = JsonConvert.DeserializeObject<TgWWResult>(stdout);

            int missingStrings = 0, unknownStrings = 0, placeholderErrors = 0, duplicatedStrings = 0;
            List<string> criticalErrors = new List<string>();

            var a = result.Annotations.FirstOrDefault(x => x.File == TgWWFile.TargetFile);
            if (a != null)
            {
                foreach (var mess in a.Messages)
                {
                    if (!Enum.IsDefined(typeof(TgWWMessageCode), mess[0])) continue; // unknown error code... ignore

                    var messageCode = (TgWWMessageCode)mess[0];
                    var lineNumber = (long)mess[1]; // this needs to be long... I don't ask why
                    var details = ((JArray)mess[2]).ToObject<object[]>();

                    switch (messageCode)
                    {
                        case TgWWMessageCode.MissingString:
                            missingStrings++;
                            break;

                        case TgWWMessageCode.UnknownString:
                            unknownStrings++;
                            break;

                        case TgWWMessageCode.ExtraPlaceholder:
                        case TgWWMessageCode.MissingPlaceholder:
                        case TgWWMessageCode.InconsistentPlaceholders:
                            placeholderErrors++;
                            break;

                        case TgWWMessageCode.LanguageTagFieldEmpty:
                            criticalErrors.Add("L" + lineNumber + ": " + string.Format("<language {0}=\"\" /> must not be empty!", details));
                            break;

                        case TgWWMessageCode.DuplicatedString:
                            duplicatedStrings++;
                            break;

                        case TgWWMessageCode.ValueEmpty:
                            criticalErrors.Add("L" + lineNumber + ": " + string.Format("The <string key=\"{0}\"> contains empty values!", details));
                            break;

                        case TgWWMessageCode.ValuesMissing:
                            criticalErrors.Add("L" + lineNumber + ": " + string.Format("The <string key=\"{0}\"> doesn't contain any values!", details));
                            break;
                    }
                }

                foreach (var err in a.Errors)
                {
                    var lineNumber = (long)err[0];
                    var desc = (string)err[1];

                    criticalErrors.Add("L" + lineNumber + ": " + desc);
                }
            }

            string response;
            if (criticalErrors.Any())
            {
                response = "❌ DON'T UPLOAD! This file has CRITICAL errors:\n" + string.Join("\n", criticalErrors);
            }
            else if (missingStrings != 0 || unknownStrings != 0 || placeholderErrors != 0 || duplicatedStrings != 0)
            {
                response = "⚠️ This file CAN be uploaded, but it has flaws:\n";
                if (missingStrings != 0) response += $"{missingStrings} missing string{(missingStrings == 1 ? "" : "s")}\n";
                if (unknownStrings != 0) response += $"{unknownStrings} unknown string{(unknownStrings == 1 ? "" : "s")}\n";
                if (placeholderErrors != 0) response += $"{placeholderErrors} error{(placeholderErrors == 1 ? "" : "s")} regarding {{#}}\n";
                if (duplicatedStrings != 0) response += $"{duplicatedStrings} duplicated string{(duplicatedStrings == 1 ? "" : "s")}\n";

                response += "\nIt's up to the admins to decide whether the file should be uploaded like this!";
            }
            else
            {
                response = "✅ This file is perfect and can be uploaded!";
            }

            await Bot.SendTextMessageAsync(msg.Chat.Id, response, replyToMessageId: msg.MessageId);
            return true;
        }
        #endregion
    }
}
