﻿using System.Collections.Generic;
using Server.Structures;
using Server.Network;
using System.Globalization;
using System;
using System.IO;
using ZLibNet;

namespace Server.Functions
{
    public class UpdateHandler
    {
        public static readonly UpdateHandler Instance = new UpdateHandler();
        internal static string selfUpdatesDir = string.Concat(Directory.GetCurrentDirectory(), @"\self_updates\");
        internal static string selfUpdatePath = Path.Combine(selfUpdatesDir, @"Launcher.exe");
        internal static string updaterPath = Path.Combine(selfUpdatesDir, @"Updater.exe");
        internal static string indexPath = Path.Combine(Directory.GetCurrentDirectory(), @"index.opt");
        public List<IndexEntry> UpdateIndex = new List<IndexEntry>();
        protected List<string> legacyIndex = new List<string>();

        protected static string updatesDir
        {
            get
            {
                if (!string.IsNullOrEmpty(OPT.GetString("update.dir")))
                {
                    return OPT.GetString("update.dir");
                }
                else { return string.Format(@"{0}/{1}", Directory.GetCurrentDirectory(), "/updates/"); }
            }
        }

        public void LoadUpdateList()
        {
            switch (OPT.GetInt("send.type"))
            {
                case 0: // Google drive 
                    using (StreamReader sr = new StreamReader(File.Open(string.Format(@"{0}\{1}", Directory.GetCurrentDirectory(), "gIndex.opt"), FileMode.Open, FileAccess.Read)))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string[] optBlocks = line.Split('|');
                            if (optBlocks.Length == 4)
                            {
                                UpdateIndex.Add(new IndexEntry { FileName = optBlocks[0], SHA512 = optBlocks[1], Legacy = Convert.ToBoolean(Convert.ToInt32(optBlocks[2])), Delete = Convert.ToBoolean(Convert.ToInt32(optBlocks[3])) });
                            }
                        }
                    }
                    break;

                case 1: // HTTP
                    break;

                case 2: // FTP
                    break;

                case 3: // TCP
                    foreach (string filePath in Directory.GetFiles(updatesDir))
                    {
                        string fileName = Path.GetFileName(filePath);

                        UpdateIndex.Add(new IndexEntry
                        {
                            FileName = fileName,
                            SHA512 = Hash.GetSHA512Hash(filePath),
                            Legacy = OPT.IsLegacy(fileName),
                            Delete = OPT.IsDelete(fileName)
                        });
                    }
                    break;
            }
        }

        public void OnUserRequestUpdateDateTime(Client client)
        {
            DateTime dateTime = default(DateTime);

            switch (OPT.GetInt("send.type"))
            {
                case 0:
                    dateTime = Directory.GetLastWriteTimeUtc(indexPath);
                    break;

                case 1:
                    break;

                case 2:
                    break;

                case 3:
                    dateTime = Directory.GetLastWriteTimeUtc(updatesDir);
                    break;
            }

            if (dateTime != default(DateTime)) { ClientPackets.Instance.SC_SendUpdateTime(client, dateTime.ToString(CultureInfo.InvariantCulture)); }
            else { Console.WriteLine("Failed to get proper update time for Client [{0}]", client.Id); }
        }

        public void OnUserRequestSelfUpdate(Client client, string remoteHash)
        {
            if (Directory.Exists(selfUpdatesDir))
            {
                if (File.Exists(selfUpdatePath))
                {
                    string hash = Hash.GetSHA512Hash(selfUpdatePath);
                    if (hash != remoteHash)
                    {
                        string zipName = compressFile(selfUpdatePath);
                        
                        ClientPackets.Instance.SC_SendSelfUpdate(client, zipName);
                    }
                }
            }
        }

        internal void OnUserRequestUpdater(Client client, string remoteHash)
        {
            if (Directory.Exists(selfUpdatesDir))
            {
                if (File.Exists(updaterPath))
                {
                    string hash = Hash.GetSHA512Hash(updaterPath);
                    if (hash != remoteHash || remoteHash == "NO_HASH")
                    {
                        string zipName = compressFile(updaterPath);
                        ClientPackets.Instance.SC_SendSelfUpdater(client, zipName);
                    }
                }
            }
        }

        public void OnUserRequestUpdateIndex(Client client)
        {
            foreach (IndexEntry indexEntry in UpdateIndex) { ClientPackets.Instance.SC_SendUpdateIndex(client, indexEntry.FileName, indexEntry.SHA512, indexEntry.Legacy, indexEntry.Delete); }

            ClientPackets.Instance.SC_SendUpdateIndexEOF(client);
        }

        internal void OnUserRequestFile(Client client, string fileName)
        {
            if (OPT.GetBool("debug")) { Console.WriteLine("Client [{0}] requested file: {1}", client.Id, fileName); }

            string updatePath = string.Format(@"{0}\{1}", updatesDir, fileName);
            string archiveName = compressFile(updatePath);
            string archivePath = string.Format(@"{0}\{1}.zip", Program.tmpPath, archiveName);

            if (File.Exists(archivePath))
            {
                ClientPackets.Instance.SC_SendFile(client, archivePath);
            }
        }

        internal string compressFile(string filePath)
        {
            string name = OTP.GenerateRandomPassword(10);
            string zipPath = Path.Combine(Program.tmpPath, string.Concat(name, ".zip"));

            Zipper z = new Zipper();
            z.ItemList.Add(filePath);
            z.ZipFile = zipPath;
            z.Zip();
            z = null;

            return name;
        }
    }
}
