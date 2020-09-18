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

            Bot.OnMessage += async (sender, e) => await OnMessage(sender, e);
            Bot.OnCallbackQuery += async (sender, e) => await WuffpadAuthenticator(sender, e);

            Bot.StartReceiving();

            string input;
            do
            {
                input = Console.ReadLine();
            } while (input.ToLower() != "exit");

            Bot.StopReceiving();
        }

        private static async Task OnMessage(object sender, MessageEventArgs e)
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
                await ShcreibfelherMaker(e);
        }

        #region Authenticator
        private static async Task WuffpadAuthenticator(object sender, CallbackQueryEventArgs e)
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
        private static async Task ShcreibfelherMaker(MessageEventArgs e)
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
                Arguments = $"\"{tgwwlangFile}\" check --json --model \"{modelFile}\" -- \"{filepath}\"",
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

            List<string> unknownStrings = new List<string>();
            List<string> duplicatedStrings = new List<string>();
            List<string> missingStrings = new List<string>();
            List<string> placeholderErrors = new List<string>();
            List<long> textOutsideValues = new List<long>();
            List<string> criticalErrors = new List<string>();

            var a = result.Annotations.FirstOrDefault(x => x.File == TgWWFile.TargetFile);
            if (a != null)
            {
                foreach (var err in a.Errors)
                {
                    var lineNumber = (long)err[0];
                    var desc = (string)err[1];

                    var line = lineNumber == 0 ? "" : $"L{lineNumber}: ";

                    criticalErrors.Add(line + desc);
                }

                if (!criticalErrors.Any())
                {
                    foreach (var mess in a.Messages)
                    {
                        if (!Enum.IsDefined(typeof(TgWWMessageCode), mess[0])) continue; // unknown error code... ignore

                        var messageCode = (TgWWMessageCode)mess[0];
                        var lineNumber = (long)mess[1]; // this needs to be long... I don't ask why
                        var details = ((JArray)mess[2]).ToObject<object[]>();

                        var line = lineNumber == 0 ? "" : $"L{lineNumber}: ";

                        switch (messageCode)
                        {
                            case TgWWMessageCode.MissingString:
                                missingStrings.Add((string)details.ElementAt(0));
                                break;

                            case TgWWMessageCode.UnknownString:
                                unknownStrings.Add((string)details.ElementAt(0));
                                break;

                            case TgWWMessageCode.ExtraPlaceholder:
                            case TgWWMessageCode.MissingPlaceholder:
                                placeholderErrors.Add((string)details.ElementAt(0));
                                break;

                            case TgWWMessageCode.LanguageTagFieldEmpty:
                                criticalErrors.Add(line + string.Format("<language {0}=\"\" /> must not be empty!", details));
                                break;

                            case TgWWMessageCode.DuplicatedString:
                                duplicatedStrings.Add((string)details.ElementAt(0));
                                break;

                            case TgWWMessageCode.ValueEmpty:
                                criticalErrors.Add(line + string.Format("The <string key=\"{0}\"> contains empty values!", details));
                                break;

                            case TgWWMessageCode.ValuesMissing:
                                criticalErrors.Add(line + string.Format("The <string key=\"{0}\"> doesn't contain any values!", details));
                                break;

                            case TgWWMessageCode.LangFileBaseVariantDuplication:
                                criticalErrors.Add(line + string.Format("The <language base=\"{0}\" variant=\"{1}\" /> is not changed from English.xml!", details));
                                break;

                            case TgWWMessageCode.LangFileNameDuplication:
                                criticalErrors.Add(line + string.Format("The <language name=\"{0}\"> is not changed from English.xml!", details));
                                break;

                            case TgWWMessageCode.TextOutsideValue:
                                textOutsideValues.Add(lineNumber);
                                break;

                            case TgWWMessageCode.AttributeWronglyTrue:
                                criticalErrors.Add(line + string.Format("The <string key=\"{0}\"> has the {1} attribute set to true, but it should be false!", details));
                                break;
                        }
                    }
                }
            }

            string response;
            bool success;
            if (criticalErrors.Any())
            {
                success = false;
                response = "❌ DON'T UPLOAD! This file has CRITICAL errors:\n" + string.Join("\n", criticalErrors.Take(5));
                if (criticalErrors.Count > 5) response += $"\nAnd {criticalErrors.Count - 5} more critical error(s)";
            }
            else if (missingStrings.Any() || unknownStrings.Any() || placeholderErrors.Any() || duplicatedStrings.Any() || textOutsideValues.Any())
            {
                missingStrings = missingStrings.Distinct().ToList();
                unknownStrings = unknownStrings.Distinct().ToList();
                placeholderErrors = placeholderErrors.Distinct().ToList();
                duplicatedStrings = duplicatedStrings.Distinct().ToList();
                textOutsideValues = textOutsideValues.Distinct().ToList();

                success = true;
                response = "⚠️ This file CAN be uploaded, but it has flaws:\n";

                if (missingStrings.Any())
                {
                    response += $"{missingStrings.Count} missing string{(missingStrings.Count == 1 ? "" : "s")}: {string.Join(", ", missingStrings.Take(5))}" +
                        $"{(missingStrings.Count > 5 ? $" (and {missingStrings.Count - 5} more)" : "")}\n";
                }
                if (unknownStrings.Any())
                {
                    response += $"{unknownStrings.Count} unknown string{(unknownStrings.Count == 1 ? "" : "s")}: {string.Join(", ", unknownStrings.Take(5))}" +
                        $"{(unknownStrings.Count > 5 ? $" (and {unknownStrings.Count - 5} more)" : "")}\n";
                }
                if (placeholderErrors.Any())
                {
                    response += $"{placeholderErrors.Count} error{(placeholderErrors.Count == 1 ? "" : "s")} regarding {{#}}: " +
                        $"{string.Join(", ", placeholderErrors.Take(5))}" +
                        $"{(placeholderErrors.Count > 5 ? $" (and {placeholderErrors.Count - 5} more)" : "")}\n";
                }
                if (duplicatedStrings.Any())
                {
                    response += $"{duplicatedStrings.Count} duplicated string{(duplicatedStrings.Count == 1 ? "" : "s")}: {string.Join(", ", duplicatedStrings.Take(5))}" +
                        $"{(duplicatedStrings.Count > 5 ? $" (and {duplicatedStrings.Count - 5} more)" : "")}\n";
                }
                if (textOutsideValues.Any())
                {
                    response += $"There is text outside of <value> tags in {textOutsideValues.Count} lines: " +
                        $"{string.Join(", ", textOutsideValues.Take(5).Select(x => $"L{x}"))}" +
                        $"{(textOutsideValues.Count > 5 ? $" (and {textOutsideValues.Count - 5} more)" : "")}\n" +
                        $"If that text was meant to be a comment, it should be an XML comment like this: <!-- COMMENT HERE -->\n";
                }

                response += "\nIt's up to the admins to decide whether the file should be uploaded like this!";
            }
            else
            {
                success = true;
                response = "✅ This file is perfect and can be uploaded!";
            }

            a = result.Annotations.FirstOrDefault(x => x.File == TgWWFile.ModelFile);
            if (a != null)
            {
                if (a.Errors.Any()) response += "\n\nWarning: critical errors are present in the model file on the server!";
                else if (a.Messages.Any())
                {
                    HashSet<long> codes = new HashSet<long>();
                    foreach (var mess in a.Messages)
                    {
                        if (!Enum.IsDefined(typeof(TgWWMessageCode), mess[0]))
                        {
                            codes.Add((long)mess[0]);
                            continue;
                        }

                        var messageCode = (TgWWMessageCode)mess[0];
                        switch (messageCode)
                        {
                            case TgWWMessageCode.DuplicatedString:
                            case TgWWMessageCode.InconsistentPlaceholders:
                            case TgWWMessageCode.LanguageTagFieldEmpty:
                            case TgWWMessageCode.ModelNotDefault:
                            case TgWWMessageCode.ValueEmpty:
                            case TgWWMessageCode.ValuesMissing:
                            case TgWWMessageCode.TextOutsideValue:
                                codes.Add((long)messageCode);
                                break;
                        }
                    }
                    if (codes.Any())
                    {
                        response += "\n\nWarning: (non-critical) error(s) present in the model file on the server: " + string.Join(", ", codes);
                    }
                }
            }

            await Bot.SendTextMessageAsync(msg.Chat.Id, response, replyToMessageId: msg.MessageId);
            return success;
        }
        #endregion
    }
}
