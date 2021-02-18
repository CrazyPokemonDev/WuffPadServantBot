using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

            var res = await ValidateLanguageFile(e.Message);
            if (!res) // the file has critical errors, do not create Shcreibfelher (if the file is Deutsch.xml)
                return;

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
            var pm = msg.Chat.Type == ChatType.Private;

            var filepath = Path.Combine(validationPath, msg.Document.FileName);
            using (var stream = File.OpenWrite(filepath))
            {
                await Bot.GetInfoAndDownloadFileAsync(msg.Document.FileId, stream);
            }

            var psi = new ProcessStartInfo()
            {
                FileName = "py.exe",
                Arguments = $"\"{tgwwlangFile}\" check --json --model \"English.xml\" -- \"{msg.Document.FileName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = validationPath
            };

            string stdout;
            using (var p = Process.Start(psi))
            {
                stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
            }

            File.Delete(filepath);

            var result = JsonConvert.DeserializeObject<TgWWResult>(stdout);

            var warnings = new List<string>();
            var criticalErrors = new List<string>();
            var missingStrings = new List<string>();

            var a = result.Annotations.FirstOrDefault(x => x.File == TgWWFile.TargetFile);
            if (a != null)
            {
                foreach (var err in a.Errors)
                {
                    var lineNumber = (long)err[0];
                    var desc = (string)err[1];

                    var line = lineNumber == 0 ? "" : $"Line {lineNumber}: ";

                    var error = line + desc;
                    criticalErrors.Add(error);
                }

                if (!criticalErrors.Any())
                {
                    foreach (var mess in a.Messages)
                    {
                        if (!Enum.IsDefined(typeof(TgWWMessageCode), mess[0])) continue; // unknown error code... ignore

                        var messageCode = (TgWWMessageCode)mess[0];
                        var lineNumber = (long)mess[1]; // this needs to be long... I don't ask why
                        var details = ((JArray)mess[2]).ToObject<object[]>();

                        var line = lineNumber == 0 ? "" : $"Line {lineNumber}: ";

                        switch (messageCode)
                        {
                            #region Critical errors
                            case TgWWMessageCode.LanguageTagFieldEmpty:
                                var message = $"{line}Empty {details.ElementAt(0)} attribute in language tag";
                                criticalErrors.Add(message);
                                break;

                            case TgWWMessageCode.ValueEmpty:
                                message = $"{line}Empty values in {details.ElementAt(0)}";
                                criticalErrors.Add(message);
                                break;

                            case TgWWMessageCode.ValuesMissing:
                                message = $"{line}No values in {details.ElementAt(0)}";
                                criticalErrors.Add(message);
                                break;

                            case TgWWMessageCode.LangFileBaseVariantDuplication:
                                message = $"{line}Base/Variant matches English.xml! ({details.ElementAt(0)} {details.ElementAt(1)})";
                                criticalErrors.Add(message);
                                break;

                            case TgWWMessageCode.LangFileNameDuplication:
                                message = $"{line}Name matches English.xml! ({details.ElementAt(0)})";
                                criticalErrors.Add(message);
                                break;

                            case TgWWMessageCode.AttributeWronglyTrue:
                                message = $"{line}Attribute {details.ElementAt(1)} is true in {details.ElementAt(0)} but should be false";
                                criticalErrors.Add(message);
                                break;
                            #endregion

                            #region Warnings
                            case TgWWMessageCode.MissingString:
                                missingStrings.Add((string)details.ElementAt(0));
                                break;

                            case TgWWMessageCode.UnknownString:
                                message = $"{line}Unknown string: {details.ElementAt(0)}";
                                warnings.Add(message);
                                break;

                            case TgWWMessageCode.ExtraPlaceholder:
                                message = $"{line}Extra {details.ElementAt(1)} in {details.ElementAt(0)}";
                                warnings.Add(message);
                                break;

                            case TgWWMessageCode.MissingPlaceholder:
                                message = $"{line}Missing {details.ElementAt(1)} in {details.ElementAt(0)}";
                                warnings.Add(message);
                                break;

                            case TgWWMessageCode.DuplicatedString:
                                message = $"{line}Multiple definitions of {details.ElementAt(0)}";
                                warnings.Add(message);
                                break;

                            case TgWWMessageCode.TextOutsideValue:
                                message = $"{line}Text outside of value tags\n  If this is a comment, it should be like this: <!-- COMMENT HERE -->";
                                warnings.Add(message);
                                break;
                                #endregion
                        }
                    }
                }
            }

            string response;
            ValidationResult success;
            if (criticalErrors.Any())
            {
                success = ValidationResult.HasErrors;

                response = string.Join("\n", pm ? criticalErrors : criticalErrors.Take(5)); // in the group, only show up to 5 errors
                if (criticalErrors.Count > 5 && !pm) response += $"\nAnd {criticalErrors.Count - 5} more critical error(s).";
                if (!pm) response += $"\n\nIf you want to see a list of errors, you can also send the file to me in PM for validation.";
            }
            else if (warnings.Any() || missingStrings.Any())
            {
                success = ValidationResult.HasWarnings;

                response = string.Join("\n", pm ? warnings : warnings.Take(5)); // in the group, only show up to 5 warnings
                if (pm || missingStrings.Count <= 1)
                    foreach (var stringId in missingStrings)
                        response += $"\nMissing string: {stringId}";
                else
                {
                    response += "\nMissing strings: ";
                    response += string.Join(", ", missingStrings.Take(5));
                    if (missingStrings.Count > 5)
                        response += $", and {missingStrings.Count - 5} more";
                }
                if (!pm)
                {
                    if (warnings.Count > 5)
                        response += $"\nAnd {warnings.Count - 5} more warning(s).";
                    if (warnings.Count > 5 || missingStrings.Count > 5)
                        response += "\n\nIf you want to see a full list of warnings, you can send the file to me in PM for validation.";
                }
                response += "\n\nIt’s up to the admins to decide whether the file should be uploaded like this!";
            }
            else
            {
                success = ValidationResult.Perfect;
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

            switch (success)
            {
                case ValidationResult.HasErrors:
                    if (pm)
                    {
                        if (criticalErrors.Count > 5)
                            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(response)))
                                await Bot.SendDocumentAsync(msg.Chat.Id, new InputOnlineFile(stream, "errors.txt"), "❌ This file has CRITICAL errors!", replyToMessageId: msg.MessageId);
                        else
                            await Bot.SendTextMessageAsync(msg.Chat.Id, $"❌ This file has CRITICAL errors:\n\n{response}", replyToMessageId: msg.MessageId);
                    }
                    else
                        await Bot.SendTextMessageAsync(msg.Chat.Id, $"❌ DON’T UPLOAD! This file has CRITICAL errors:\n\n{response}", replyToMessageId: msg.MessageId);
                    return false;

                case ValidationResult.HasWarnings:
                    if (pm)
                    {
                        if (warnings.Count > 5 || missingStrings.Count > 5)
                            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(response)))
                                await Bot.SendDocumentAsync(msg.Chat.Id, new InputOnlineFile(stream, "warnings.txt"), "⚠️ This file CAN be uploaded, but it has flaws you should fix first!", replyToMessageId: msg.MessageId);
                        else
                            await Bot.SendTextMessageAsync(msg.Chat.Id, $"⚠️ This file CAN be uploaded, but it has flaws you should fix first:\n\n{response}", replyToMessageId: msg.MessageId);
                    }
                    else
                        await Bot.SendTextMessageAsync(msg.Chat.Id, $"⚠️ This file CAN be uploaded, but it has flaws:\n\n{response}", replyToMessageId: msg.MessageId);
                    return true;

                case ValidationResult.Perfect:
                    await Bot.SendTextMessageAsync(msg.Chat.Id, response, replyToMessageId: msg.MessageId);
                    return true;
            }

            throw new ArgumentOutOfRangeException(nameof(success), success, "Neither of the 3 validation results applied.");
        }

        enum ValidationResult
        {
            Perfect,
            HasWarnings,
            HasErrors
        }
        #endregion
    }
}
