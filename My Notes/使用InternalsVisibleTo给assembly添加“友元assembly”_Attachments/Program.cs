namespace PatchInternalVisibleTo
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    internal class Program
    {
        private static string CreatePublicKey(string snExe, string keyFileName)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = snExe,
                Arguments = string.Format("-p {0} {1}", keyFileName, "dx_public_key.tmp"),
                UseShellExecute = false
            };
            Process.Start(startInfo).WaitForExit();
            return "dx_public_key.tmp";
        }

        private static string CreatePublicKeyToken(string snExe, string keyFileName)
        {
            string publicKeyName = CreatePublicKey(snExe, keyFileName);
            string str2 = ObtainPublicKeyToken(snExe, publicKeyName);
            File.Delete(publicKeyName);
            return str2;
        }

        private static void Main(string[] args)
        {
            Console.WriteLine("InternalVisibleTo attribute patcher (C) Developer Express Inc.");
            if (args.Length != 3)
            {
                Console.WriteLine("Usage: PatchInternalVisibleTo <patched file path> <sn.exe full path> <strong key full path>");
            }
            else
            {
                string path = args[0];
                if (!File.Exists(path))
                {
                    Console.WriteLine("The file {0} not found", path);
                }
                else
                {
                    string str2 = args[1];
                    if (!File.Exists(str2))
                    {
                        Console.WriteLine("The file {0} not found", str2);
                    }
                    else
                    {
                        string str3 = args[2];
                        if (!File.Exists(str3))
                        {
                            Console.WriteLine("The file {0} not found", str3);
                        }
                        else
                        {
                            PerformPatch(path, str2, str3);
                        }
                    }
                }
            }
        }

        private static string ObtainPublicKeyToken(string snExe, string publicKeyName)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = snExe,
                Arguments = string.Format("-o {0} {1}", publicKeyName, "dx_public_key_token.csv.tmp"),
                UseShellExecute = false
            };
            Process.Start(startInfo).WaitForExit();
            string str = string.Empty;
            using (StreamReader reader = new StreamReader("dx_public_key_token.csv.tmp"))
            {
                str = reader.ReadToEnd();
                reader.Close();
            }
            File.Delete("dx_public_key_token.csv.tmp");
            string[] strArray = str.Split(new char[] { ',' });
            StringBuilder builder = new StringBuilder();
            int length = strArray.Length;
            for (int i = 0; i < length; i++)
            {
                int num3 = int.Parse(strArray[i]);
                builder.AppendFormat("{0:x2}", num3);
            }
            return builder.ToString();
        }

        private static void PatchFile(string fileName, string publicKeyToken)
        {
            string input = string.Empty;
            using (StreamReader reader = new StreamReader(fileName))
            {
                input = reader.ReadToEnd();
                reader.Close();
            }
            input = new Regex("\",\\s*PublicKey=[0123456789abcdefABCDEF]*\"").Replace(input, string.Format("\", PublicKey={0}\"", publicKeyToken));
            string randomFileName = Path.GetRandomFileName();
            using (StreamWriter writer = new StreamWriter(randomFileName))
            {
                writer.Write(input);
                writer.Close();
            }
            File.Delete(fileName);
            File.Move(randomFileName, fileName);
        }

        private static void PerformPatch(string fileName, string snExe, string keyFileName)
        {
            string publicKeyToken = CreatePublicKeyToken(snExe, keyFileName);
            PatchFile(fileName, publicKeyToken);
        }
    }
}

